using RabbitMQ.Client;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PandemicShield.Parser
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

            ConnectionFactory connectionFactory = new ConnectionFactory() { HostName = rabbitHost };


            string filePath = "hg38.fasta";
            try
            {
                using var connection = await connectionFactory.CreateConnectionAsync();
                using var channel = await connection.CreateChannelAsync();
                await channel.QueueDeclareAsync(
                        queue: "dna_chunks",
                        durable: false,
                        exclusive: false,
                        autoDelete: false
                    );

                using StreamReader sr = new StreamReader(filePath);
                string? currentLine;
                StringBuilder buffer = new StringBuilder();
                int overlap = 3;
                int chunkSize = 1000;

                Stopwatch sw = new Stopwatch();
                sw.Start();

                while ((currentLine = await sr.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(currentLine)) continue;
                    if (currentLine[0] == '>') continue;

                    buffer.Append(currentLine);

                    if (buffer.Length >= chunkSize)
                    {
                        string chunkToSend = buffer.ToString(0, chunkSize);
                        var body = Encoding.UTF8.GetBytes(chunkToSend);

                        await channel.BasicPublishAsync(
                            exchange: "",
                            routingKey: "dna_chunks",
                            body: body
                        );

                        buffer.Remove(0, chunkSize - overlap);

                        Console.WriteLine($"Відправлено шматок геному довжиною {chunkToSend.Length} символів");
                    }

                }
                if (buffer.Length > 0)
                {
                    string chunkToSend = buffer.ToString();
                    var body = Encoding.UTF8.GetBytes(chunkToSend);
                    await channel.BasicPublishAsync(
                        exchange: "",
                        routingKey: "dna_chunks",
                        body: body
                    );
                    Console.WriteLine($"Відправлено останній шматок геному довжиною {chunkToSend.Length} символів");
                }

                sw.Stop();
                Console.WriteLine($"[ПАРСЕР] Файл оброблено і відправлено за {sw.ElapsedMilliseconds} мс");

            }
            catch (IOException e)
            {
                Console.WriteLine("File could not be read: ");
                Console.WriteLine(e.Message);
            }

            Console.ReadLine();
        }
    }
}
