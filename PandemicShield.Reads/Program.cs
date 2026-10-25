using PandemicShield.Contracts;
using PandemicShield.Contracts.Events;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace PandemicShield.Reads
{
    /// <summary>
    /// Воркер Reads: бере пакет FASTQ з черги reads_jobs, робить з прочитань FASTA (консенсус вірусу або збірку бактерії)
    /// і віддає її API через import_jobs — далі це звичайний запуск з Nextclade або AMRFinderPlus і MLST.
    /// </summary>
    internal class Program
    {
        private static readonly ChangeNotifier Changes = new();

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var pipeline = ReadsPipeline.FromEnvironment();
            string dataDir = Environment.GetEnvironmentVariable("DATA_DIR") ?? "/data";
            string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

            var factory = new ConnectionFactory() { HostName = rabbitHost };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(queue: "reads_jobs", durable: true, exclusive: false, autoDelete: false);
            await channel.QueueDeclareAsync(queue: "import_jobs", durable: true, exclusive: false, autoDelete: false);
            // Збирання бактерії займає хвилини й весь процесор: по одному зразку за раз.
            await channel.BasicQosAsync(prefetchCount: 1, prefetchSize: 0, global: false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                // Підтверджуємо одразу: перерване збирання позначається як невдале, а не повторюється безкінечно.
                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                ReadsJobMessage? job = JsonSerializer.Deserialize<ReadsJobMessage>(ea.Body.Span);
                if (job == null) return;

                var result = await ProcessAsync(job, pipeline, dataDir);
                if (result != null)
                {
                    await channel.BasicPublishAsync(exchange: "", routingKey: "import_jobs", mandatory: false,
                        basicProperties: new BasicProperties { Persistent = true }, body: JsonSerializer.SerializeToUtf8Bytes(result));
                }
            };

            await channel.BasicConsumeAsync(queue: "reads_jobs", autoAck: false, consumer: consumer);

            Console.WriteLine("Воркер Reads (minimap2, samtools, fastp, SKESA, Flye) чекає на завдання...");
            Thread.Sleep(Timeout.Infinite);
        }

        private static async Task<ImportJobMessage?> ProcessAsync(ReadsJobMessage job, ReadsPipeline pipeline, string dataDir)
        {
            ImportBatchEntity? batch;
            using (var db = new PandemicDbContext())
                batch = await db.Imports.FindAsync(job.ImportId);
            if (batch == null || batch.Status != ImportStatus.Queued) return null;

            string importDir = Path.Combine(dataDir, "imports", batch.Id.ToString());
            var files = Directory.EnumerateFiles(Path.Combine(importDir, "reads")).Order(StringComparer.Ordinal).ToList();
            string sampleName = ReadsNaming.SampleName(files);
            Console.WriteLine($"Пакет {batch.Id}: {batch.Platform}, {files.Count} файл(и), зразок {sampleName}");

            await UpdateAsync(batch.Id, b => b.MarkRunning(1));
            try
            {
                var result = await pipeline.RunAsync(files, batch.Platform ?? "illumina", batch.Category == ThreatCategory.Bacteria,
                    batch.NextcladeDataset, sampleName, Path.Combine(importDir, "work"),
                    message => UpdateAsync(batch.Id, b => b.MarkProgress(0, 0, message)));

                await UpdateAsync(batch.Id, b => b.MarkProgress(0, 0, "Передаю результат на аналіз…"));
                Console.WriteLine($"Пакет {batch.Id}: {result.Summary}");
                return new ImportJobMessage
                {
                    ImportId = batch.Id,
                    FastaPath = result.FastaPath,
                    Dataset = result.Dataset,
                    RunName = $"{sampleName} (FASTQ)",
                    ReadsSummary = result.Summary
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Пакет {batch.Id}: помилка: {ex.Message}");
                await UpdateAsync(batch.Id, b =>
                {
                    b.MarkProgress(0, 1, null);
                    b.MarkFailed(ex.Message);
                });
                return null;
            }
        }

        private static async Task UpdateAsync(Guid id, Action<ImportBatchEntity> update)
        {
            using (var db = new PandemicDbContext())
            {
                var batch = await db.Imports.FindAsync(id);
                if (batch == null) return;
                update(batch);
                await db.SaveChangesAsync();
            }
            await Changes.NotifyAsync(ChangeTopics.Imports);
        }
    }
}
