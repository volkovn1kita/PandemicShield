using Microsoft.EntityFrameworkCore;
using PandemicShield.Bio.Variants;
using PandemicShield.Contracts;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace PandemicShield.Mlst
{
    /// <summary>
    /// Воркер MLST: бере завдання з черги mlst_jobs, визначає тип штаму (ST) бактеріальної збірки і створює
    /// сповіщення за правилами <c>st:…</c> (наприклад, епідемічний клон K. pneumoniae ST147).
    /// Ген показує, «що» є в бактерії; ST — «чий це штам»: той самий ST у різних шпиталях може означати спалах.
    /// </summary>
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var runner = MlstRunner.FromEnvironment();
            string? version = await runner.VersionAsync();
            string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

            var factory = new ConnectionFactory() { HostName = rabbitHost };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(queue: "mlst_jobs", durable: true, exclusive: false, autoDelete: false);
            await channel.BasicQosAsync(prefetchCount: 1, prefetchSize: 0, global: false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                MlstJobMessage? job = JsonSerializer.Deserialize<MlstJobMessage>(ea.Body.Span);
                if (job != null)
                {
                    await ProcessJobAsync(job, runner, version);
                }

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            };

            await channel.BasicConsumeAsync(queue: "mlst_jobs", autoAck: false, consumer: consumer);

            Console.WriteLine($"Воркер MLST {version} чекає на завдання...");
            Thread.Sleep(Timeout.Infinite);
        }

        private static async Task ProcessJobAsync(MlstJobMessage job, MlstRunner runner, string? version)
        {
            Console.WriteLine($"Запуск {job.RunId}: MLST ({job.Organism ?? "схема автоматично"}) для {job.FilePath}");
            await UpdateRunAsync(job.RunId, run => run.MarkMlstRunning());

            try
            {
                string outputDir = Path.Combine(Path.GetDirectoryName(job.FilePath)!, "mlst");
                MlstResult result = await runner.RunAsync(job.FilePath, job.Organism, outputDir);
                string message = MlstReport.Describe(result);
                int alerts = await SaveAlertsAsync(job.RunId, result);

                await UpdateRunAsync(job.RunId, run => run.MarkMlstCompleted(
                    result.NoScheme ? null : result.Scheme,
                    result.SequenceType,
                    result.Alleles.Count == 0 ? null : result.AlleleProfile,
                    version,
                    alerts > 0 ? $"{message}, сповіщень: {alerts}" : message));
                Console.WriteLine($"Запуск {job.RunId}: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Запуск {job.RunId}: помилка MLST: {ex.Message}");
                await UpdateRunAsync(job.RunId, run => run.MarkMlstFailed(ex.Message));
            }
        }

        private static async Task<int> SaveAlertsAsync(Guid runId, MlstResult result)
        {
            if (!result.HasSequenceType) return 0;

            using var db = new PandemicDbContext();
            int alerts = 0;
            foreach (var marker in await db.VariantMarkers.AsNoTracking().ToListAsync())
            {
                if (VariantPattern.TryParse(marker.Notation, out var pattern, out _) && pattern.MatchesSequenceType(result.Scheme, result.SequenceType))
                {
                    db.Threats.Add(ThreatAlertEntity.FromMlst(runId, marker.Name, result.Scheme, result.SequenceType));
                    alerts++;
                }
            }
            await db.SaveChangesAsync();
            return alerts;
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
