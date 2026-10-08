
using OCPPServer.Tracing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace OCPPServer
{
    public sealed class RabbitMqConsumer : BackgroundService
    {
        private const string CommandExchange = "ocpp.commands";
        private const string CommandQueue = "ocpp.server.commands";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITracingService      _tracer;

        public RabbitMqConsumer(IServiceScopeFactory scopeFactory, ITracingService tracer)
        {
            _scopeFactory = scopeFactory;
            _tracer       = tracer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait for the publisher's connection to be ready
            IConnection? conn;
            while ((conn = RabbitMqPublisher.SharedConnection) is null)
            {
                if (stoppingToken.IsCancellationRequested) return;
                await Task.Delay(1_000, stoppingToken);
            }

            await using var channel = await conn.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

            await channel.ExchangeDeclareAsync(
                exchange: CommandExchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                queue: CommandQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await channel.QueueBindAsync(
                queue: CommandQueue,
                exchange: CommandExchange,
                routingKey: "command.#",
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IOcppCommandHandler>();
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    _tracer.Verbose("RMQ", $"[←] {ea.RoutingKey}: {json}");
                    await handler.HandleAsync(ea.RoutingKey, json, stoppingToken);
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _tracer.Exception("RMQ-Consumer", ex, $"Command '{ea.RoutingKey}' failed");
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            await channel.BasicConsumeAsync(
                queue: CommandQueue,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            try { await Task.Delay(Timeout.Infinite, stoppingToken); }
            catch (OperationCanceledException) { }
        }
    }
}
