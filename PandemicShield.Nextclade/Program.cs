using Microsoft.EntityFrameworkCore;
using PandemicShield.Bio.Variants;
using PandemicShield.Contracts;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using PandemicShield.Nextclade.Results;
using PandemicShield.Nextclade.Rules;
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
                string ndjson = await runner.RunAsync(job.FilePath, job.Dataset, outputDir);

                StoreSummary summary;
                using (var reader = new StreamReader(ndjson))
                {
                    summary = await NextcladeResultStore.SaveAsync(job.RunId, job.Dataset, runner.DatasetTag(job.Dataset),
                        NextcladeNdjsonReader.Read(reader), rules, () => new PandemicDbContext());
                }

                string message = $"Послідовностей: {summary.Sequences} (з помилкою: {summary.Failed}), " +
                                 $"мутацій: {summary.Mutations}, сповіщень: {summary.Alerts}";
                await UpdateRunAsync(job.RunId, run => run.MarkNextcladeCompleted(summary.Sequences, message));
                Console.WriteLine($"Запуск {job.RunId}: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Запуск {job.RunId}: помилка Nextclade: {ex.Message}");
                await UpdateRunAsync(job.RunId, run => run.MarkNextcladeFailed(ex.Message));
            }
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
        }
    }
}
