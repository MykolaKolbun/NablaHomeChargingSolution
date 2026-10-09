using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace EVHomeAPI.Ocpp;

public interface IOcppCommandPublisher
{
    Task RemoteStartAsync(RemoteStartCommand cmd, CancellationToken ct = default);
    Task RemoteStopAsync(RemoteStopCommand cmd, CancellationToken ct = default);
    Task AuthorizeResponseAsync(AuthorizeResponseCommand cmd, CancellationToken ct = default);
    Task RequestStatusAsync(StatusRequestCommand cmd, CancellationToken ct = default);
    Task SetChargingLimitAsync(ChargingLimitCommand cmd, CancellationToken ct = default);
    Task ClearChargingLimitAsync(ChargingClearCommand cmd, CancellationToken ct = default);
    Task DevCallAsync(DevCallCommand cmd, CancellationToken ct = default);
}

/// <summary>Publishes commands to EVOCPP via exchange ocpp.commands.</summary>
public sealed class RabbitMqCommandPublisher(RabbitMqConnection rabbit, ILogger<RabbitMqCommandPublisher> logger)
    : IOcppCommandPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);   // IChannel is not thread-safe for publishing
    private IChannel? _channel;

    public Task RemoteStartAsync(RemoteStartCommand cmd, CancellationToken ct = default) =>
        PublishAsync(OcppRoutingKeys.RemoteStart, cmd, ct);

    public Task RemoteStopAsync(RemoteStopCommand cmd, CancellationToken ct = default) =>
        PublishAsync(OcppRoutingKeys.RemoteStop, cmd, ct);

    public Task AuthorizeResponseAsync(AuthorizeResponseCommand cmd, CancellationToken ct = default) =>
        PublishAsync(OcppRoutingKeys.AuthorizeResponse, cmd, ct);

    public Task RequestStatusAsync(StatusRequestCommand cmd, CancellationToken ct = default) =>
        PublishAsync(OcppRoutingKeys.StatusRequest, cmd, ct);

    public Task SetChargingLimitAsync(ChargingLimitCommand cmd, CancellationToken ct = default) =>
        PublishAsync(OcppRoutingKeys.ChargingLimit, cmd, ct);

    public Task ClearChargingLimitAsync(ChargingClearCommand cmd, CancellationToken ct = default) =>
        PublishAsync(OcppRoutingKeys.ChargingClear, cmd, ct);

    public Task DevCallAsync(DevCallCommand cmd, CancellationToken ct = default) =>
        PublishAsync(OcppRoutingKeys.DevCall, cmd, ct);

    private async Task PublishAsync<T>(string routingKey, T payload, CancellationToken ct)
    {
        // Default options = PascalCase, which EVOCPP's case-sensitive deserializer expects.
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

        await _lock.WaitAsync(ct);
        try
        {
            if (_channel is not { IsOpen: true })
            {
                var connection = await rabbit.GetAsync(ct);
                _channel = await connection.CreateChannelAsync(cancellationToken: ct);
                await _channel.ExchangeDeclareAsync(RabbitMqConnection.CommandsExchange, ExchangeType.Topic,
                    durable: true, autoDelete: false, cancellationToken: ct);
            }

            await _channel.BasicPublishAsync(RabbitMqConnection.CommandsExchange, routingKey,
                mandatory: false, basicProperties: new BasicProperties { ContentType = "application/json" },
                body: body, cancellationToken: ct);
            logger.LogInformation("[→] {RoutingKey} {Body}", routingKey, Encoding.UTF8.GetString(body));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        _lock.Dispose();
    }
}
