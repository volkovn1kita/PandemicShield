using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using PandemicShield.Contracts.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PandemicShield.Api.Events
{
    /// <summary>
    /// SignalR-хаб /hubs/events: застосунок отримує подію «changed» щоразу, коли щось змінилося,
    /// і перечитує лише відповідні дані замість опитування кожні 3 секунди.
    /// </summary>
    public sealed class EventsHub : Hub
    {
    }

    /// <summary>
    /// Пересилає події з fanout-обмінника RabbitMQ (їх публікують API й усі воркери) клієнтам SignalR.
    /// Черга тимчасова й ексклюзивна: події потрібні лише тим, хто зараз підключений.
    /// </summary>
    public sealed class EventRelay : BackgroundService
    {
        private readonly IHubContext<EventsHub> _hub;
        private readonly ILogger<EventRelay> _logger;

        public EventRelay(IHubContext<EventsHub> hub, ILogger<EventRelay> logger)
        {
            _hub = hub;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory { HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost" };

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                    await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                    await channel.ExchangeDeclareAsync(ChangeNotifier.Exchange, ExchangeType.Fanout, durable: false, autoDelete: false,
                        cancellationToken: stoppingToken);
                    var queue = await channel.QueueDeclareAsync(string.Empty, durable: false, exclusive: true, autoDelete: true,
                        cancellationToken: stoppingToken);
                    await channel.QueueBindAsync(queue.QueueName, ChangeNotifier.Exchange, string.Empty, cancellationToken: stoppingToken);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (_, ea) =>
                    {
                        var change = JsonSerializer.Deserialize<ChangeEvent>(ea.Body.Span);
                        if (change != null) await _hub.Clients.All.SendAsync("changed", change, stoppingToken);
                    };
                    await channel.BasicConsumeAsync(queue.QueueName, autoAck: true, consumer: consumer, cancellationToken: stoppingToken);
                    _logger.LogInformation("Події змін пересилаються в SignalR (/hubs/events).");

                    while (connection.IsOpen && !stoppingToken.IsCancellationRequested)
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("RabbitMQ недоступний для подій, повтор за 5 с: {Message}", ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
    }
}
