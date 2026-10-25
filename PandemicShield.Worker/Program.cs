using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PandemicShield.Bio;
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
        private static readonly TimeSpan DictionaryTtl = TimeSpan.FromSeconds(15);

        private static List<DiseaseMarker> diseases = new List<DiseaseMarker>();
        private static DateTime diseasesLoadedAt = DateTime.MinValue;

        private static async Task<List<DiseaseMarker>> GetDiseaseAsync()
        {
            string apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5020";
            HttpResponseMessage response = await client.GetAsync($"{apiUrl}/api/dictionary");

            response.EnsureSuccessStatusCode();

            var dictionary = await response.Content.ReadFromJsonAsync<List<DiseaseMarker>>();
            return dictionary ?? new List<DiseaseMarker>();
        }

        // Словник перечитується не частіше, ніж раз на DictionaryTtl, тож зміни через CRUD
        // починають діяти без перезапуску Worker.
        private static async Task<List<DiseaseMarker>> GetCachedDiseasesAsync()
        {
            if (DateTime.UtcNow - diseasesLoadedAt < DictionaryTtl) return diseases;

            try
            {
                diseases = await GetDiseaseAsync();
                diseasesLoadedAt = DateTime.UtcNow;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Не вдалося оновити словник маркерів, використовую попередній: {ex.Message}");
            }
            return diseases;
        }

        private static OrfFinderOptions ReadOrfOptions()
        {
            int minLength = int.TryParse(Environment.GetEnvironmentVariable("ORF_MIN_LENGTH"), out int value) ? value : 75;
            int codeId = int.TryParse(Environment.GetEnvironmentVariable("GENETIC_CODE"), out int code) ? code : 1;

            return new OrfFinderOptions
            {
                MinNucleotideLength = minLength,
                GeneticCode = GeneticCode.FromId(codeId)
            };
        }

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var orfOptions = ReadOrfOptions();
            var bacterialOrfOptions = orfOptions with
            {
                GeneticCode = GeneticCode.BacterialArchaealPlastid,
                AllowAlternativeStarts = true
            };
            await GetCachedDiseasesAsync();

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
                DnaChunkMessage? dnaChunkMessage = JsonSerializer.Deserialize<DnaChunkMessage>(ea.Body.Span);
                if (dnaChunkMessage == null)
                {
                    throw new Exception("Cannot convert chunk");
                }

                var allDiseases = await GetCachedDiseasesAsync();
                var relevantDiseases = allDiseases.Where(d => d.Category == dnaChunkMessage.Category).ToList();

                // Бактерії: таблиця коду NCBI 11 і альтернативні старт-кодони (GTG, TTG), як у прокаріотів.
                var options = dnaChunkMessage.Category == ThreatCategory.Bacteria ? bacterialOrfOptions : orfOptions;
                List<ThreatReport> reports = MutationScannerService.ScanChunk(dnaChunkMessage, relevantDiseases, options);

                foreach (ThreatReport report in reports)
                {
                    byte[] reportBody = JsonSerializer.SerializeToUtf8Bytes(report);

                    await channel.BasicPublishAsync(
                        exchange: "",
                        routingKey: "threat_alerts",
                        body: reportBody);
                }

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);

            };

            await channel.BasicConsumeAsync(queue: "dna_chunks", autoAck: false, consumer: consumer);

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
