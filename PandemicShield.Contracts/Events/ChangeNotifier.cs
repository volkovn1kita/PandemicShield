using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace PandemicShield.Contracts.Events
{
    /// <summary>Що змінилося: застосунок перечитує лише відповідні дані.</summary>
    public static class ChangeTopics
    {
        public const string Runs = "runs";
        public const string Alerts = "alerts";
        public const string Imports = "imports";
        public const string Surveillance = "surveillance";
        public const string Rules = "rules";
    }

    /// <summary>Подія зміни: тема і, якщо стосується одного запуску, його Id.</summary>
    public sealed record ChangeEvent(string Topic, Guid? RunId = null);

    /// <summary>
    /// Повідомляє застосунок про зміни замість того, щоб він опитував API кожні 3 секунди.
    /// Кожен сервіс після запису в БД публікує подію в fanout-обмінник RabbitMQ <see cref="Exchange"/>;
    /// API пересилає їх у SignalR-хаб /hubs/events.
    /// <para>
    /// Агрегатор зберігає сповіщення ORF по одному, тож одна й та сама подія може йти сотнями за секунду:
    /// повтори тієї ж теми й запуску протягом <see cref="Throttle"/> відкидаються, але остання завжди доходить.
    /// </para>
    /// <para>Помилки публікації не ламають аналіз: подія — це лише підказка оновити екран.</para>
    /// </summary>
    public sealed class ChangeNotifier : IAsyncDisposable
    {
        public const string Exchange = "pandemic_events";
        public static readonly TimeSpan Throttle = TimeSpan.FromMilliseconds(500);

        private readonly ConnectionFactory _factory;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ConcurrentDictionary<ChangeEvent, DateTime> _lastSent = new();
        private readonly ConcurrentDictionary<ChangeEvent, bool> _pending = new();
        private IConnection? _connection;
        private IChannel? _channel;

        public ChangeNotifier(string? rabbitHost = null)
        {
            _factory = new ConnectionFactory { HostName = rabbitHost ?? Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost" };
        }

        public Task NotifyAsync(string topic, Guid? runId = null) => NotifyAsync(new ChangeEvent(topic, runId));

        public async Task NotifyAsync(ChangeEvent change)
        {
            var now = DateTime.UtcNow;
            if (_lastSent.TryGetValue(change, out var last) && now - last < Throttle)
            {
                // Надто часто: відкладаємо одну «хвостову» подію, щоб остання зміна теж дійшла.
                if (_pending.TryAdd(change, true))
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(Throttle);
                        _pending.TryRemove(change, out _);
                        await PublishAsync(change);
                    });
                }
                return;
            }
            await PublishAsync(change);
        }

        private async Task PublishAsync(ChangeEvent change)
        {
            _lastSent[change] = DateTime.UtcNow;
            await _lock.WaitAsync();
            try
            {
                if (_channel is null || !_channel.IsOpen)
                {
                    _connection = await _factory.CreateConnectionAsync();
                    _channel = await _connection.CreateChannelAsync();
                    await _channel.ExchangeDeclareAsync(Exchange, ExchangeType.Fanout, durable: false, autoDelete: false);
                }
                await _channel.BasicPublishAsync(Exchange, string.Empty, JsonSerializer.SerializeToUtf8Bytes(change));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Подію {change.Topic} не надіслано: {ex.Message}");
                _channel = null;
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
