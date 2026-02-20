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

            var factory = new ConnectionFactory() { HostName = "localhost"};
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                        queue: "dna_chunks",
                        durable: false,
                        exclusive: false,
                        autoDelete: false
                );

            var consumer = new AsyncEventingBasicConsumer(channel);

            string targetMutation = "CCCC";

            consumer.ReceivedAsync += async (model, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                if (message.Contains(targetMutation))
                {
                    Console.WriteLine("УВАГА: Знайдено збіг мутації!");
                }

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);

            };

            await channel.BasicConsumeAsync(queue: "dna_chunks", autoAck: false, consumer: consumer);

            Console.ReadLine();


        }
    }
}
