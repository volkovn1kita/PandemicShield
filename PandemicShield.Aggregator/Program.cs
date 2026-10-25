using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using PandemicShield.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace PandemicShield.Aggregator
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
            var factory = new ConnectionFactory() {HostName = rabbitHost };

            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "threat_alerts",
                durable: true,
                exclusive: false,
                autoDelete: false);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (model, ea) => {

                ThreatReport? report = JsonSerializer.Deserialize<ThreatReport>(ea.Body.Span);

                if (report != null)
                {
                    ThreatAlertEntity alertEntity = new ThreatAlertEntity(
                        runId: report.RunId == Guid.Empty ? null : report.RunId,
                        threatName: report.ThreatName,
                        recordId: report.RecordId,
                        strand: report.Strand,
                        ntStart: report.NtStart,
                        ntEnd: report.NtEnd,
                        aaPosition: report.AaPosition,
                        proteinSequence: report.ProteinSequence,
                        category: report.Category);

                    using var dbContext = new PandemicDbContext();
                    dbContext.Threats.Add(alertEntity);
                    await dbContext.SaveChangesAsync();
                }


                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            };

            await channel.BasicConsumeAsync(queue: "threat_alerts", autoAck: false, consumer: consumer);

            Thread.Sleep(Timeout.Infinite);

        }
    }
}
