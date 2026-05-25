using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCPPServer.ChargingStationInterface;
using OCPPServer.Data;
using OCPPServer.DataBase.DBModels;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace OCPPServer.OCPP2._1_Models;

/// <summary>
/// OCPP 2.0.1 / 2.1 message handler.
/// Handles incoming CALL messages from chargers that negotiated ocpp2.1 or ocpp2.0.1
/// at the WebSocket handshake. Publishes the same RabbitMQ events as the 1.6 handler
/// so downstream consumers require no changes.
///
/// Message type numbers (OCPP 2.1):
///   2 = CALL, 3 = CALLRESULT, 4 = CALLERROR,
///   5 = CALLRESULTERROR (new in 2.1), 6 = SEND (no response expected)
/// </summary>
public sealed class Ocpp21Communicator
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _pendingRequests = new();

    // ── ConnectorStatus 2.x → ChargePointStatus 1.6 best-effort mapping ──────────
    private static Enumerators.ChargePointStatus MapConnectorStatus(string status) => status switch
    {
        "Available"   => Enumerators.ChargePointStatus.Available,
        "Occupied"    => Enumerators.ChargePointStatus.Charging,
        "Reserved"    => Enumerators.ChargePointStatus.Reserved,
        "Unavailable" => Enumerators.ChargePointStatus.Unavailable,
        "Faulted"     => Enumerators.ChargePointStatus.Faulted,
        _             => Enumerators.ChargePointStatus.Available
    };

    public async Task RouteOcppMessage(WebSocket socket, string stationId, string json, ChargingDBContext db)
    {
        JArray message;
        try { message = JArray.Parse(json); }
        catch (Exception ex)
        {
            OcppTrace.Error("OCPP21", $"[PARSE ERR] {stationId}: {ex.Message}");
            return;
        }

        var messageType = message[0].Value<int>();
        var messageId   = message[1].Value<string>() ?? string.Empty;

        // CALLRESULT / CALLERROR / CALLRESULTERROR → complete pending request
        if (messageType is 3 or 4 or 5)
        {
            if (_pendingRequests.TryRemove(messageId, out var tcs))
            {
                var payload = messageType == 3
                    ? (JObject)message[2]
                    : new JObject { ["errorCode"] = message[2], ["errorDescription"] = message[3] };
                tcs.TrySetResult(payload);
            }
            else
            {
                OcppTrace.Error("OCPP21", $"[← {messageType}] {stationId} msgId={messageId} — no matching pending request");
            }
            return;
        }

        // SEND (type 6) — unidirectional, no response expected
        if (messageType == 6)
        {
            var action6  = message[2].Value<string>() ?? string.Empty;
            var payload6 = (JObject)message[3];
            OcppTrace.Msg("OCPP21", $"[← SEND] {stationId}: action={action6}");
            await HandleCallOrSend(socket, stationId, action6, messageId, payload6, db, requiresResponse: false);
            return;
        }

        if (messageType != 2)
        {
            OcppTrace.Error("OCPP21", $"[WARN] {stationId}: unknown messageType={messageType}");
            return;
        }

        var actionName = message[2].Value<string>() ?? string.Empty;
        var callPayload = (JObject)message[3];
        OcppTrace.Msg("OCPP21", $"[← CALL] {stationId}: action={actionName}");

        await HandleCallOrSend(socket, stationId, actionName, messageId, callPayload, db, requiresResponse: true);
    }

    private async Task HandleCallOrSend(
        WebSocket socket, string stationId, string action, string messageId,
        JObject payload, ChargingDBContext db, bool requiresResponse)
    {
        try
        {
            switch (action)
            {
                case "BootNotification":
                    await HandleBootNotification(socket, messageId, payload, stationId, db);
                    break;

                case "Heartbeat":
                    await HandleHeartbeat(socket, messageId, stationId, db);
                    break;

                case "StatusNotification":
                    await HandleStatusNotification(socket, messageId, payload, stationId, db);
                    break;

                case "TransactionEvent":
                    await HandleTransactionEvent(socket, messageId, payload, stationId, db);
                    break;

                case "MeterValues":
                    await HandleMeterValues(socket, messageId, payload, stationId, db);
                    break;

                default:
                    OcppTrace.Msg("OCPP21", $"[WARN] {stationId}: unhandled action '{action}'");
                    if (requiresResponse)
                        await SendCallResult(socket, messageId, new JObject());
                    break;
            }
        }
        catch (Exception ex)
        {
            OcppTrace.Error("OCPP21", $"[EXCEPTION] {stationId} handling {action}: {ex.Message}");
            try
            {
                if (requiresResponse)
                    await SendCallError(socket, messageId, "InternalError", ex.Message);
            }
            catch { }
        }
    }

    // ── BootNotification ──────────────────────────────────────────────────────────
    // OCPP 2.x payload:
    //   { "reason": "PowerUp", "chargingStation": { "model": "...", "vendorName": "...",
    //     "serialNumber": "...", "firmwareVersion": "...", "modem": { "iccid": "..." } } }

    private async Task HandleBootNotification(
        WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
    {
        var cs      = payload["chargingStation"] as JObject;
        var model   = cs?["model"]?.Value<string>();
        var vendor  = cs?["vendorName"]?.Value<string>();
        var serial  = cs?["serialNumber"]?.Value<string>();
        var fw      = cs?["firmwareVersion"]?.Value<string>();
        var iccid   = cs?["modem"]?["iccid"]?.Value<string>();
        var protocol = ChargingStationConnections.GetProtocol(stationId);

        OcppTrace.Dbg("OCPP21", $"BootNotification from {stationId}: vendor={vendor} model={model} protocol={protocol}");

        var plug = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == stationId);
        if (plug == null)
        {
            db.Plugs.Add(new Plug
            {
                OcppId           = stationId,
                Vendor           = vendor,
                ChargePointModel = model,
                ChargePointSN    = serial ?? string.Empty,
                FirmwareVersion  = fw,
                SIMNr            = iccid,
                OcppVersion      = protocol,
                Status           = Enumerators.ChargePointStatus.Available,
                IsOnline         = true,
                MeterValue       = 0m,
                CreatedAt        = DateTime.UtcNow,
                LastStatusUpdate = DateTime.UtcNow,
            });
        }
        else
        {
            if (!string.IsNullOrEmpty(vendor))  plug.Vendor           = vendor;
            if (!string.IsNullOrEmpty(model))   plug.ChargePointModel = model;
            if (!string.IsNullOrEmpty(serial))  plug.ChargePointSN    = serial;
            if (!string.IsNullOrEmpty(fw))      plug.FirmwareVersion  = fw;
            if (!string.IsNullOrEmpty(iccid))   plug.SIMNr            = iccid;

            plug.OcppVersion      = protocol;
            plug.Status           = Enumerators.ChargePointStatus.Available;
            plug.IsOnline         = true;
            plug.LastStatusUpdate = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        await SendCallResult(socket, messageId, new JObject
        {
            ["currentTime"] = DateTime.UtcNow.ToString("o"),
            ["interval"]    = 300,
            ["status"]      = "Accepted"
        });
    }

    // ── Heartbeat ─────────────────────────────────────────────────────────────────

    private async Task HandleHeartbeat(
        WebSocket socket, string messageId, string stationId, ChargingDBContext db)
    {
        OcppTrace.Dbg("OCPP21", $"Heartbeat from {stationId}");
        var plug = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == stationId);
        if (plug != null)
        {
            plug.IsOnline         = true;
            plug.LastStatusUpdate = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        await SendCallResult(socket, messageId, new JObject
        {
            ["currentTime"] = DateTime.UtcNow.ToString("o")
        });
    }

    // ── StatusNotification ───────────────────────────────────────────────────────
    // OCPP 2.x payload:
    //   { "timestamp": "...", "connectorStatus": "Available", "evseId": 1, "connectorId": 1 }

    private async Task HandleStatusNotification(
        WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
    {
        var evseId      = payload["evseId"]?.Value<int>() ?? 0;
        var connectorId = payload["connectorId"]?.Value<int>() ?? 1;
        var statusStr   = payload["connectorStatus"]?.Value<string>() ?? "";

        OcppTrace.Msg("OCPP21", $"StatusNotification {stationId} evseId={evseId} connectorId={connectorId} status={statusStr}");

        var plug = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == stationId);
        if (plug != null)
        {
            plug.Status           = MapConnectorStatus(statusStr);
            plug.LastStatusUpdate = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        _ = RabbitMqPublisher.PublishStatusChangedAsync(stationId, statusStr, connectorId, isConnected: true);

        await SendCallResult(socket, messageId, new JObject());
    }

    // ── TransactionEvent ─────────────────────────────────────────────────────────
    // Replaces StartTransaction + StopTransaction + MeterValues from OCPP 1.6.
    // eventType: "Started" | "Updated" | "Ended"

    private async Task HandleTransactionEvent(
        WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
    {
        var eventType   = payload["eventType"]?.Value<string>() ?? "";
        var txInfo      = payload["transactionInfo"] as JObject;
        var ocppTxId    = txInfo?["transactionId"]?.Value<string>() ?? string.Empty;
        var meterValues = payload["meterValue"] as JArray;

        OcppTrace.Msg("OCPP21", $"TransactionEvent {stationId}: eventType={eventType} ocppTxId={ocppTxId}");

        switch (eventType)
        {
            case "Started":
                await HandleTxStarted(socket, messageId, stationId, ocppTxId, meterValues, db);
                break;
            case "Updated":
                await HandleTxUpdated(socket, messageId, stationId, meterValues, db);
                break;
            case "Ended":
                await HandleTxEnded(socket, messageId, stationId, ocppTxId, meterValues, db);
                break;
            default:
                await SendCallResult(socket, messageId, new JObject());
                break;
        }
    }

    private async Task HandleTxStarted(
        WebSocket socket, string messageId, string stationId,
        string ocppTxId, JArray? meterValues, ChargingDBContext db)
    {
        var localTxId    = ChargingStationConnections.RegisterOcpp21Transaction(stationId, ocppTxId);
        var meterStartWh = MeterValueParser.Parse(meterValues).EnergyWh;

        OcppTrace.Msg("OCPP21", $"Transaction started {stationId}: localId={localTxId} ocppTxId={ocppTxId}");

        if (meterStartWh.HasValue)
        {
            ChargingStationConnections.SetMeterStart(stationId, meterStartWh.Value);
            var plug = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == stationId);
            if (plug != null)
            {
                plug.MeterValue       = meterStartWh.Value;
                plug.LastMeterValueAt = null;
                plug.LastStatusUpdate = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        await SendCallResult(socket, messageId, new JObject
        {
            ["idTokenInfo"] = new JObject { ["status"] = "Accepted" }
        });

        ChargingStationConnections.CompletePendingStartTransaction(stationId, localTxId);
        _ = RabbitMqPublisher.PublishTransactionStartedAsync(stationId, localTxId, meterStartWh);
    }

    private async Task HandleTxUpdated(
        WebSocket socket, string messageId, string stationId,
        JArray? meterValues, ChargingDBContext db)
    {
        var readings = MeterValueParser.Parse(meterValues);
        if (!readings.EnergyWh.HasValue)
        {
            await SendCallResult(socket, messageId, new JObject());
            return;
        }

        var plug = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == stationId);
        var now  = DateTime.UtcNow;

        double? currentPowerKw = readings.PowerKw;
        if (currentPowerKw == null && plug?.MeterValue.HasValue == true && plug.LastMeterValueAt.HasValue)
        {
            var deltaWh    = (double)(readings.EnergyWh.Value - plug.MeterValue.Value);
            var deltaHours = (now - plug.LastMeterValueAt.Value).TotalHours;
            if (deltaHours > 0 && deltaWh >= 0)
                currentPowerKw = Math.Round(deltaWh / 1000.0 / deltaHours, 2);
        }

        if (plug != null)
        {
            plug.MeterValue       = readings.EnergyWh.Value;
            plug.LastMeterValueAt = now;
            plug.LastStatusUpdate = now;
            if (readings.SoC.HasValue)
                plug.StateOfCharge = readings.SoC;
            await db.SaveChangesAsync();
        }

        _ = RabbitMqPublisher.PublishMeterUpdatedAsync(
            stationId, readings.EnergyWh.Value,
            ChargingStationConnections.GetMeterStart(stationId),
            currentPowerKw, readings.SoC);

        await SendCallResult(socket, messageId, new JObject());
    }

    private async Task HandleTxEnded(
        WebSocket socket, string messageId, string stationId,
        string ocppTxId, JArray? meterValues, ChargingDBContext db)
    {
        var meterStopWh       = MeterValueParser.Parse(meterValues).EnergyWh;
        var meterStartForStop = ChargingStationConnections.GetMeterStart(stationId);
        // Read local tx ID before clearing — needed for RabbitMQ event and pending stop signal
        var localTxId         = ChargingStationConnections.GetOcpp21LocalTxId(stationId) ?? 0;

        if (meterStopWh.HasValue)
        {
            var plug = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == stationId);
            if (plug != null)
            {
                plug.MeterValue       = meterStopWh.Value;
                plug.LastMeterValueAt = null;
                plug.LastStatusUpdate = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        ChargingStationConnections.ClearMeterStart(stationId);
        ChargingStationConnections.ClearOcpp21Transaction(stationId);

        await SendCallResult(socket, messageId, new JObject());

        _ = RabbitMqPublisher.PublishTransactionStoppedAsync(
            stationId, localTxId, meterStopWh ?? 0, meterStartForStop);

        ChargingStationConnections.CompletePendingStopTransaction(stationId, localTxId);
    }

    // ── MeterValues (standalone, some 2.x chargers send both MeterValues + TransactionEvent) ─────

    private async Task HandleMeterValues(
        WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
    {
        var readings = MeterValueParser.Parse(payload["meterValue"] as JArray);

        if (!readings.EnergyWh.HasValue)
        {
            await SendCallResult(socket, messageId, new JObject());
            return;
        }

        var plug = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == stationId);
        var now  = DateTime.UtcNow;

        double? currentPowerKw = readings.PowerKw;
        if (currentPowerKw == null && plug?.MeterValue.HasValue == true && plug.LastMeterValueAt.HasValue)
        {
            var deltaWh    = (double)(readings.EnergyWh.Value - plug.MeterValue.Value);
            var deltaHours = (now - plug.LastMeterValueAt.Value).TotalHours;
            if (deltaHours > 0 && deltaWh >= 0)
                currentPowerKw = Math.Round(deltaWh / 1000.0 / deltaHours, 2);
        }

        if (plug != null)
        {
            plug.MeterValue       = readings.EnergyWh.Value;
            plug.LastMeterValueAt = now;
            plug.LastStatusUpdate = now;
            if (readings.SoC.HasValue)
                plug.StateOfCharge = readings.SoC;
            await db.SaveChangesAsync();
        }

        _ = RabbitMqPublisher.PublishMeterUpdatedAsync(
            stationId, readings.EnergyWh.Value,
            ChargingStationConnections.GetMeterStart(stationId),
            currentPowerKw, readings.SoC);

        await SendCallResult(socket, messageId, new JObject());
    }

    // ── Outbound commands ────────────────────────────────────────────────────────

    /// <summary>
    /// OCPP 2.x equivalent of RemoteStartTransaction.
    /// The response contains an optional transactionId (string); actual confirmation
    /// arrives later via TransactionEvent(Started).
    /// </summary>
    public async Task<JObject> SendStartCharging(WebSocket socket, string stationId, int evseId, string idTag)
    {
        // remoteStartId reuses the 1.6 counter — just needs to be a unique int
        var remoteStartId = ChargingStationConnections.AssignTransaction(stationId);

        var payload = new JObject
        {
            ["remoteStartId"] = remoteStartId,
            ["idToken"]       = new JObject { ["idToken"] = idTag, ["type"] = "ISO14443" }
        };
        if (evseId > 0)
            payload["evseId"] = evseId;

        return await SendCallAndWaitAsync(socket, "RequestStartTransaction", payload, TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// OCPP 2.x equivalent of RemoteStopTransaction.
    /// Looks up the OCPP 2.x string transactionId from the local int mapping.
    /// </summary>
    public async Task<JObject> SendStopCharging(WebSocket socket, string stationId, int? transactionId)
    {
        var ocppTxId = ChargingStationConnections.GetOcpp21TxId(stationId);
        if (ocppTxId is null)
        {
            OcppTrace.Error("OCPP21", $"[→ OUT] {stationId}: RequestStopTransaction — no active OCPP 2.x transaction found");
            return new JObject { ["status"] = "Rejected" };
        }

        return await SendCallAndWaitAsync(socket, "RequestStopTransaction",
            new JObject { ["transactionId"] = ocppTxId },
            TimeSpan.FromSeconds(30));
    }

    // ── Wire helpers ─────────────────────────────────────────────────────────────

    public async Task<JObject> SendCallAndWaitAsync(
        WebSocket socket, string action, JObject payload, TimeSpan timeout)
    {
        var msgId = Guid.NewGuid().ToString();
        var tcs   = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[msgId] = tcs;

        await SendAsync(socket, new JArray { 2, msgId, action, payload });

        using var cts = new CancellationTokenSource(timeout);
        using (cts.Token.Register(() => tcs.TrySetCanceled()))
            return await tcs.Task;
    }

    private async Task SendCallResult(WebSocket socket, string messageId, JObject payload)
        => await SendAsync(socket, new JArray { 3, messageId, payload });

    private async Task SendCallError(WebSocket socket, string messageId, string errorCode, string description)
        => await SendAsync(socket, new JArray { 4, messageId, errorCode, description, new JObject() });

    private async Task SendAsync(WebSocket socket, JArray message)
    {
        var json  = message.ToString(Formatting.None);
        var bytes = Encoding.UTF8.GetBytes(json);
        OcppTrace.Msg("OCPP21", $"[→ OUT] {json}");
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

}
