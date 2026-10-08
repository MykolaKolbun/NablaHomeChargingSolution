using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using OCPPServer.ChargingStationInterface;
using OCPPServer.Data;
using OCPPServer.OCPP1._6_Models;
using OCPPServer.Tracing;
using System.Net.WebSockets;
using System.Text.Json;

namespace OCPPServer;

public sealed class OcppCommandHandler : IOcppCommandHandler
{
    private readonly ICommunicator     _communicator;
    private readonly ChargingDBContext _db;
    private readonly ITracingService   _tracer;
    private readonly TimeSpan          _txConfirmTimeout;
    private readonly TimeSpan          _commandTimeout;

    public OcppCommandHandler(ICommunicator communicator, ChargingDBContext db,
        ITracingService tracer, IConfiguration config)
    {
        _communicator     = communicator;
        _db               = db;
        _tracer           = tracer;
        _txConfirmTimeout = TimeSpan.FromSeconds(
            config.GetValue("Ocpp:TransactionConfirmTimeoutSeconds", 60));
        _commandTimeout   = TimeSpan.FromSeconds(
            config.GetValue("Ocpp:CommandTimeoutSeconds", 30));
    }

    public async Task HandleAsync(string routingKey, string json, CancellationToken ct)
    {
        using var payload = JsonDocument.Parse(json);
        switch (routingKey)
        {
            case "command.remote.start":
                {
                    var cmd = JsonSerializer.Deserialize<RemoteStartCommand>(json)!;

                    // Validate required fields per OCPP 1.6
                    if (string.IsNullOrEmpty(cmd.OcppId) || string.IsNullOrEmpty(cmd.IdTag) || cmd.ConnectorId < 0)
                    {
                        _tracer.Warning("RemoteStart",
                            $"Invalid payload — ocppId='{cmd.OcppId}' idTag='{cmd.IdTag}' connectorId={cmd.ConnectorId}");
                        break;
                    }

                    var socket = ChargingStationConnections.GetSocket(cmd.OcppId);
                    if (socket is null || socket.State != WebSocketState.Open)
                    {
                        OcppTrace.Msg("RemoteStart", $"{cmd.OcppId} not connected — discarding");
                        break;
                    }

                    var result = await _communicator.SendStartCharging(socket, cmd.OcppId, cmd.ConnectorId, cmd.IdTag);
                    var status = result["status"]?.Value<string>() ?? "Unknown";

                    if (status == "Accepted")
                    {
                        // Charger said it will try to start — wait for the actual StartTransaction.req
                        // before notifying EVChargingApi. Timeout covers cable-plug-in delay.
                        var tcs = ChargingStationConnections.RegisterPendingStartTransaction(cmd.OcppId);
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        timeoutCts.CancelAfter(_txConfirmTimeout);

                        try
                        {
                            var transactionId = await tcs.Task.WaitAsync(timeoutCts.Token);

                            var plug = await _db.Plugs.FirstOrDefaultAsync(p => p.OcppId == cmd.OcppId, ct);
                            if (plug != null)
                            {
                                plug.IsOnline = true;
                                await _db.SaveChangesAsync(ct);
                            }

                            await RabbitMqPublisher.PublishRemoteStartResponseAsync(cmd.OcppId, "Accepted", cmd.ConnectorId, cmd.IdTag, cmd.TrackingId);
                        }
                        catch (OperationCanceledException)
                        {
                            ChargingStationConnections.CancelPendingStartTransaction(cmd.OcppId);
                            _tracer.Warning("RemoteStart",
                                $"{cmd.OcppId} — StartTransaction not received within {_txConfirmTimeout.TotalSeconds:F0}s",
                                chargePointId: cmd.OcppId);
                        }
                    }
                    else
                    {
                        await RabbitMqPublisher.PublishRemoteStartResponseAsync(cmd.OcppId, status, cmd.ConnectorId, cmd.IdTag, cmd.TrackingId);
                    }
                    break;
                }

            case "command.remote.stop":
                {
                    var cmd = JsonSerializer.Deserialize<RemoteStopCommand>(json)!;

                    if (string.IsNullOrEmpty(cmd.OcppId) || cmd.TransactionId is null)
                    {
                        _tracer.Warning("RemoteStop",
                            $"Invalid payload — ocppId='{cmd.OcppId}' transactionId={cmd.TransactionId}");
                        break;
                    }

                    var socket = ChargingStationConnections.GetSocket(cmd.OcppId);
                    if (socket is null || socket.State != WebSocketState.Open)
                    {
                        OcppTrace.Msg("RemoteStop", $"{cmd.OcppId} not connected — discarding");
                        break;
                    }

                    var result = await _communicator.SendStopCharging(socket, cmd.OcppId, cmd.TransactionId);
                    var status = result["status"]?.Value<string>() ?? "Unknown";

                    if (status == "Accepted")
                    {
                        var tcs = ChargingStationConnections.RegisterPendingStopTransaction(cmd.OcppId);
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        timeoutCts.CancelAfter(_txConfirmTimeout);

                        try
                        {
                            await tcs.Task.WaitAsync(timeoutCts.Token);

                            var plug = await _db.Plugs.FirstOrDefaultAsync(p => p.OcppId == cmd.OcppId, ct);
                            if (plug != null)
                            {
                                plug.IsOnline = true;
                                await _db.SaveChangesAsync(ct);
                            }

                            await RabbitMqPublisher.PublishRemoteStopResponseAsync(cmd.OcppId, "Accepted", cmd.TransactionId);
                        }
                        catch (OperationCanceledException)
                        {
                            ChargingStationConnections.CancelPendingStopTransaction(cmd.OcppId);
                            _tracer.Warning("RemoteStop",
                                $"{cmd.OcppId} — StopTransaction not received within {_txConfirmTimeout.TotalSeconds:F0}s",
                                chargePointId: cmd.OcppId);
                            await RabbitMqPublisher.PublishRemoteStopResponseAsync(cmd.OcppId, "Timeout", cmd.TransactionId);
                        }
                    }
                    else
                    {
                        await RabbitMqPublisher.PublishRemoteStopResponseAsync(cmd.OcppId, status, cmd.TransactionId);
                    }
                    break;
                }

            case "command.commandreq":
                {
                    var ocppId = payload.RootElement.TryGetProperty("ocppId", out var idEl)
                        ? idEl.GetString() : null;
                    var requestedMessage = payload.RootElement.TryGetProperty("requestedMessage", out var rmEl)
                        ? rmEl.GetString() : null;
                    var connectorId = payload.RootElement.TryGetProperty("connectorId", out var c)
                                      && c.ValueKind != JsonValueKind.Null
                                           ? c.GetInt32() : (int?)null;

                    if (string.IsNullOrEmpty(ocppId) || string.IsNullOrEmpty(requestedMessage))
                    {
                        _tracer.Warning("RMQ-Consumer",
                            $"[commandreq] invalid payload — ocppId='{ocppId}' requestedMessage='{requestedMessage}'");
                        break;
                    }

                    var socket = ChargingStationConnections.GetSocket(ocppId);
                    if (socket is null || socket.State != WebSocketState.Open)
                    {
                        OcppTrace.Msg("RMQ-Consumer", $"[commandreq] {ocppId} not connected — discarding");
                        break;
                    }

                    try
                    {
                        var result = await _communicator.SendTriggerMessage(socket, ocppId, requestedMessage, connectorId);
                        var status = result["status"]?.Value<string>() ?? "Unknown";
                        await RabbitMqPublisher.PublishTriggerResponseAsync(ocppId, status, requestedMessage);
                    }
                    catch (OperationCanceledException)
                    {
                        _tracer.Warning("RMQ-Consumer",
                            $"[commandreq] {ocppId} — TriggerMessage({requestedMessage}) timed out ({_commandTimeout.TotalSeconds:F0}s)",
                            chargePointId: ocppId);
                        await RabbitMqPublisher.PublishTriggerResponseAsync(ocppId, "Timeout", requestedMessage);
                    }
                    break;
                }

            case "command.statusreq":
                {
                    var ocppId = payload.RootElement.TryGetProperty("ocppId", out var idEl)
                        ? idEl.GetString() : null;
                    var connectorId = payload.RootElement.TryGetProperty("connectorId", out var c)
                                      && c.ValueKind != JsonValueKind.Null
                                      ? c.GetInt32() : (int?)null;

                    if (string.IsNullOrEmpty(ocppId))
                    {
                        _tracer.Warning("RMQ-Consumer", "[statusreq] invalid payload — ocppId is missing");
                        break;
                    }

                    var socket = ChargingStationConnections.GetSocket(ocppId);
                    if (socket is null || socket.State != WebSocketState.Open)
                    {
                        OcppTrace.Msg("RMQ-Consumer", $"[statusreq] {ocppId} not connected — discarding");
                        break;
                    }

                    try
                    {
                        var result = await _communicator.SendStatusNotificationRequest(socket, ocppId, connectorId);
                        var status = result["status"]?.Value<string>() ?? "Unknown";
                        await RabbitMqPublisher.PublishTriggerResponseAsync(ocppId, status, "StatusNotification");
                    }
                    catch (OperationCanceledException)
                    {
                        _tracer.Warning("RMQ-Consumer",
                            $"[statusreq] {ocppId} — TriggerMessage(StatusNotification) timed out ({_commandTimeout.TotalSeconds:F0}s)",
                            chargePointId: ocppId);
                        await RabbitMqPublisher.PublishTriggerResponseAsync(ocppId, "Timeout", "StatusNotification");
                    }
                    break;
                }

            case "command.authorize.response":
                {
                    var ocppId = payload.RootElement.TryGetProperty("ocppId", out var idEl)
                        ? idEl.GetString() : null;
                    var status = payload.RootElement.TryGetProperty("status", out var stEl)
                        ? stEl.GetString() : null;

                    if (string.IsNullOrEmpty(ocppId) || string.IsNullOrEmpty(status))
                    {
                        _tracer.Warning("Authorize",
                            $"[authorize.response] invalid payload — ocppId='{ocppId}' status='{status}'");
                        break;
                    }

                    _tracer.Info("Authorize", $"{ocppId}: backend authorize response — status={status}");
                    ChargingStationConnections.CompletePendingAuthorize(ocppId, status);
                    break;
                }

            default:
                OcppTrace.Msg("RMQ-Consumer", $"Unknown command: {routingKey}");
                break;
        }
    }
}
