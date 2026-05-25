/**
 * Communicator.cs — OCPP 1.6J message handler
 *
 * This is the core of the OCPP server. It handles all messages exchanged
 * between the server and physical chargers over WebSocket.
 *
 * OCPP 1.6J message format (JSON array):
 *   CALL        [2, messageId, action, payload]   — request from charger or server
 *   CALLRESULT  [3, messageId, payload]            — successful response
 *   CALLERROR   [4, messageId, code, description, details] — error response
 *
 * Incoming CALL messages from chargers are handled by Handle* methods:
 *   HandleBootNotification      — charger registers itself on connect/restart
 *   HandleHeartbeat             — periodic keep-alive (every ~5 min)
 *   HandleStatusNotification    — charger reports connector status change
 *   HandleStartTransaction      — charger confirms a charging session has started
 *   HandleStopTransaction       — charger confirms a charging session has ended
 *   HandleMeterValueNotification — periodic energy/power readings during charging
 *
 * Outgoing CALL messages we send to chargers:
 *   SendStartCharging  → RemoteStartTransaction
 *   SendStopCharging   → RemoteStopTransaction
 *
 * Pending requests (_pendingRequests) maps messageId → TaskCompletionSource so
 * we can await CALLRESULT responses asynchronously without blocking the receive loop.
 *
 * Callbacks to EVChargingApi:
 *   PushStatusToApi          — fires on StatusNotification → EVChargingApi → SignalR → app
 *   PushTransactionStartedToApi — fires on StartTransaction → stores OCPP transactionId
 */

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCPP_RD.OCPP1._6_Models;
using OCPPServer.ChargingStationInterface;
using OCPPServer.Data;
using OCPPServer.DataBase.DBModels;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

namespace OCPPServer.OCPP1._6_Models
{
    public class Communicator: ICommunicator
    {
        /// <summary>
        /// Pending requests waiting for a response from the charging station.
        /// </summary>
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _pendingRequests = new();

        // ── RabbitMQ event callbacks ───────────────────────────────────────────
        // All charger lifecycle events are now published to RabbitMQ instead of
        // calling EVChargingApi directly over HTTP. EVChargingApi's
        // RabbitMqConsumerService subscribes and pushes SignalR updates to the app.

        /// <summary>
        /// Publish a "charger offline" event when the charger's WebSocket closes.
        /// Called from Program.cs in the finally block of the WebSocket receive loop.
        /// </summary>
        public void PushDisconnect(string ocppId)
            => _ = RabbitMqPublisher.PublishStatusChangedAsync(
                ocppId, "Offline", connectorId: 0, isConnected: false);

        public async Task RouteOcppMessage(WebSocket socket, string connectorId, string json, ChargingDBContext db)
        {
            JArray message;
            try { message = JArray.Parse(json); }
            catch (Exception ex)
            {
                OcppTrace.Error("OCPP", $"[PARSE ERR] {connectorId}: failed to parse JSON — {ex.Message}");
                return;
            }

            int    messageType = message[0].Value<int>();
            string messageId   = message[1].Value<string>();

            // HANDLE RESPONSE (CALLRESULT / CALLERROR)
            if (messageType == OcppMessageType.CALLRESULT ||
                messageType == OcppMessageType.CALLERROR)
            {
                var typeLabel = messageType == OcppMessageType.CALLRESULT ? "CALLRESULT" : "CALLERROR";
                if (_pendingRequests.TryRemove(messageId, out var tcs))
                {
                    OcppTrace.Msg("OCPP", $"[← {typeLabel}] {connectorId} msgId={messageId} matched");
                    var payload = messageType == OcppMessageType.CALLRESULT
                        ? (JObject)message[2]
                        : new JObject
                        {
                            ["errorCode"]        = message[2],
                            ["errorDescription"] = message[3]
                        };
                    tcs.TrySetResult(payload);
                }
                else
                {
                    OcppTrace.Error("OCPP", $"[← {typeLabel}] {connectorId} msgId={messageId} — no matching pending request (timed out?)");
                }
                return;
            }

            if (messageType != OcppMessageType.CALL)
            {
                OcppTrace.Error("OCPP", $"[WARN] {connectorId}: unknown messageType={messageType}, ignoring");
                return;
            }

            string action       = message[2].Value<string>();
            JObject payloadCall = (JObject)message[3];
            OcppTrace.Msg("OCPP", $"[← CALL] {connectorId}: action={action} payload={payloadCall}");

            // Wrap in try/catch so a handler exception never crashes the WebSocket connection
            try
            {
                switch (action)
                {
                    case "BootNotification":
                        await HandleBootNotification(socket, messageId, payloadCall, connectorId, db);
                        break;

                    case "Heartbeat":
                        await HandleHeartbeat(socket, messageId, connectorId, db);
                        break;

                    case "StatusNotification":
                        await HandleStatusNotification(socket, messageId, payloadCall, connectorId, db);
                        break;

                    case "StartTransaction":
                        await HandleStartTransaction(socket, messageId, payloadCall, connectorId, db);
                        break;

                    case "StopTransaction":
                        await HandleStopTransaction(socket, messageId, payloadCall, connectorId, db);
                        break;

                    case "MeterValues":
                        await HandleMeterValueNotification(socket, messageId, payloadCall, connectorId, db);
                        break;

                    case "DiagnosticsStatusNotification":
                        await HandleDiagnosticsStatusNotification(socket, messageId, payloadCall, connectorId);
                        break;

                    default:
                        await SendCallError(socket, messageId, "NotSupported",
                            $"Action {action} not supported");
                        break;
                }
            }
            catch (Exception ex)
            {
                OcppTrace.Error("OCPP", $"[EXCEPTION] {connectorId} handling {action}: {ex.Message}");
                try { await SendCallError(socket, messageId, "InternalError", ex.Message); } catch { }
            }
        }

        public async Task HandleBootNotification(WebSocket socket, string messageId, JObject payload, string connectorId, ChargingDBContext db)
        {
            var req = payload.ToObject<BootNotificationRequest>();

            OcppTrace.Dbg("OCPP", $"BootNotification from {connectorId}: vendor={req.ChargePointVendor} model={req.ChargePointModel}");

            var existingConnector = await db.Plugs
                .FirstOrDefaultAsync(c => c.OcppId == connectorId);

            var negotiatedProtocol = ChargingStationConnections.GetProtocol(connectorId);

            if (existingConnector == null)
            {
                db.Plugs.Add(new Plug
                {
                    OcppId           = connectorId,
                    Vendor           = req.ChargePointVendor,
                    ChargePointModel = req.ChargePointModel,
                    ChargePointSN    = req.ChargePointSerialNumber ?? string.Empty,
                    FirmwareVersion  = req.FirmwareVersion,
                    SIMNr            = req.Iccid,
                    OcppVersion      = negotiatedProtocol,
                    Status           = Enumerators.ChargePointStatus.Available,
                    IsOnline         = true,
                    MeterValue       = 0m,
                    CreatedAt        = DateTime.UtcNow,
                    LastStatusUpdate = DateTime.UtcNow,
                });
            }
            else
            {
                // Only overwrite a field when the charger sends a non-empty value —
                // preserves admin-entered data if the charger omits optional fields.
                if (!string.IsNullOrEmpty(req.ChargePointVendor))
                    existingConnector.Vendor = req.ChargePointVendor;
                if (!string.IsNullOrEmpty(req.ChargePointModel))
                    existingConnector.ChargePointModel = req.ChargePointModel;
                if (!string.IsNullOrEmpty(req.ChargePointSerialNumber))
                    existingConnector.ChargePointSN = req.ChargePointSerialNumber;
                if (!string.IsNullOrEmpty(req.FirmwareVersion))
                    existingConnector.FirmwareVersion = req.FirmwareVersion;
                if (!string.IsNullOrEmpty(req.Iccid))
                    existingConnector.SIMNr = req.Iccid;

                existingConnector.OcppVersion      = negotiatedProtocol;
                existingConnector.Status           = Enumerators.ChargePointStatus.Available;
                existingConnector.IsOnline         = true;
                existingConnector.LastStatusUpdate = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            var responsePayload = new JObject
            {
                ["status"] = "Accepted",  ///TODO: implement logic for status
                ["currentTime"] = DateTime.UtcNow.ToString("o"),
                ["interval"] = 300
            };

            await SendCallResult(socket, messageId, responsePayload);
        }

        public async Task HandleHeartbeat(WebSocket socket, string messageId, string stationId, ChargingDBContext db)
        {
            OcppTrace.Dbg("OCPP", "Heartbeat received");

            var plug = await db.Plugs.FirstOrDefaultAsync(c => c.OcppId == stationId);
            if (plug != null)
            {
                plug.LastStatusUpdate = DateTime.UtcNow;
                plug.IsOnline = true; // assume online if heartbeat received
                await db.SaveChangesAsync();
            }

            var payload = new JObject
            {
                ["currentTime"] = DateTime.UtcNow.ToString("o")
            };

            await SendCallResult(socket, messageId, payload);  // ← was "messageId" (string literal)
        }

        public async Task HandleStatusNotification(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            // Parse status as a plain string — avoids enum deserialization exceptions for
            // values the charger sends that we might not have in the enum yet.
            var connectorId = payload["connectorId"]?.Value<int>() ?? 0;
            var statusStr   = payload["status"]?.Value<string>() ?? "";
            var errorCode   = payload["errorCode"]?.Value<string>() ?? "";

            OcppTrace.Msg("OCPP", $"StatusNotification {stationId} connectorId={connectorId} status={statusStr} errorCode={errorCode}");

            // Only update DB for the physical connector (connectorId > 0); connectorId=0 is the charger controller
            if (connectorId == 0)
            {
                await SendCallResult(socket, messageId, new JObject());
                return;
            }

            var existingConnector = await db.Plugs
                .FirstOrDefaultAsync(c => c.OcppId == stationId);

            if (existingConnector != null)
            {
                if (Enum.TryParse<Enumerators.ChargePointStatus>(statusStr, out var parsedStatus))
                    existingConnector.Status = parsedStatus;
                else
                    OcppTrace.Error("OCPP", $"[WARN] Unknown ChargePointStatus '{statusStr}' for {stationId} — DB status not changed");

                existingConnector.LastStatusUpdate = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            else
            {
                OcppTrace.Error("OCPP", $"[WARN] StatusNotification from unknown stationId '{stationId}' — ignoring");
            }

            // Publish status change event → RabbitMQ → EVChargingApi → SignalR → app
            _ = RabbitMqPublisher.PublishStatusChangedAsync(stationId, statusStr, connectorId, isConnected: true);

            await SendCallResult(socket, messageId, new JObject());
        }

        public async Task HandleStartTransaction(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            var transactionId = ChargingStationConnections.AssignTransaction(stationId);
            OcppTrace.Msg("OCPP", $"StartTransaction {stationId} assigned transactionId={transactionId}");

            // Save meter reading at transaction start
            var meterStartRaw = payload["meterStart"]?.Value<decimal?>();
            if (meterStartRaw.HasValue)
            {
                // Store MeterStart in-memory — no longer persisted in OCPP DB.
                // EVChargingApi will persist it in ChargingSession.MeterStart via RabbitMQ.
                ChargingStationConnections.SetMeterStart(stationId, meterStartRaw.Value);

                var connector = await db.Plugs.FirstOrDefaultAsync(c => c.OcppId == stationId);
                if (connector != null)
                {
                    connector.MeterValue       = meterStartRaw.Value;
                    connector.LastMeterValueAt = null;
                    connector.LastStatusUpdate = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    OcppTrace.Dbg("OCPP", $"MeterStart={meterStartRaw.Value} Wh (in-memory) for {stationId}");
                }
            }

            var response = new JObject
            {
                ["transactionId"] = transactionId,
                ["idTagInfo"] = new JObject { ["status"] = "Accepted" }
            };
            await SendCallResult(socket, messageId, response);

            // Signal any OcppCommandHandler waiting for this StartTransaction
            // (RemoteStart flow: command handler waits here before publishing RemoteStartResponse)
            ChargingStationConnections.CompletePendingStartTransaction(stationId, transactionId);

            // Publish event → RabbitMQ → EVChargingApi stores transactionId + MeterStart on the session
            _ = RabbitMqPublisher.PublishTransactionStartedAsync(stationId, transactionId, meterStartRaw);
        }

        public async Task HandleStopTransaction(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            OcppTrace.Msg("OCPP", $"StopTransaction {stationId}: keys=[{string.Join(", ", payload.Properties().Select(p => p.Name))}] meterStop={payload["meterStop"]}]");
            var stoppedTxId = payload["transactionId"]?.Value<int?>() ?? 0;
            ChargingStationConnections.ClearTransaction(stationId);

            // Save meter reading at transaction end
            var meterStopRaw = payload["meterStop"]?.Value<decimal?>();
            // Read MeterStart from in-memory cache before clearing it
            var meterStartForStop = ChargingStationConnections.GetMeterStart(stationId);

            if (meterStopRaw.HasValue)
            {
                var connector = await db.Plugs.FirstOrDefaultAsync(c => c.OcppId == stationId);
                if (connector != null)
                {
                    connector.MeterValue       = meterStopRaw.Value;
                    connector.LastMeterValueAt = null;
                    connector.LastStatusUpdate = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    var consumed = meterStartForStop.HasValue
                        ? $"{(meterStopRaw.Value - meterStartForStop.Value) / 1000m:F3} kWh consumed"
                        : "no MeterStart on record";
                    OcppTrace.Dbg("OCPP", $"MeterStop={meterStopRaw.Value} Wh for {stationId} — {consumed}");
                }
            }

            // Clear in-memory MeterStart — session is over
            ChargingStationConnections.ClearMeterStart(stationId);

            await SendCallResult(socket, messageId, new JObject
            {
                ["idTagInfo"] = new JObject { ["status"] = "Accepted" }
            });

            // Signal any OcppCommandHandler waiting for this StopTransaction
            ChargingStationConnections.CompletePendingStopTransaction(stationId, stoppedTxId);

            // Publish StopTransaction event — EVChargingApi will finalize the session
            // and push SessionFinalized via SignalR to the app.
            if (meterStopRaw.HasValue)
            {
                _ = RabbitMqPublisher.PublishTransactionStoppedAsync(
                    stationId, stoppedTxId, meterStopRaw.Value, meterStartForStop);
            }
        }

        public async Task HandleMeterValueNotification(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            OcppTrace.Msg("OCPP", $"MeterValues {stationId}: {payload}");

            // Parse raw JTokens — avoids enum deserialization issues with strings like
            // "Energy.Active.Import.Register" that don't map to C# enum names.
            var meterValues = payload["meterValue"] as JArray;
            if (meterValues == null)
            {
                OcppTrace.Error("OCPP", $"[WARN] MeterValues {stationId}: no meterValue array in payload");
                await SendCallResult(socket, messageId, new JObject());
                return;
            }

            var readings = MeterValueParser.Parse(meterValues);

            if (!readings.EnergyWh.HasValue)
            {
                OcppTrace.Error("OCPP", $"[WARN] MeterValues {stationId}: no Energy measurand found in sampled values");
                await SendCallResult(socket, messageId, new JObject());
                return;
            }

            var connector = await db.Plugs.FirstOrDefaultAsync(c => c.OcppId == stationId);
            if (connector == null)
            {
                OcppTrace.Error("OCPP", $"[WARN] MeterValues {stationId}: plug not found in DB — ignoring");
                await SendCallResult(socket, messageId, new JObject());
                return;
            }

            var now = DateTime.UtcNow;

            // Prefer power reported directly by the charger; fall back to calculated (ΔWh/Δh)
            double? currentPowerKw = readings.PowerKw;
            if (currentPowerKw == null && connector.MeterValue.HasValue && connector.LastMeterValueAt.HasValue)
            {
                var deltaWh    = (double)(readings.EnergyWh.Value - connector.MeterValue.Value);
                var deltaHours = (now - connector.LastMeterValueAt.Value).TotalHours;
                if (deltaHours > 0 && deltaWh >= 0)
                    currentPowerKw = Math.Round(deltaWh / 1000.0 / deltaHours, 2);
            }

            connector.MeterValue       = readings.EnergyWh.Value;
            connector.LastMeterValueAt = now;
            connector.LastStatusUpdate = now;
            if (readings.SoC.HasValue)
                connector.StateOfCharge = readings.SoC;
            await db.SaveChangesAsync();
            OcppTrace.Dbg("OCPP", $"Meter stored: {readings.EnergyWh.Value} Wh, power={currentPowerKw} kW, SoC={readings.SoC} % for {stationId}");

            _ = RabbitMqPublisher.PublishMeterUpdatedAsync(
                stationId,
                readings.EnergyWh.Value,
                ChargingStationConnections.GetMeterStart(stationId),
                currentPowerKw,
                readings.SoC);

            await SendCallResult(socket, messageId, new JObject());
        }

        public async Task<JObject> SendStartCharging(WebSocket socket, string stationId, int connectorId, string idTag)
        {
            var payload = new JObject
            {
                ["connectorId"] = connectorId,
                ["idTag"] = idTag
            };

            return await SendCallAndWaitAsync(socket, "RemoteStartTransaction", payload, TimeSpan.FromSeconds(30));
        }

        public async Task<JObject> SendStopCharging(WebSocket socket, string stationId, int? transactionId = null)
        {
            if (transactionId is null)
            {
                OcppTrace.Error("OCPP", $"[→ OUT] {stationId}: RemoteStopTransaction skipped — transactionId is null");
                return new JObject { ["status"] = "Rejected" };
            }

            OcppTrace.Msg("OCPP", $"[→ OUT] {stationId}: sending RemoteStopTransaction transactionId={transactionId}");

            var payload = new JObject
            {
                ["transactionId"] = transactionId.Value
            };

            var response = await SendCallAndWaitAsync(
                socket,
                "RemoteStopTransaction",
                payload,
                TimeSpan.FromSeconds(30));

            return response;
        }

        public async Task SendCallResult(WebSocket socket, string messageId, JObject payload)
        {
            var response = new JArray
            {
                OcppMessageType.CALLRESULT,
                messageId,
                payload
            };
            await SendAsync(socket, response);
        }

        /// <summary>
        /// Sends a CALLERROR message through the WebSocket
        /// </summary>
        public async Task SendCallError(WebSocket socket, string messageId, string errorCode, string errorDescription)
        {
            var response = new JArray
            {
                OcppMessageType.CALLERROR,
                messageId,
                errorCode,
                errorDescription,
                new JObject()
            };

            await SendAsync(socket, response);
        }

        /// <summary>
        /// Help method to send a CALL message and wait for the response asynchronously
        /// </summary>
        public async Task<JObject> SendCallAndWaitAsync(WebSocket socket, string action, JObject payload, TimeSpan timeout)
        {
            var messageId = Guid.NewGuid().ToString();

            var tcs = new TaskCompletionSource<JObject>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            _pendingRequests[messageId] = tcs;

            var call = new JArray
            {
                OcppMessageType.CALL,
                messageId,
                action,
                payload
            };

            await SendAsync(socket, call);

            using var cts = new CancellationTokenSource(timeout);
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                return await tcs.Task; // ✅ async, non-blocking
            }
        }


        /// <summary>
        /// Sends a message through the WebSocket
        /// </summary>
        public async Task SendAsync(WebSocket socket, JArray message)
        {
            var json  = message.ToString(Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(json);

            OcppTrace.Msg("OCPP", $"[→ OUT] {json}");

            await socket.SendAsync(
                bytes,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }

        public async Task<JObject> SendTriggerMessage(WebSocket socket, string requestedMessage, int? connectorId)
        {
            if (!Enum.TryParse<RequestedMessage>(requestedMessage, ignoreCase: true, out _))
                throw new ArgumentException($"Unknown requestedMessage: '{requestedMessage}'");

            var payload = new JObject { ["requestedMessage"] = requestedMessage };
            if (connectorId.HasValue)
                payload["connectorId"] = connectorId.Value;

            return await SendCallAndWaitAsync(socket, "TriggerMessage", payload, TimeSpan.FromSeconds(30));
        }

        public async Task<JObject> SendStatusNotificationRequest(WebSocket socket, int? connectorId)
        {
            var payload = new JObject { ["requestedMessage"] = "StatusNotification" };
            if (connectorId.HasValue)
                payload["connectorId"] = connectorId.Value;

            return await SendCallAndWaitAsync(socket, "TriggerMessage", payload, TimeSpan.FromSeconds(30));
        }

        public async Task<JObject> SendGetDiagnostics(WebSocket socket, GetDiagnosticsRequest request)
        {
            var payload = new JObject { ["location"] = request.Location };
            if (request.Retries.HasValue)       payload["retries"]       = request.Retries.Value;
            if (request.RetryInterval.HasValue) payload["retryInterval"] = request.RetryInterval.Value;
            if (request.StartTime.HasValue)     payload["startTime"]     = request.StartTime.Value.ToString("o");
            if (request.StopTime.HasValue)      payload["stopTime"]      = request.StopTime.Value.ToString("o");

            return await SendCallAndWaitAsync(socket, "GetDiagnostics", payload, TimeSpan.FromSeconds(30));
        }

        public async Task HandleDiagnosticsStatusNotification(WebSocket socket, string messageId, JObject payload, string stationId)
        {
            var statusStr = payload["status"]?.Value<string>() ?? "";
            OcppTrace.Msg("OCPP", $"DiagnosticsStatusNotification {stationId}: status={statusStr}");

            await SendCallResult(socket, messageId, new JObject());

            _ = RabbitMqPublisher.PublishDiagnosticsStatusAsync(stationId, statusStr);
        }
    }
}
