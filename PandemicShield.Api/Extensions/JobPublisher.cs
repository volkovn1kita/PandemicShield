using RabbitMQ.Client;
using System.Text.Json;

namespace PandemicShield.Api.Extensions
{
    /// <summary>
    /// Публікує завдання для воркерів зовнішніх інструментів у стійкі черги (переживають перезапуск RabbitMQ).
    /// </summary>
    public sealed class JobPublisher : IAsyncDisposable
    {
        public const string NextcladeQueue = "nextclade_jobs";
        public const string AmrFinderQueue = "amrfinder_jobs";
        public const string MlstQueue = "mlst_jobs";
        public const string ImportQueue = "import_jobs";
        public const string SurveillanceQueue = "surveillance_jobs";

        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly HashSet<string> _declared = new();
        private IConnection? _connection;
        private IChannel? _channel;

        public async Task PublishAsync<T>(string queue, T job)
        {
            await _lock.WaitAsync();
            try
            {
                if (_channel is null || !_channel.IsOpen)
                {
                    string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
                    _connection = await new ConnectionFactory() { HostName = rabbitHost }.CreateConnectionAsync();
                    _channel = await _connection.CreateChannelAsync();
                    _declared.Clear();
                }

                if (_declared.Add(queue))
                {
                    await _channel.QueueDeclareAsync(queue: queue, durable: true, exclusive: false, autoDelete: false);
                }

                await _channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: queue,
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
