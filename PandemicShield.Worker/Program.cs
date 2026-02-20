using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using PandemicShield.Contracts;
using System.Text.Json;
using PandemicShield.Worker.Services;

namespace PandemicShield.Worker
{
    public static class BiologyDictionary
    {
        // Словник трансляції кодонів ДНК в амінокислоти
        public static readonly Dictionary<string, char> CodonTable = new Dictionary<string, char>
    {
        {"ATT", 'I'}, {"ATC", 'I'}, {"ATA", 'I'}, // Isoleucine
        {"CTT", 'L'}, {"CTC", 'L'}, {"CTA", 'L'}, {"CTG", 'L'}, {"TTA", 'L'}, {"TTG", 'L'}, // Leucine
        {"GTT", 'V'}, {"GTC", 'V'}, {"GTA", 'V'}, {"GTG", 'V'}, // Valine
        {"TTT", 'F'}, {"TTC", 'F'}, // Phenylalanine
        {"ATG", 'M'}, // Methionine (START)
        {"TGT", 'C'}, {"TGC", 'C'}, // Cysteine
        {"GCT", 'A'}, {"GCC", 'A'}, {"GCA", 'A'}, {"GCG", 'A'}, // Alanine
        {"GGT", 'G'}, {"GGC", 'G'}, {"GGA", 'G'}, {"GGG", 'G'}, // Glycine
        {"CCT", 'P'}, {"CCC", 'P'}, {"CCA", 'P'}, {"CCG", 'P'}, // Proline
        {"ACT", 'T'}, {"ACC", 'T'}, {"ACA", 'T'}, {"ACG", 'T'}, // Threonine
        {"TCT", 'S'}, {"TCC", 'S'}, {"TCA", 'S'}, {"TCG", 'S'}, {"AGT", 'S'}, {"AGC", 'S'}, // Serine
        {"TAT", 'Y'}, {"TAC", 'Y'}, // Tyrosine
        {"TGG", 'W'}, // Tryptophan
        {"CAA", 'Q'}, {"CAG", 'Q'}, // Glutamine
        {"AAT", 'N'}, {"AAC", 'N'}, // Asparagine
        {"CAT", 'H'}, {"CAC", 'H'}, // Histidine
        {"GAA", 'E'}, {"GAG", 'E'}, // Glutamic acid
        {"GAT", 'D'}, {"GAC", 'D'}, // Aspartic acid
        {"AAA", 'K'}, {"AAG", 'K'}, // Lysine
        {"CGT", 'R'}, {"CGC", 'R'}, {"CGA", 'R'}, {"CGG", 'R'}, {"AGA", 'R'}, {"AGG", 'R'}, // Arginine
        {"TAA", '*'}, {"TAG", '*'}, {"TGA", '*'}  // STOP кодони
    };
    }
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

                var foundProteins = TranslationService.FindProteins(dnaChunkMessage.Sequence);

                foreach (string protein in foundProteins)
                {
                    Console.WriteLine($"Protein {protein}");
                }

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);

            };

            await channel.BasicConsumeAsync(queue: "dna_chunks", autoAck: false, consumer: consumer);

            Console.ReadLine();


        }
    }
}
