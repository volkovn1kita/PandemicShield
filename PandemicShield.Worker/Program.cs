using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using PandemicShield.Contracts;
using System.Text.Json;
using PandemicShield.Worker.Services;
using PandemicShield.Worker.Data;
using System.Net.Http.Json;

namespace PandemicShield.Worker
{
    internal class Program
    {
        private static readonly HttpClient client = new HttpClient();
      
        private static async Task<List<DiseaseMarker>> GetDiseaseAsync()
        {
            string apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5020";
            HttpResponseMessage response = await client.GetAsync($"{apiUrl}/api/dictionary");

            response.EnsureSuccessStatusCode();

            var dictionary = await response.Content.ReadFromJsonAsync<List<DiseaseMarker>>();
            return dictionary ?? new List<DiseaseMarker>();
        }

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var diseases = await GetDiseaseAsync();

            string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

            var factory = new ConnectionFactory() { HostName = rabbitHost };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "dna_chunks",
                durable: false,
                exclusive: false,
                autoDelete: false);

            await channel.QueueDeclareAsync(
                queue: "threat_alerts",
                durable: true,
                autoDelete: false,
                exclusive: false);

            await channel.BasicQosAsync(prefetchCount: 1, prefetchSize: 0, global: false);
            
            var consumer = new AsyncEventingBasicConsumer(channel);

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
                var relevantDiseases = diseases.Where(d => d.Category == dnaChunkMessage.Category).ToList();
                foreach (ProteinData protein in foundProteins)
                {
                    List<ThreatReport> reports = MutationScannerService.ScanProtein(protein, relevantDiseases);
                    
                    if (reports != null)
                    {
                        foreach (ThreatReport report in reports)
                        {
                            var json = JsonSerializer.Serialize(report);
                            byte[] reportBody = Encoding.UTF8.GetBytes(json);

                            await channel.BasicPublishAsync(
                                exchange: "",
                                routingKey: "threat_alerts",
                                body: reportBody);
                        }
                    }
                }

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);

            };

            await channel.BasicConsumeAsync(queue: "dna_chunks", autoAck: false, consumer: consumer);

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
