using Microsoft.EntityFrameworkCore;
using PandemicShield.Bio.Variants;
using PandemicShield.Contracts;
using PandemicShield.Contracts.Events;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using PandemicShield.Nextclade.Results;
using PandemicShield.Nextclade.Rules;
using PandemicShield.Nextclade.Sorting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace PandemicShield.Nextclade
{
    /// <summary>
    /// Воркер Nextclade: бере завдання з черги nextclade_jobs, запускає Nextclade CLI на файлі запуску,
    /// зберігає клади, лінії, QC і мутації кожної послідовності та створює сповіщення за маркерами-варіантами.
    /// </summary>
    internal class Program
    {
        // Подія для застосунку (через API і SignalR): статус запуску чи сповіщення змінились.
        private static readonly ChangeNotifier Changes = new();

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var runner = NextcladeRunner.FromEnvironment();
            string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

            var factory = new ConnectionFactory() { HostName = rabbitHost };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "nextclade_jobs",
                durable: true,
                exclusive: false,
                autoDelete: false);

            await channel.BasicQosAsync(prefetchCount: 1, prefetchSize: 0, global: false);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                NextcladeJobMessage? job = JsonSerializer.Deserialize<NextcladeJobMessage>(ea.Body.Span);
                if (job != null)
                {
                    await ProcessJobAsync(job, runner);
                }

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            };

            await channel.BasicConsumeAsync(queue: "nextclade_jobs", autoAck: false, consumer: consumer);

            Console.WriteLine("Nextclade-воркер чекає на завдання...");
            Thread.Sleep(Timeout.Infinite);
        }

        private static async Task ProcessJobAsync(NextcladeJobMessage job, NextcladeRunner runner)
        {
            Console.WriteLine($"Запуск {job.RunId}: Nextclade ({job.Dataset}) для {job.FilePath}");

            await UpdateRunAsync(job.RunId, run => run.MarkNextcladeRunning());

            try
            {
                List<VariantRule> rules = await LoadRulesAsync();
                string outputDir = Path.Combine(Path.GetDirectoryName(job.FilePath)!, "nextclade");

                var (summary, breakdown) = job.Dataset == NextcladeRunner.AutoDataset
                    ? await RunSortedAsync(job, runner, rules, outputDir)
                    : (await RunDatasetAsync(job.RunId, job.FilePath, job.Dataset, runner, rules, outputDir), null);

                string message = $"Послідовностей: {summary.Sequences} (з помилкою: {summary.Failed}), " +
                                 $"мутацій: {summary.Mutations}, сповіщень: {summary.Alerts}" +
                                 (breakdown == null ? "" : $". Набори: {breakdown}");
                await UpdateRunAsync(job.RunId, run => run.MarkNextcladeCompleted(summary.Sequences, message));
                if (summary.Alerts > 0) await Changes.NotifyAsync(ChangeTopics.Alerts, job.RunId);
                Console.WriteLine($"Запуск {job.RunId}: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Запуск {job.RunId}: помилка Nextclade: {ex.Message}");
                await UpdateRunAsync(job.RunId, run => run.MarkNextcladeFailed(ex.Message));
            }
        }

        private static async Task<StoreSummary> RunDatasetAsync(Guid runId, string fastaPath, string dataset, NextcladeRunner runner,
            IReadOnlyList<VariantRule> rules, string outputDir)
        {
            string ndjson = await runner.RunAsync(fastaPath, dataset, outputDir);
            using var reader = new StreamReader(ndjson);
            return await NextcladeResultStore.SaveAsync(runId, dataset, runner.DatasetTag(dataset),
                NextcladeNdjsonReader.Read(reader), rules, () => new PandemicDbContext());
        }

        /// <summary>
        /// Автовибір: <c>nextclade sort</c> визначає набір для кожної послідовності, файл розкладається по наборах,
        /// і Nextclade проганяється для кожного окремо (SARS-CoV-2 і грип з одного файлу — у своїх наборах).
        /// </summary>
        private static async Task<(StoreSummary Summary, string Breakdown)> RunSortedAsync(NextcladeJobMessage job, NextcladeRunner runner,
            IReadOnlyList<VariantRule> rules, string outputDir)
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
            string sortTsv = await runner.SortAsync(job.FilePath, outputDir);

            Dictionary<int, SortMatch> assignment;
            using (var reader = new StreamReader(sortTsv))
                assignment = DatasetSorter.Assign(DatasetSorter.Read(reader), runner.InstalledDatasets());

            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            var writers = new List<StreamWriter>();
            FastaSplitter.SplitResult split;
            using (var input = new StreamReader(job.FilePath))
            {
                split = FastaSplitter.Split(input, assignment, dataset =>
                {
                    string path = Path.Combine(outputDir, "sorted", $"{files.Count}.fasta");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    files[dataset] = path;
                    var writer = new StreamWriter(path);
                    writers.Add(writer);
                    return writer;
                });
            }
            foreach (var writer in writers) writer.Dispose();

            int sequences = 0, failed = 0, mutations = 0, alerts = 0;
            foreach (var (dataset, path) in files)
            {
                var part = await RunDatasetAsync(job.RunId, path, dataset, runner, rules, Path.Combine(outputDir, $"run-{Path.GetFileNameWithoutExtension(path)}"));
                sequences += part.Sequences;
                failed += part.Failed;
                mutations += part.Mutations;
                alerts += part.Alerts;
            }

            if (split.Unassigned.Count > 0)
            {
                await NextcladeResultStore.SaveUnassignedAsync(job.RunId, split.Unassigned,
                    "Не вдалося визначити вірус: послідовність не схожа на жоден встановлений набір даних Nextclade.",
                    () => new PandemicDbContext());
            }

            string breakdown = string.Join(", ", split.RecordsPerDataset.OrderByDescending(p => p.Value).Select(p => $"{p.Key} — {p.Value}"));
            if (split.Unassigned.Count > 0) breakdown += $", не визначено — {split.Unassigned.Count}";
            return (new StoreSummary(sequences + split.Unassigned.Count, failed + split.Unassigned.Count, mutations, alerts), breakdown);
        }

        private static async Task<List<VariantRule>> LoadRulesAsync()
        {
            using var db = new PandemicDbContext();
            var markers = await db.VariantMarkers.AsNoTracking().ToListAsync();

            var rules = new List<VariantRule>();
            foreach (var marker in markers)
            {
                if (VariantPattern.TryParse(marker.Notation, out var pattern, out string error))
                {
                    rules.Add(new VariantRule(marker.Id, marker.Name, pattern, marker.Dataset));
                }
                else
                {
                    Console.WriteLine($"Маркер «{marker.Name}» пропущено: {error}");
                }
            }
            return rules;
        }

        private static async Task UpdateRunAsync(Guid runId, Action<AnalysisRunEntity> update)
        {
            using var db = new PandemicDbContext();
            var run = await db.Runs.FindAsync(runId);
            if (run == null) return;

            update(run);
            await db.SaveChangesAsync();
            await Changes.NotifyAsync(ChangeTopics.Runs, runId);
        }
    }
}
