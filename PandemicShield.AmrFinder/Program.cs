using Microsoft.EntityFrameworkCore;
using PandemicShield.AmrFinder.Report;
using PandemicShield.AmrFinder.Rules;
using PandemicShield.Bio.Variants;
using PandemicShield.Contracts;
using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace PandemicShield.AmrFinder
{
    /// <summary>
    /// Воркер AMRFinderPlus: бере завдання з черги amrfinder_jobs, шукає гени й точкові мутації
    /// резистентності в бактеріальній збірці, зберігає знахідки і створює сповіщення за правилами.
    /// </summary>
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var runner = AmrFinderRunner.FromEnvironment();
            string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

            var factory = new ConnectionFactory() { HostName = rabbitHost };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(queue: "amrfinder_jobs", durable: true, exclusive: false, autoDelete: false);
            await channel.BasicQosAsync(prefetchCount: 1, prefetchSize: 0, global: false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                AmrJobMessage? job = JsonSerializer.Deserialize<AmrJobMessage>(ea.Body.Span);
                if (job != null)
                {
                    await ProcessJobAsync(job, runner);
                }

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            };

            await channel.BasicConsumeAsync(queue: "amrfinder_jobs", autoAck: false, consumer: consumer);

            Console.WriteLine("Воркер AMRFinderPlus чекає на завдання...");
            Thread.Sleep(Timeout.Infinite);
        }

        private static async Task ProcessJobAsync(AmrJobMessage job, AmrFinderRunner runner)
        {
            Console.WriteLine($"Запуск {job.RunId}: AMRFinderPlus ({job.Organism ?? "без виду"}) для {job.FilePath}");
            await UpdateRunAsync(job.RunId, run => run.MarkAmrRunning());

            try
            {
                string outputDir = Path.Combine(Path.GetDirectoryName(job.FilePath)!, "amrfinder");
                string reportPath = await runner.RunAsync(job.FilePath, job.Organism, outputDir);
                string? databaseVersion = await runner.DatabaseVersionAsync();

                List<AmrHit> hits;
                using (var reader = new StreamReader(reportPath))
                {
                    hits = AmrReportReader.Read(reader);
                }

                int alerts = await SaveAsync(job.RunId, hits);
                int amrGenes = hits.Count(h => h.Type == "AMR");
                string message = $"Знахідок: {hits.Count} (резистентність: {amrGenes}), сповіщень: {alerts}";

                await UpdateRunAsync(job.RunId, run => run.MarkAmrCompleted(databaseVersion, message));
                Console.WriteLine($"Запуск {job.RunId}: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Запуск {job.RunId}: помилка AMRFinderPlus: {ex.Message}");
                await UpdateRunAsync(job.RunId, run => run.MarkAmrFailed(ex.Message));
            }
        }

        private static async Task<int> SaveAsync(Guid runId, List<AmrHit> hits)
        {
            using var db = new PandemicDbContext();

            var rules = new List<AmrRule>();
            foreach (var marker in await db.VariantMarkers.AsNoTracking().ToListAsync())
            {
                if (VariantPattern.TryParse(marker.Notation, out var pattern, out _) && pattern.IsAmr)
                {
                    rules.Add(new AmrRule(marker.Id, marker.Name, pattern));
                }
            }

            var entities = hits.Select(h => new AmrHitEntity(
                runId, h.ContigId, h.Start, h.Stop, h.Strand, h.ElementSymbol, h.ElementName, h.Scope, h.Type, h.Subtype,
                h.Class, h.Subclass, h.Method, h.CoveragePercent, h.IdentityPercent, h.ClosestReference)).ToList();
            db.AmrHits.AddRange(entities);

            int alerts = 0;
            foreach (AmrMatch match in AmrRuleMatcher.Match(hits, rules))
            {
                var entity = entities[hits.IndexOf(match.Hit)];
                db.Threats.Add(ThreatAlertEntity.FromAmrFinder(runId, match.Rule.Name, entity));
                alerts++;
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
