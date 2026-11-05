using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCPP_RD.OCPP1._6_Models;
using OCPPServer.ChargerAdapters;
using OCPPServer.ChargingStationInterface;
using OCPPServer.Data;
using OCPPServer.DataBase.DBModels;
using OCPPServer.Tracing;
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
    private readonly ITracingService _tracer;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _pendingRequests = new();

    private readonly TimeSpan _commandTimeout;
    private readonly TimeSpan _authorizeTimeout;

    public Ocpp21Communicator(ITracingService tracer, IConfiguration config)
    {
        _tracer           = tracer;
        _commandTimeout   = TimeSpan.FromSeconds(config.GetValue("Ocpp:CommandTimeoutSeconds",   30));
        _authorizeTimeout = TimeSpan.FromSeconds(config.GetValue("Ocpp:AuthorizeTimeoutSeconds", 10));
    }

    // ── ConnectorStatus 2.x → ChargePointStatus 1.6 best-effort mapping ──────────
    // Returns null for unrecognised values so the caller can log a warning and
    // leave the DB status unchanged (same behaviour as the OCPP 1.6 handler).
    private static Enumerators.ChargePointStatus? MapConnectorStatus(string status) => status switch
    {
        "Available"   => Enumerators.ChargePointStatus.Available,
        "Occupied"    => Enumerators.ChargePointStatus.Charging,
        "Reserved"    => Enumerators.ChargePointStatus.Reserved,
        "Unavailable" => Enumerators.ChargePointStatus.Unavailable,
        "Faulted"     => Enumerators.ChargePointStatus.Faulted,
        _             => null
    };

    public async Task RouteOcppMessage(WebSocket socket, string stationId, string json, ChargingDBContext db)
    {
        JArray message;
        try { message = JArray.Parse(json); }
        catch (Exception ex)
        {
            _tracer.Warning("OCPP21", $"{stationId}: failed to parse JSON — {ex.Message}");
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
                _tracer.Warning("OCPP21", $"[← {messageType}] {stationId} msgId={messageId} — no matching pending request", chargePointId: stationId);
            }
            return;
        }

        // SEND (type 6) — unidirectional, no response expected
        if (messageType == 6)
        {
            var action6  = message[2].Value<string>() ?? string.Empty;
            var payload6 = (JObject)message[3];
            _tracer.Verbose("OCPP21", $"[← SEND] {stationId}: action={action6}", chargePointId: stationId);
            await HandleCallOrSend(socket, stationId, action6, messageId, payload6, db, requiresResponse: false);
            return;
        }

        if (messageType != 2)
        {
            _tracer.Warning("OCPP21", $"{stationId}: unknown messageType={messageType}");
            return;
        }

        var actionName = message[2].Value<string>() ?? string.Empty;
        var callPayload = (JObject)message[3];
        _tracer.Verbose("OCPP21", $"[← CALL] {stationId}: action={actionName}", chargePointId: stationId);

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

                case "Authorize":
                    await HandleAuthorize(socket, messageId, payload, stationId);
                    break;

                case "TransactionEvent":
                    await HandleTransactionEvent(socket, messageId, payload, stationId, db);
                    break;

                case "MeterValues":
                    await HandleMeterValues(socket, messageId, payload, stationId, db);
                    break;

                default:
                    _tracer.Warning("OCPP21", $"[WARN] {stationId}: unhandled action '{action}'", chargePointId: stationId);
                    if (requiresResponse)
                        await SendCallResult(socket, messageId, new JObject());
                    break;
            }
        }
        catch (Exception ex)
        {
            _tracer.Exception("OCPP21", ex, $"{stationId} handling {action}", chargePointId: stationId);
            try
            {
                if (requiresResponse)
                    await SendCallError(socket, messageId, "InternalError", ex.Message);
            }
            catch { }
        }
    }

    // ── Vehicle identification helpers ───────────────────────────────────────────

    /// <summary>
    /// Extracts the EVCC ID (vehicle MAC address) from an OCPP 2.x idToken object.
    /// Returns null for non-vehicle tokens (RFID, KeyCode, etc.).
    ///
    /// Supports two forms per the ISO 15118 whitepaper:
    ///   1. Direct: idToken.type == "MacAddress"  →  idToken.idToken is the EVCC ID
    ///   2. Combined: primary token is RFID/KeyCode, EVCC ID is in additionalInfo[type=="MacAddress"]
    /// </summary>
    private static string? ExtractCarId(JObject? idToken)
    {
        if (idToken is null) return null;

        var type  = idToken["type"]?.Value<string>() ?? "";
        var value = idToken["idToken"]?.Value<string>() ?? "";

        if (type.Equals("MacAddress", StringComparison.OrdinalIgnoreCase))
            return value;

        // Combined auth+identification: EVCC ID is in additionalInfo
        var additional = idToken["additionalInfo"] as JArray;
        if (additional != null)
        {
            foreach (var entry in additional)
            {
                if (entry["type"]?.Value<string>()?.Equals("MacAddress", StringComparison.OrdinalIgnoreCase) == true)
                    return entry["additionalIdToken"]?.Value<string>();
            }
        }

        return null;
    }

    private async Task HandleAuthorize(WebSocket socket, string messageId, JObject payload, string stationId)
    {
        var carId = ExtractCarId(payload["idToken"] as JObject);

        // OCPP 2.x Authorize.req carries an optional evseId array (§6.1 of the spec).
        // Extract the first element when present — a vehicle connects to one EVSE at a time.
        int? connectorId = null;
        if (payload["evseId"] is JArray evseArr && evseArr.Count > 0)
            connectorId = evseArr[0].Value<int?>();

        OcppTrace.Msg("OCPP21", $"Authorize {stationId}: carId={carId ?? "(none)"} evseId={connectorId?.ToString() ?? "(none)"}");

        // EVCC ID present → ask the backend, wait for its decision (same pattern as 1.6).
        // Regular RFID/app token → accept immediately.
        if (carId != null)
        {
            if (connectorId is > 0)
            {
                var conn = ChargingStationConnections.GetConnector(stationId, connectorId.Value);
                if (conn != null) conn.CarId = carId;
            }
            else
                connectorId = ChargingStationConnections.BindAuthorizedCar(stationId, carId);

            // Register the waiter BEFORE publishing (see OCPP 1.6 HandleAuthorize): a fast backend
            // answer arriving before registration would be dropped and the vehicle rejected.
            var tcs        = ChargingStationConnections.RegisterPendingAuthorize(stationId);
            var authStatus = "Rejected"; // fail closed

            _ = RabbitMqPublisher.PublishAuthorizeRequestedAsync(stationId, carId, connectorId);

            using var cts = new CancellationTokenSource(_authorizeTimeout);
            try
            {
                authStatus = await tcs.Task.WaitAsync(cts.Token);
                _tracer.Info("Authorize", $"{stationId}: backend responded {authStatus}",
                    chargePointId: stationId);
            }
            catch (OperationCanceledException)
            {
                ChargingStationConnections.CancelPendingAuthorize(stationId);
                _tracer.Warning("Authorize", $"{stationId}: backend timeout — rejecting vehicle",
                    chargePointId: stationId);
            }

            await SendCallResult(socket, messageId, new JObject
            {
                ["idTokenInfo"] = new JObject { ["status"] = authStatus }
            });
        }
        else
        {
            // Regular RFID / app token — accept immediately
            await SendCallResult(socket, messageId, new JObject
            {
                ["idTokenInfo"] = new JObject { ["status"] = "Accepted" }
            });
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
                CreatedAt        = DateTime.UtcNow,
                LastStatusUpdate = DateTime.UtcNow,
                LastHeartbeatAt  = DateTime.UtcNow,
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
            plug.LastHeartbeatAt  = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        var state = ChargingStationConnections.Get(stationId);
        if (state != null)
        {
            state.Adapter = ChargerAdapterFactory.Create(vendor);
            state.Adapter.OnBootNotification(payload);
        }

        await SendCallResult(socket, messageId, new JObject
        {
            ["currentTime"] = DateTime.UtcNow.ToString("o"),
            ["interval"]    = 60,
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
            plug.LastHeartbeatAt  = DateTime.UtcNow;
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

        _tracer.Info("StatusNotification",
            $"{stationId} evseId={evseId} connectorId={connectorId} status={statusStr}",
            chargePointId: stationId);

        // Our connector key is the EVSE (one session per EVSE); the 2.x connectorId is the plug inside it.
        var key = evseId > 0 ? evseId : connectorId;

        await ConnectorStore.SetStatusAsync(db, stationId, key, statusStr);

        // Plug.Status is the legacy single-connector view (admin REST): it mirrors EVSE 1.
        if (key == 1)
        {
            var plug = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == stationId);
            if (plug != null)
            {
                var mappedStatus = MapConnectorStatus(statusStr);
                if (mappedStatus.HasValue)
                    plug.Status = mappedStatus.Value;
                else
                    _tracer.Warning("StatusNotification",
                        $"{stationId}: unknown OCPP 2.x connectorStatus '{statusStr}' — DB status not changed",
                        chargePointId: stationId);

                plug.LastStatusUpdate = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        var conn21 = ChargingStationConnections.GetConnector(stationId, key);
        string? carId21 = null;
        if (conn21 != null)
        {
            // 2.x "Occupied" ≈ 1.6 "Preparing" — stored as Preparing so the Authorize binding works alike
            conn21.Status = statusStr == "Occupied" ? "Preparing" : statusStr;
            if (statusStr == "Occupied")
                carId21 = conn21.CarId;
            else if (statusStr == "Available")
                conn21.CarId = null;        // vehicle disconnected — clear for next session
        }

        _ = RabbitMqPublisher.PublishStatusChangedAsync(stationId, statusStr, key, isConnected: true, carId21);

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
        var idToken     = payload["idToken"] as JObject;
        var evseId      = payload["evse"]?["id"]?.Value<int?>();

        OcppTrace.Msg("OCPP21", $"TransactionEvent {stationId}: eventType={eventType} ocppTxId={ocppTxId} evse={evseId?.ToString() ?? "-"}");

        switch (eventType)
        {
            case "Started":
                await HandleTxStarted(socket, messageId, stationId, ocppTxId, evseId ?? 1, meterValues, db, idToken);
                break;
            case "Updated":
                await HandleTxUpdated(socket, messageId, stationId, ocppTxId, evseId, meterValues, db);
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
        string ocppTxId, int evseId, JArray? meterValues, ChargingDBContext db,
        JObject? idToken = null)
    {
        if (evseId <= 0) evseId = 1;
        var localTxId = ChargingStationConnections.RegisterOcpp21Transaction(stationId, evseId, ocppTxId);

        var state = ChargingStationConnections.Get(stationId);
        var conn  = state?.Connector(evseId);
        var meterStartWh = state?.Adapter.ParseMeterValues(meterValues).EnergyWh
                           ?? MeterValueParser.Parse(meterValues).EnergyWh;

        // EVCC ID: from this TransactionEvent (charger may skip Authorize), else an unbound Authorize one
        if (conn != null)
        {
            var carId = idToken != null ? ExtractCarId(idToken) : null;
            if (carId != null)
            {
                conn.CarId = carId;
                if (state!.UnboundCarId == carId) state.UnboundCarId = null;
                OcppTrace.Dbg("OCPP21", $"CarId from TransactionEvent(Started) for {stationId}#{evseId}: {carId}");
            }
            else if (conn.CarId is null && state!.UnboundCarId is { } unbound)
            {
                conn.CarId         = unbound;
                state.UnboundCarId = null;
            }

            conn.MeterStartWh     = meterStartWh;
            conn.MeterValueWh     = meterStartWh;
            conn.LastMeterValueAt = null;
            conn.CurrentPowerKw   = null;
            conn.StateOfCharge    = null;
        }

        _tracer.Info("StartTransaction",
            $"{stationId}: evse={evseId} localId={localTxId} ocppTxId={ocppTxId}",
            chargePointId: stationId, sessionId: localTxId);

        // Persist transaction + MeterStartWh per connector so they survive an OCPP server restart.
        await ConnectorStore.StartTransactionAsync(db, stationId, evseId, localTxId, meterStartWh);

        await SendCallResult(socket, messageId, new JObject
        {
            ["idTokenInfo"] = new JObject { ["status"] = "Accepted" }
        });

        ChargingStationConnections.CompletePendingStartTransaction(stationId, evseId, localTxId);
        _ = RabbitMqPublisher.PublishTransactionStartedAsync(stationId, localTxId, meterStartWh, evseId);
    }

    private async Task HandleTxUpdated(
        WebSocket socket, string messageId, string stationId,
        string ocppTxId, int? evseId, JArray? meterValues, ChargingDBContext db)
    {
        var conn = ChargingStationConnections.Get(stationId)?.ByOcppTransaction(ocppTxId)
                   ?? (evseId is > 0 ? ChargingStationConnections.GetConnector(stationId, evseId.Value) : null);
        await ProcessMeterAsync(socket, messageId, stationId, conn?.ConnectorId ?? evseId ?? 1, meterValues, db);
    }

    private async Task HandleTxEnded(
        WebSocket socket, string messageId, string stationId,
        string ocppTxId, JArray? meterValues, ChargingDBContext db)
    {
        var state = ChargingStationConnections.Get(stationId);
        var conn  = state?.ByOcppTransaction(ocppTxId);

        // Read before clearing — needed for the RabbitMQ event and pending stop signal
        var localTxId = conn?.LocalTxId ?? 0;
        var row = localTxId > 0
            ? await ConnectorStore.FindByTransactionAsync(db, stationId, localTxId)
            : null;
        int? connectorId       = conn?.ConnectorId ?? row?.ConnectorId;
        var  meterStartForStop = conn?.MeterStartWh ?? row?.MeterStartWh;

        var meterStopWh = state?.Adapter.ParseMeterValues(meterValues).EnergyWh
                          ?? MeterValueParser.Parse(meterValues).EnergyWh;

        conn?.EndTransaction(meterStopWh);
        if (row != null) await ConnectorStore.EndTransactionAsync(db, row);

        await SendCallResult(socket, messageId, new JObject());

        _ = RabbitMqPublisher.PublishTransactionStoppedAsync(
            stationId, localTxId, meterStopWh ?? 0, meterStartForStop, connectorId: connectorId);

        ChargingStationConnections.CompletePendingStopTransaction(stationId, localTxId);
    }

    // ── MeterValues (standalone, some 2.x chargers send both MeterValues + TransactionEvent) ─────

    private Task HandleMeterValues(
        WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
    {
        var evseId = payload["evseId"]?.Value<int?>() ?? 1;
        if (evseId == 0)   // main meter of the whole station — not a session
            return SendCallResult(socket, messageId, new JObject());
        return ProcessMeterAsync(socket, messageId, stationId, evseId, payload["meterValue"] as JArray, db);
    }

    /// <summary>Updates the live meter of one EVSE and publishes charger.meter.updated.</summary>
    private async Task ProcessMeterAsync(
        WebSocket socket, string messageId, string stationId, int evseId, JArray? meterValues, ChargingDBContext db)
    {
        var state    = ChargingStationConnections.Get(stationId);
        var conn     = state?.Connector(evseId);
        var readings = state?.Adapter.ParseMeterValues(meterValues)
                       ?? MeterValueParser.Parse(meterValues);

        if (!readings.EnergyWh.HasValue)
        {
            await SendCallResult(socket, messageId, new JObject());
            return;
        }

        // Restart recovery: restore MeterStartWh / transaction from DB if lost from in-memory state.
        if (conn != null && !conn.MeterStartWh.HasValue)
        {
            var row = await db.PlugConnectors.AsNoTracking()
                .FirstOrDefaultAsync(c => c.OcppId == stationId && c.ConnectorId == evseId);
            if (row?.MeterStartWh.HasValue == true)
            {
                conn.MeterStartWh = row.MeterStartWh;
                conn.LocalTxId  ??= row.TransactionId;
                OcppTrace.Dbg("OCPP21",
                    $"MeterStart recovered from DB after restart: {row.MeterStartWh} Wh for {stationId}#{evseId}");
            }
        }

        var now = DateTime.UtcNow;

        double? currentPowerKw = readings.PowerKw;
        if (currentPowerKw == null && conn?.MeterValueWh.HasValue == true && conn.LastMeterValueAt.HasValue)
        {
            var deltaWh    = (double)(readings.EnergyWh.Value - conn.MeterValueWh.Value);
            var deltaHours = (now - conn.LastMeterValueAt.Value).TotalHours;
            if (deltaHours > 0 && deltaWh > 0)
                currentPowerKw = Math.Round(deltaWh / 1000.0 / deltaHours, 2);
            else if (deltaWh == 0)
                currentPowerKw = conn.CurrentPowerKw;  // snapshot with no new energy — keep last known value
        }

        if (conn != null)
        {
            conn.MeterValueWh     = readings.EnergyWh.Value;
            conn.LastMeterValueAt = now;
            conn.CurrentPowerKw   = currentPowerKw;
            if (readings.SoC.HasValue)
                conn.StateOfCharge = readings.SoC;
        }

        _ = RabbitMqPublisher.PublishMeterUpdatedAsync(
            stationId, readings.EnergyWh.Value, conn?.MeterStartWh, currentPowerKw, readings.SoC,
            evseId, conn?.LocalTxId);

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
        var remoteStartId = ChargingStationConnections.NextTransactionId();

        var payload = new JObject
        {
            ["remoteStartId"] = remoteStartId,
            ["idToken"]       = new JObject { ["idToken"] = idTag, ["type"] = "ISO14443" }
        };
        if (evseId > 0)
            payload["evseId"] = evseId;

        return await SendCallAndWaitAsync(socket, "RequestStartTransaction", payload, _commandTimeout);
    }

    /// <summary>
    /// OCPP 2.x equivalent of RemoteStopTransaction.
    /// Looks up the OCPP 2.x string transactionId from the local int mapping.
    /// </summary>
    public async Task<JObject> SendStopCharging(WebSocket socket, string stationId, int? transactionId)
    {
        var ocppTxId = transactionId is { } local
            ? ChargingStationConnections.GetOcpp21TxId(stationId, local)
            : null;
        if (ocppTxId is null)
        {
            _tracer.Warning("RemoteStop",
                $"{stationId}: RequestStopTransaction — no active OCPP 2.x transaction {transactionId}",
                chargePointId: stationId);
            return new JObject { ["status"] = "Rejected" };
        }

        return await SendCallAndWaitAsync(socket, "RequestStopTransaction",
            new JObject { ["transactionId"] = ocppTxId },
            _commandTimeout);
    }

    // ── Outbound CSMS → charger commands ─────────────────────────────────────────

    /// <summary>
    /// OCPP 2.x TriggerMessage — same action name as 1.6 but connectorId maps to evse.id.
    /// </summary>
    public Task<JObject> SendTriggerMessage(WebSocket socket, string stationId, string requestedMessage, int? connectorId)
    {
        var payload = new JObject { ["requestedMessage"] = requestedMessage };
        if (connectorId.HasValue)
            payload["evse"] = new JObject { ["id"] = connectorId.Value };
        return SendCallAndWaitAsync(socket, "TriggerMessage", payload, _commandTimeout);
    }

    /// <summary>Requests a StatusNotification from an OCPP 2.x charger via TriggerMessage.</summary>
    public Task<JObject> SendStatusNotificationRequest(WebSocket socket, string stationId, int? connectorId)
    {
        var payload = new JObject { ["requestedMessage"] = "StatusNotification" };
        if (connectorId.HasValue)
            payload["evse"] = new JObject { ["id"] = connectorId.Value };
        return SendCallAndWaitAsync(socket, "TriggerMessage", payload, _commandTimeout);
    }

    /// <summary>
    /// GetDiagnostics does not exist in OCPP 2.x (superseded by GetLog).
    /// Returns Rejected immediately so the caller receives a clear signal.
    /// </summary>
    public Task<JObject> SendGetDiagnostics(WebSocket socket, string stationId, GetDiagnosticsRequest request)
    {
        _tracer.Warning("OCPP21", "SendGetDiagnostics is not supported on OCPP 2.x chargers — use GetLog");
        return Task.FromResult(new JObject { ["status"] = "Rejected" });
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
        using (cts.Token.Register(() =>
        {
            _pendingRequests.TryRemove(msgId, out _);
            tcs.TrySetCanceled();
        }))
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
        _tracer.Verbose("OCPP21", $"[→ OUT] {json}");
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

}
