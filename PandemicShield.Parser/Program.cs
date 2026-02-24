using RabbitMQ.Client;
using System.Diagnostics;
using System.IO;
using System.Text;
using PandemicShield.Contracts;
using System.Text.Json;

namespace PandemicShield.Parser
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

            ConnectionFactory connectionFactory = new ConnectionFactory() { HostName = rabbitHost };


            string filePath = "sequence.fasta";
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
                int overlap = 60;
                int chunkSize = 60000;

                int totalCharactersRead = 0;

                Stopwatch sw = new Stopwatch();
                sw.Start();

                while ((currentLine = await sr.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(currentLine)) continue;
                    if (currentLine[0] == '>') continue;

                    buffer.Append(currentLine);

                    if (buffer.Length >= chunkSize)
                    {
                        DnaChunkMessage chunkMessage = new DnaChunkMessage()
                        {

                            Sequence = buffer.ToString(0, chunkSize),
                            StartPosition = totalCharactersRead,
                            Category = ThreatCategory.Virus
                        };

                        var jsonToSend = JsonSerializer.Serialize(chunkMessage);
                        var body = Encoding.UTF8.GetBytes(jsonToSend);

                        await channel.BasicPublishAsync(
                            exchange: "",
                            routingKey: "dna_chunks",
                            body: body
                        );

                        buffer.Remove(0, chunkSize - overlap);
                        totalCharactersRead += chunkSize - overlap;

                    }

                }
                if (buffer.Length > 0)
                {
                    DnaChunkMessage chunkMessage = new DnaChunkMessage()
                    {
                        Sequence = buffer.ToString(),
                        StartPosition = totalCharactersRead,
                        IsLastChunk = true
                    };

                    var jsonToSend = JsonSerializer.Serialize(chunkMessage);

                    var body = Encoding.UTF8.GetBytes(jsonToSend);
                    await channel.BasicPublishAsync(
                        exchange: "",
                        routingKey: "dna_chunks",
                        body: body
                    );
                    
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
