using PandemicShield.Contracts;
using RabbitMQ.Client;
using System.Text.Json;

namespace PandemicShield.Api.Extensions
{
    /// <summary>Публікує завдання в чергу nextclade_jobs (стійка черга: завдання переживають перезапуск RabbitMQ).</summary>
    public sealed class NextcladeJobPublisher : IAsyncDisposable
    {
        public const string QueueName = "nextclade_jobs";

        private readonly SemaphoreSlim _lock = new(1, 1);
        private IConnection? _connection;
        private IChannel? _channel;

        public async Task PublishAsync(NextcladeJobMessage job)
        {
            await _lock.WaitAsync();
            try
            {
                if (_channel is null || !_channel.IsOpen)
                {
                    string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
                    _connection = await new ConnectionFactory() { HostName = rabbitHost }.CreateConnectionAsync();
                    _channel = await _connection.CreateChannelAsync();
                    await _channel.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false, autoDelete: false);
                }

                await _channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: QueueName,
                    mandatory: false,
                    basicProperties: new BasicProperties { Persistent = true },
                    body: JsonSerializer.SerializeToUtf8Bytes(job));
            }
            finally
            {
                _lock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel is not null) await _channel.DisposeAsync();
            if (_connection is not null) await _connection.DisposeAsync();
        }
    }
}
