using RabbitMQ.Client;
using System.IO;
using System.Text;

namespace PandemicShield.Parser
{
    internal class Program
    {
        static async Task Main(string[] args)
        {

            ConnectionFactory connectionFactory = new ConnectionFactory() { HostName = "localhost" };


            string filePath = "sequence.fasta";
            try
            {
                using var connection = await connectionFactory.CreateConnectionAsync();
                using var channel = await connection.CreateChannelAsync();
                {
                    await channel.QueueDeclareAsync(
                        queue: "dna_chunks",
                        durable: false,
                        exclusive: false,
                        autoDelete: false
                    );

                    using (StreamReader sr = new StreamReader(filePath))
                    {
                        string? currentLine;
                        while ((currentLine = await sr.ReadLineAsync()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(currentLine)) continue;
                            if (currentLine[0] == '>') continue;

                            var body = Encoding.UTF8.GetBytes(currentLine);

                            await channel.BasicPublishAsync(
                                exchange: "",
                                routingKey: "dna_chunks",
                                body: body
                            );

                            Console.WriteLine($"Відправлено шматок геному довжиною {currentLine.Length} символів");
                        }

                    }

                }
            }
            catch (IOException e)
            {
                Console.WriteLine("File could not be read: ");
                Console.WriteLine(e.Message);
            }
        }
    }
}
