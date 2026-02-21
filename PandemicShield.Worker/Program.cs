using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using PandemicShield.Contracts;
using System.Text.Json;
using PandemicShield.Worker.Services;
using PandemicShield.Worker.Data;

namespace PandemicShield.Worker
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

            var factory = new ConnectionFactory() { HostName = rabbitHost };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                        queue: "dna_chunks",
                        durable: false,
                        exclusive: false,
                        autoDelete: false
            );

            await channel.BasicQosAsync(prefetchCount: 1, prefetchSize: 0, global: false);
            
            var consumer = new AsyncEventingBasicConsumer(channel);

            byte[] targetMutationBytes = Encoding.UTF8.GetBytes("CCCC");

            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                string dnaChunk = Encoding.UTF8.GetString(body);

                DnaChunkMessage? dnaChunkMessage = JsonSerializer.Deserialize<DnaChunkMessage>(dnaChunk);
                if (dnaChunkMessage == null)
                {
                    throw new Exception("Cannot convert chunk");
                }

                var foundProteins = TranslationService.FindProteins(dnaChunkMessage.Sequence, dnaChunkMessage.StartPosition);

                foreach (ProteinData protein in foundProteins)
                {
                    List<ThreatReport> reports = MutationScannerService.ScanProtein(protein);
                    foreach (ThreatReport report in reports)
                    {
                        Console.WriteLine($"Назва - {report.ThreatName}\nПослідовність - {report.ProteinSequence}\nПозиція - {report.GlobalPosition}");
                    }
                }

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);

            };

            await channel.BasicConsumeAsync(queue: "dna_chunks", autoAck: false, consumer: consumer);

            Console.ReadLine();


        }
    }
}
