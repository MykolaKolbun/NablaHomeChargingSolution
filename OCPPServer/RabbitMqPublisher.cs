/**
 * RabbitMqPublisher.cs — OCPP event publisher for OCPPServer
 *
 * Publishes charger lifecycle events to the "ocpp.events" topic exchange.
 * EVChargingApi subscribes to these events via its RabbitMqConsumerService.
 *
 * Call ConfigureAsync() once at startup from Program.cs.
 * All Publish* methods are safe to call fire-and-forget (exceptions are caught
 * and logged via OcppLog rather than thrown).
 *
 * Exchange: ocpp.events (topic, durable)
 * Routing keys:
 *   charger.status.changed       — StatusNotification received or charger disconnected
 *   charger.transaction.started  — StartTransaction confirmed by the charger
 *   charger.transaction.stopped  — StopTransaction confirmed by the charger
 *   charger.meter.updated        — MeterValues received; carries live energy + power
 */

using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace OCPPServer;

public static class RabbitMqPublisher
{
    private const string Exchange = "ocpp.events";

    private static IConnection? _connection;
    private static IChannel?    _channel;

    // ── Startup ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Connects to RabbitMQ and declares the durable topic exchange.
    /// Called once from Program.cs after database migrations.
    /// Failures are logged but non-fatal — the server runs without event publishing.
    /// </summary>
    public static async Task ConfigureAsync(string host, string username, string password)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName                 = host,
                UserName                 = username,
                Password                 = password,
                AutomaticRecoveryEnabled = true,   // reconnect after broker restart
            };
            _connection = await factory.CreateConnectionAsync();
            _channel    = await _connection.CreateChannelAsync();

            // Declare the exchange (idempotent — safe to call on every restart)
            await _channel.ExchangeDeclareAsync(
                exchange:   Exchange,
                type:       ExchangeType.Topic,
                durable:    true,       // survives broker restart
                autoDelete: false);

            OcppLog.Write($"[RABBIT    ] Connected to '{host}', exchange '{Exchange}' ready.");
        }
        catch (Exception ex)
        {
            OcppLog.Write($"[RABBIT ERR] Cannot connect to RabbitMQ at '{host}': {ex.Message}. " +
                          "Events will not be published.");
        }
    }

    // ── Internal publish helper ────────────────────────────────────────────────

    private static async Task PublishAsync(string routingKey, object payload)
    {
        if (_channel is null) return;   // RabbitMQ not configured — silently skip
        try
        {
            var body  = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            var props = new BasicProperties
            {
                ContentType  = "application/json",
                DeliveryMode = DeliveryModes.Persistent,  // survive broker restart
            };
            await _channel.BasicPublishAsync(
                exchange:        Exchange,
                routingKey:      routingKey,
                mandatory:       false,
                basicProperties: props,
                body:            body);

            OcppLog.Write($"[RABBIT → ] {routingKey}");
        }
        catch (Exception ex)
        {
            OcppLog.Write($"[RABBIT ERR] Failed to publish '{routingKey}': {ex.Message}");
        }
    }

    // ── Public publish methods ─────────────────────────────────────────────────

    /// <summary>
    /// Charger sent StatusNotification or disconnected from the WebSocket.
    /// EVChargingApi uses this to push a live status update to the app via SignalR.
    /// </summary>
    public static Task PublishStatusChangedAsync(
        string ocppId, string status, int connectorId, bool isConnected)
        => PublishAsync("charger.status.changed",
            new { ocppId, status, connectorId, isConnected });

    /// <summary>
    /// Charger confirmed StartTransaction. EVChargingApi stores the OCPP
    /// transactionId on the active session so RemoteStop can include it.
    /// </summary>
    public static Task PublishTransactionStartedAsync(string ocppId, int transactionId)
        => PublishAsync("charger.transaction.started",
            new { ocppId, transactionId });

    /// <summary>
    /// Charger confirmed StopTransaction. Includes both meterStopWh and
    /// meterStartWh so EVChargingApi can calculate the real energy delta.
    /// </summary>
    public static Task PublishTransactionStoppedAsync(
        string ocppId, decimal meterStopWh, decimal? meterStartWh)
        => PublishAsync("charger.transaction.stopped",
            new { ocppId, meterStopWh, meterStartWh });

    /// <summary>
    /// Charger sent MeterValues. Carries live energy and instantaneous power
    /// so EVChargingApi can push real-time updates to the session screen.
    /// </summary>
    public static Task PublishMeterUpdatedAsync(
        string ocppId, decimal meterValueWh, decimal? meterStartWh, double? currentPowerKw)
        => PublishAsync("charger.meter.updated",
            new { ocppId, meterValueWh, meterStartWh, currentPowerKw });
}
