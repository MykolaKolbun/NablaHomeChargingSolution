using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using OCPPServer.ChargingStationInterface;
using OCPPServer.Data;
using OCPPServer.OCPP1._6_Models;
using System.Net.WebSockets;
using System.Text.Json;

namespace OCPPServer;

public sealed class OcppCommandHandler : IOcppCommandHandler
{
    private readonly ICommunicator _communicator;
    private readonly ChargingDBContext _db;

    public OcppCommandHandler(ICommunicator communicator, ChargingDBContext db)
    {
        _communicator = communicator;
        _db = db;
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
                        OcppTrace.Error("RMQ-Consumer", $"[remote.start] invalid payload — ocppId='{cmd.OcppId}' idTag='{cmd.IdTag}' connectorId={cmd.ConnectorId}");
                        break;
                    }

                    var socket = ChargingStationConnections.Get(cmd.OcppId);
                    if (socket is null || socket.State != WebSocketState.Open)
                    {
                        OcppTrace.Msg("RMQ-Consumer", $"[remote.start] {cmd.OcppId} not connected — discarding");
                        break;
                    }

                    var result = await _communicator.SendStartCharging(socket, cmd.OcppId, cmd.ConnectorId, cmd.IdTag);
                    var status = result["status"]?.Value<string>() ?? "Unknown";

                    if (status == "Accepted")
                    {
                        // Charger said it will try to start — wait for the actual StartTransaction.req
                        // before notifying EVChargingApi. Timeout: 60s (covers cable-plug-in delay).
                        var tcs = ChargingStationConnections.RegisterPendingStartTransaction(cmd.OcppId);
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

                        try
                        {
                            var transactionId = await tcs.Task.WaitAsync(timeoutCts.Token);

                            var plug = await _db.Plugs.FirstOrDefaultAsync(p => p.OcppId == cmd.OcppId, ct);
                            if (plug != null)
                            {
                                plug.IsOnline = true;
                                await _db.SaveChangesAsync(ct);
                            }

                            await RabbitMqPublisher.PublishRemoteStartResponseAsync(cmd.OcppId, "Accepted", cmd.ConnectorId, cmd.IdTag);
                        }
                        catch (OperationCanceledException)
                        {
                            ChargingStationConnections.CancelPendingStartTransaction(cmd.OcppId);
                            OcppTrace.Error("RMQ-Consumer", $"[remote.start] {cmd.OcppId} — StartTransaction not received within 60 s");
                        }
                    }
                    else
                    {
                        await RabbitMqPublisher.PublishRemoteStartResponseAsync(cmd.OcppId, status, cmd.ConnectorId, cmd.IdTag);
                    }
                    break;
                }

            case "command.remote.stop":
                {
                    var cmd = JsonSerializer.Deserialize<RemoteStopCommand>(json)!;

                    if (string.IsNullOrEmpty(cmd.OcppId) || cmd.TransactionId is null)
                    {
                        OcppTrace.Error("RMQ-Consumer", $"[remote.stop] invalid payload — ocppId='{cmd.OcppId}' transactionId={cmd.TransactionId}");
                        break;
                    }

                    var socket = ChargingStationConnections.Get(cmd.OcppId);
                    if (socket is null || socket.State != WebSocketState.Open)
                    {
                        OcppTrace.Msg("RMQ-Consumer", $"[remote.stop] {cmd.OcppId} not connected — discarding");
                        break;
                    }

                    var result = await _communicator.SendStopCharging(socket, cmd.OcppId, cmd.TransactionId);
                    var status = result["status"]?.Value<string>() ?? "Unknown";

                    if (status == "Accepted")
                    {
                        var tcs = ChargingStationConnections.RegisterPendingStopTransaction(cmd.OcppId);
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

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
                            OcppTrace.Error("RMQ-Consumer", $"[remote.stop] {cmd.OcppId} — StopTransaction not received within 60 s");
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
                        OcppTrace.Error("RMQ-Consumer", $"[commandreq] invalid payload — ocppId='{ocppId}' requestedMessage='{requestedMessage}'");
                        break;
                    }

                    var socket = ChargingStationConnections.Get(ocppId);
                    if (socket is null || socket.State != WebSocketState.Open)
                    {
                        OcppTrace.Msg("RMQ-Consumer", $"[commandreq] {ocppId} not connected — discarding");
                        break;
                    }

                    var result = await _communicator.SendTriggerMessage(socket, requestedMessage, connectorId);
                    var status = result["status"]?.Value<string>() ?? "Unknown";
                    await RabbitMqPublisher.PublishTriggerResponseAsync(ocppId, status, requestedMessage);
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
                        OcppTrace.Error("RMQ-Consumer", "[statusreq] invalid payload — ocppId is missing");
                        break;
                    }

                    var socket = ChargingStationConnections.Get(ocppId);
                    if (socket is null || socket.State != WebSocketState.Open)
                    {
                        OcppTrace.Msg("RMQ-Consumer", $"[statusreq] {ocppId} not connected — discarding");
                        break;
                    }

                    var result = await _communicator.SendStatusNotificationRequest(socket, connectorId);
                    var status = result["status"]?.Value<string>() ?? "Unknown";
                    await RabbitMqPublisher.PublishTriggerResponseAsync(ocppId, status, "StatusNotification");
                    break;
                }
            default:
                OcppTrace.Msg("RMQ-Consumer", $"Unknown command: {routingKey}");
                break;
        }
    }
}