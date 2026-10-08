using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EVHomeAPI.Ocpp;

/// <summary>
/// Consumes all EVOCPP events from ONE durable queue (evhome.events ← charger.#) with
/// prefetch 1, so events are applied strictly in publish order. Each message is handled
/// in its own DI scope by <see cref="OcppEventProcessor"/>.
///
/// A message that throws is logged and acked (not requeued): a poison message must not
/// block the queue; the next status/meter event re-synchronizes state.
/// </summary>
public sealed class OcppEventConsumer(
    RabbitMqConnection rabbit,
    IServiceScopeFactory scopes,
    ILogger<OcppEventConsumer> logger) : BackgroundService
{
    public const string Queue = "evhome.events";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            var connection = await rabbit.GetAsync(ct);
            var channel    = await connection.CreateChannelAsync(cancellationToken: ct);

            await channel.ExchangeDeclareAsync(RabbitMqConnection.EventsExchange, ExchangeType.Topic,
                durable: true, autoDelete: false, cancellationToken: ct);
            await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
            await channel.QueueBindAsync(Queue, RabbitMqConnection.EventsExchange, "charger.#", cancellationToken: ct);
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: ct);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                var json = Encoding.UTF8.GetString(ea.Body.Span);
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<OcppEventProcessor>()
                        .HandleAsync(ea.RoutingKey, json, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to handle {RoutingKey}: {Json}", ea.RoutingKey, json);
                }
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
            };

            await channel.BasicConsumeAsync(Queue, autoAck: false, consumer: consumer, cancellationToken: ct);
            logger.LogInformation("Consuming EVOCPP events from {Queue}", Queue);

            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown
        }
    }
}
