using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace PandemicShield.Worker
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhsot";

            var factory = new ConnectionFactory() { HostName = rabbitHost };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                        queue: "dna_chunks",
                        durable: false,
                        exclusive: false,
                        autoDelete: false
                );

            var consumer = new AsyncEventingBasicConsumer(channel);

            byte[] targetMutationBytes = Encoding.UTF8.GetBytes("CCCC");

            consumer.ReceivedAsync += async (model, ea) =>
            {

                ReadOnlySpan<byte> bodySpan = ea.Body.Span;

                ReadOnlySpan<byte> targetSpan = targetMutationBytes;

                if (bodySpan.IndexOf(targetSpan) >= 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[!!!] УВАГА: Знайдено збіг мутації у повідомленні");
                    Console.ResetColor();
                }

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);

            };

            await channel.BasicConsumeAsync(queue: "dna_chunks", autoAck: false, consumer: consumer);

            Console.ReadLine();


        }
    }
}
