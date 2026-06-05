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
using OCPPServer.Tracing;
using RabbitMQ.Client;

namespace OCPPServer;

public static class RabbitMqPublisher
{
    internal static IConnection? SharedConnection => _connection;
    private const string Exchange = "ocpp.events";

    private static IConnection?     _connection;
    private static IChannel?        _channel;
    private static ITracingService? _tracer;

    // ── Startup ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Connects to RabbitMQ and declares the durable topic exchange.
    /// Called once from Program.cs after database migrations.
    /// Failures are logged but non-fatal — the server runs without event publishing.
    /// </summary>
    public static async Task ConfigureAsync(string host, string username, string password,
        ITracingService tracer, int reconnectDelayMs = 5_000)
    {
        // Retry until RabbitMQ is ready — mirrors the retry loop in EVChargingApi's
        // RabbitMqConsumerService so startup ordering never causes a silent failure.
        _tracer = tracer;
        while (true)
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
                    durable:    true,
                    autoDelete: false);

                _tracer.Info("RMQ", $"Connected to '{host}', exchange '{Exchange}' ready.");
                return;
            }
            catch (Exception ex)
            {
                OcppTrace.Error("RMQ", $"Not ready ({ex.Message}), retrying in {reconnectDelayMs} ms…");
                await Task.Delay(reconnectDelayMs);
            }
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

            _tracer?.Verbose("RMQ", $"[→] {routingKey}");
        }
        catch (Exception ex)
        {
            OcppTrace.Error("RMQ", $"Failed to publish '{routingKey}': {ex.Message}");
        }
    }

    // ── Public publish methods ─────────────────────────────────────────────────

    /// <summary>
    /// Charger sent Authorize.req with an EVCC ID (ISO 15118 vehicle MAC address).
    /// Backend should look up the registered car, decide Accepted/Rejected,
    /// and reply via command.authorize.response { ocppId, status }.
    /// If no response arrives within 10 s the server rejects automatically.
    ///
    /// <paramref name="connectorId"/> is included only when the OCPP version provides it
    /// (OCPP 2.x <c>evseId</c>). OCPP 1.6 Authorize.req has no EVSE field — pass null
    /// and the key will be omitted entirely from the published JSON.
    /// </summary>
    public static Task PublishAuthorizeRequestedAsync(string ocppId, string carId, int? connectorId = null)
    {
        var payload = new Dictionary<string, object?> { ["ocppId"] = ocppId, ["carId"] = carId };
        if (connectorId.HasValue) payload["connectorId"] = connectorId.Value;
        return PublishAsync("charger.authorize.requested", payload);
    }

    /// <summary>
    /// Charger sent StatusNotification or disconnected from the WebSocket.
    /// EVChargingApi uses this to push a live status update to the app via SignalR.
    /// </summary>
    public static Task PublishStatusChangedAsync(
        string ocppId, string status, int connectorId, bool isConnected, string? carId = null)
        => PublishAsync("charger.status.changed",
            new { ocppId, status, connectorId, isConnected, carId });

    /// <summary>
    /// Charger confirmed StartTransaction. EVChargingApi stores the OCPP
    /// transactionId on the active session so RemoteStop can include it.
    /// </summary>
    public static Task PublishTransactionStartedAsync(string ocppId, int transactionId, decimal? meterStartWh)
        => PublishAsync("charger.transaction.started",
            new { ocppId, transactionId, meterStartWh });

    /// <summary>
    /// Charger confirmed StopTransaction. Includes transactionId so EVChargingApi
    /// can reject stale stop events that belong to an older transaction, plus
    /// meterStopWh and meterStartWh to calculate the real energy delta.
    /// </summary>
    public static Task PublishTransactionStoppedAsync(
        string ocppId, int transactionId, decimal meterStopWh, decimal? meterStartWh)
        => PublishAsync("charger.transaction.stopped",
            new { ocppId, transactionId, meterStopWh, meterStartWh });

    /// <summary>
    /// Charger sent MeterValues. Carries live energy and instantaneous power
    /// so EVChargingApi can push real-time updates to the session screen.
    /// </summary>
    public static Task PublishMeterUpdatedAsync(
        string ocppId, decimal meterValueWh, decimal? meterStartWh, double? currentPowerKw, decimal? soc = null)
        => PublishAsync("charger.meter.updated",
            new { ocppId, meterValueWh, meterStartWh, currentPowerKw, soc });

    /// <summary>
    /// Charger responded to RemoteStartTransaction. EVChargingApi uses this to
    /// confirm or reject the session start on the user's side.
    /// trackingId is echoed back from the original command (PR-004) so the API can
    /// correlate the response exactly; null when the command carried none.
    /// </summary>
    public static Task PublishRemoteStartResponseAsync(
        string ocppId, string status, int connectorId, string idTag, Guid? trackingId = null)
        => PublishAsync("charger.remote.start.response",
            new { ocppId, status, connectorId, idTag, trackingId });

    /// <summary>
    /// Charger responded to RemoteStopTransaction.
    /// </summary>
    public static Task PublishRemoteStopResponseAsync(string ocppId, string status, int? transactionId)
        => PublishAsync("charger.remote.stop.response",
            new { ocppId, status, transactionId });

    /// <summary>
    /// Charger responded to TriggerMessage.
    /// </summary>
    public static Task PublishTriggerResponseAsync(string ocppId, string status, string requestedMessage)
        => PublishAsync("charger.trigger.response",
            new { ocppId, status, requestedMessage });

    /// <summary>
    /// Charger sent DiagnosticsStatusNotification — upload progress update.
    /// </summary>
    public static Task PublishDiagnosticsStatusAsync(string ocppId, string status)
        => PublishAsync("charger.diagnostics.status",
            new { ocppId, status });
}
