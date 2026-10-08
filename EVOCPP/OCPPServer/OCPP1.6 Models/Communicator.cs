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
using OCPPServer.ChargerAdapters;
using OCPPServer.ChargingStationInterface;
using OCPPServer.Data;
using OCPPServer.DataBase.DBModels;
using OCPPServer.Tracing;
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
        private readonly ITracingService _tracer;
        private readonly TimeSpan        _commandTimeout;
        private readonly TimeSpan        _authorizeTimeout;

        public Communicator(ITracingService tracer, IConfiguration config)
        {
            _tracer           = tracer;
            _commandTimeout   = TimeSpan.FromSeconds(config.GetValue("Ocpp:CommandTimeoutSeconds",   30));
            _authorizeTimeout = TimeSpan.FromSeconds(config.GetValue("Ocpp:AuthorizeTimeoutSeconds", 10));
        }

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
                _tracer.Warning("OCPP", $"{connectorId}: failed to parse JSON — {ex.Message}");
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
                    _tracer.Verbose("OCPP", $"[← {typeLabel}] {connectorId} msgId={messageId} matched", chargePointId: connectorId);
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
                    _tracer.Warning("OCPP", $"[← {typeLabel}] {connectorId} msgId={messageId} — no matching pending request (timed out?)", chargePointId: connectorId);
                }
                return;
            }

            if (messageType != OcppMessageType.CALL)
            {
                _tracer.Warning("OCPP", $"{connectorId}: unknown messageType={messageType}, ignoring");
                return;
            }

            string action       = message[2].Value<string>();
            JObject payloadCall = (JObject)message[3];
            _tracer.Verbose("OCPP", $"[← CALL] {connectorId}: action={action} payload={payloadCall}", chargePointId: connectorId);

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

                    case "Authorize":
                        await HandleAuthorize(socket, messageId, payloadCall, connectorId);
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
                _tracer.Exception("OCPP", ex, $"{connectorId} handling {action}", chargePointId: connectorId);
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
                    CreatedAt        = DateTime.UtcNow,
                    LastStatusUpdate = DateTime.UtcNow,
                    LastHeartbeatAt  = DateTime.UtcNow,
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
                existingConnector.LastHeartbeatAt  = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            // Update vendor-specific adapter so subsequent meter/boot messages are parsed correctly.
            var state = ChargingStationConnections.Get(connectorId);
            if (state != null)
            {
                state.Adapter = ChargerAdapterFactory.Create(req.ChargePointVendor);
                state.Adapter.OnBootNotification(payload);
            }

            var responsePayload = new JObject
            {
                ["status"] = "Accepted",  ///TODO: implement logic for status
                ["currentTime"] = DateTime.UtcNow.ToString("o"),
                ["interval"] = 60
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
                plug.LastHeartbeatAt  = DateTime.UtcNow;
                plug.IsOnline         = true;
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

            _tracer.Info("StatusNotification", $"{stationId} connectorId={connectorId} status={statusStr} errorCode={errorCode}",
                chargePointId: stationId);

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
                if (Enum.TryParse<Enumerators.ChargePointStatus>(statusStr, ignoreCase: true, out var parsedStatus))
                    existingConnector.Status = parsedStatus;
                else
                    _tracer.Warning("StatusNotification",
                        $"{stationId}: unknown ChargePointStatus '{statusStr}' — DB status not changed",
                        chargePointId: stationId);

                existingConnector.LastStatusUpdate = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            else
            {
                _tracer.Warning("StatusNotification",
                    $"No Plug record for '{stationId}' — status not persisted");
            }

            // Include car ID (EVCC ID) when status transitions to Preparing —
            // this is the moment a vehicle connects and the charger is about to start a session.
            var state16 = ChargingStationConnections.Get(stationId);
            string? carId = null;
            if (statusStr == "Preparing")
                carId = state16?.CarId;
            else if (statusStr == "Available" && state16 != null)
                state16.CarId = null;   // vehicle disconnected — clear for next session

            _ = RabbitMqPublisher.PublishStatusChangedAsync(stationId, statusStr, connectorId, isConnected: true, carId);

            await SendCallResult(socket, messageId, new JObject());
        }

        public async Task HandleAuthorize(WebSocket socket, string messageId, JObject payload, string stationId)
        {
            var idTag = payload["idTag"]?.Value<string>() ?? "";
            OcppTrace.Dbg("OCPP", $"Authorize {stationId}: idTag={idTag}");

            // EVCC ID is signaled by the "VID:" prefix (ISO 15118 whitepaper convention).
            // For EVCC sessions: ask the backend and wait for its decision.
            // For regular RFID/app tokens: accept immediately — no backend round-trip needed.
            if (idTag.StartsWith("VID:", StringComparison.OrdinalIgnoreCase))
            {
                var carId = idTag[4..];
                var state = ChargingStationConnections.Get(stationId);
                if (state != null) state.CarId = carId;

                _ = RabbitMqPublisher.PublishAuthorizeRequestedAsync(stationId, carId);

                var tcs        = ChargingStationConnections.RegisterPendingAuthorize(stationId);
                var authStatus = "Rejected"; // fail closed — backend silence = reject

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
                    ["idTagInfo"] = new JObject { ["status"] = authStatus }
                });
            }
            else
            {
                // Regular RFID / app token — accept immediately
                await SendCallResult(socket, messageId, new JObject
                {
                    ["idTagInfo"] = new JObject { ["status"] = "Accepted" }
                });
            }
        }

        public async Task HandleStartTransaction(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            var transactionId = ChargingStationConnections.NextTransactionId();
            _tracer.Info("StartTransaction", $"{stationId}: transactionId={transactionId}",
                chargePointId: stationId, sessionId: transactionId);

            var meterStartRaw = payload["meterStart"]?.Value<decimal?>();
            var idTag = payload["idTag"]?.Value<string>() ?? "";

            var state = ChargingStationConnections.Get(stationId);

            // Fallback: charger may not send Authorize.req but still carry the EVCC ID here
            if (state != null && state.CarId is null
                && idTag.StartsWith("VID:", StringComparison.OrdinalIgnoreCase))
            {
                state.CarId = idTag[4..];
                OcppTrace.Dbg("OCPP", $"CarId from StartTransaction for {stationId}: {state.CarId}");
            }
            if (state != null && meterStartRaw.HasValue)
            {
                state.MeterStartWh     = meterStartRaw.Value;
                state.MeterValueWh     = meterStartRaw.Value;
                state.LastMeterValueAt = null;
                state.LocalTxId        = transactionId;
                OcppTrace.Dbg("OCPP", $"MeterStart={meterStartRaw.Value} Wh stored in-memory for {stationId}");
            }

            // Persist MeterStartWh to DB so it survives an OCPP server restart.
            // MeterValues handlers recover state.MeterStartWh from this column on restart.
            var plugStart = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == stationId);
            if (plugStart != null)
            {
                plugStart.MeterStartWh = meterStartRaw;
                await db.SaveChangesAsync();
            }

            await SendCallResult(socket, messageId, new JObject
            {
                ["transactionId"] = transactionId,
                ["idTagInfo"]     = new JObject { ["status"] = "Accepted" }
            });

            ChargingStationConnections.CompletePendingStartTransaction(stationId, transactionId);
            _ = RabbitMqPublisher.PublishTransactionStartedAsync(stationId, transactionId, meterStartRaw);
        }

        public async Task HandleStopTransaction(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            var stoppedTxId  = payload["transactionId"]?.Value<int?>() ?? 0;
            var meterStopRaw = payload["meterStop"]?.Value<decimal?>();
            _tracer.Info("StopTransaction",
                $"{stationId}: txId={stoppedTxId} meterStop={meterStopRaw}",
                chargePointId: stationId, sessionId: stoppedTxId);

            var state = ChargingStationConnections.Get(stationId);
            var meterStartForStop = state?.MeterStartWh;

            if (state != null)
            {
                if (meterStopRaw.HasValue)
                {
                    var consumed = meterStartForStop.HasValue
                        ? $"{(meterStopRaw.Value - meterStartForStop.Value) / 1000m:F3} kWh consumed"
                        : "no MeterStart on record";
                    OcppTrace.Dbg("OCPP", $"MeterStop={meterStopRaw.Value} Wh for {stationId} — {consumed}");
                }

                state.MeterStartWh     = null;
                state.MeterValueWh     = meterStopRaw;
                state.LastMeterValueAt = null;
                state.CurrentPowerKw   = null;
                state.StateOfCharge    = null;
                state.LocalTxId        = null;
            }

            // Clear persisted MeterStartWh — session is over.
            var plugStop = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == stationId);
            if (plugStop != null)
            {
                plugStop.MeterStartWh = null;
                await db.SaveChangesAsync();
            }

            await SendCallResult(socket, messageId, new JObject
            {
                ["idTagInfo"] = new JObject { ["status"] = "Accepted" }
            });

            ChargingStationConnections.CompletePendingStopTransaction(stationId, stoppedTxId);

            if (meterStopRaw.HasValue)
                _ = RabbitMqPublisher.PublishTransactionStoppedAsync(
                    stationId, stoppedTxId, meterStopRaw.Value, meterStartForStop);
        }

        public async Task HandleMeterValueNotification(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            OcppTrace.Msg("OCPP", $"MeterValues {stationId}: {payload}");

            var meterValues = payload["meterValue"] as JArray;
            if (meterValues == null)
            {
                _tracer.Warning("MeterValues", $"{stationId}: no meterValue array in payload",
                    chargePointId: stationId);
                await SendCallResult(socket, messageId, new JObject());
                return;
            }

            var state = ChargingStationConnections.Get(stationId);
            var readings = state?.Adapter.ParseMeterValues(meterValues)
                           ?? MeterValueParser.Parse(meterValues);

            if (!readings.EnergyWh.HasValue)
            {
                _tracer.Warning("MeterValues", $"{stationId}: no Energy measurand found in sampled values",
                    chargePointId: stationId);
                await SendCallResult(socket, messageId, new JObject());
                return;
            }

            // Restart recovery: if MeterStartWh was lost from in-memory state (OCPP server
            // restart while a session was active), restore it from the persisted DB value so
            // PublishMeterUpdatedAsync carries a valid baseline instead of null.
            if (state != null && !state.MeterStartWh.HasValue)
            {
                var plugRecover = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == stationId);
                if (plugRecover?.MeterStartWh.HasValue == true)
                {
                    state.MeterStartWh = plugRecover.MeterStartWh;
                    OcppTrace.Dbg("OCPP",
                        $"MeterStart recovered from DB after restart: {plugRecover.MeterStartWh} Wh for {stationId}");
                }
            }

            var now = DateTime.UtcNow;

            // Prefer power reported directly; fall back to ΔWh/Δh from previous reading
            double? currentPowerKw = readings.PowerKw;
            if (currentPowerKw == null && state?.MeterValueWh.HasValue == true && state.LastMeterValueAt.HasValue)
            {
                var deltaWh    = (double)(readings.EnergyWh.Value - state.MeterValueWh.Value);
                var deltaHours = (now - state.LastMeterValueAt.Value).TotalHours;
                if (deltaHours > 0 && deltaWh >= 0)
                    currentPowerKw = Math.Round(deltaWh / 1000.0 / deltaHours, 2);
            }

            if (state != null)
            {
                state.MeterValueWh     = readings.EnergyWh.Value;
                state.LastMeterValueAt = now;
                state.CurrentPowerKw   = currentPowerKw;
                if (readings.SoC.HasValue)
                    state.StateOfCharge = readings.SoC;
            }

            OcppTrace.Dbg("OCPP", $"Meter: {readings.EnergyWh.Value} Wh, power={currentPowerKw} kW, SoC={readings.SoC} % for {stationId}");

            _ = RabbitMqPublisher.PublishMeterUpdatedAsync(
                stationId, readings.EnergyWh.Value, state?.MeterStartWh, currentPowerKw, readings.SoC);

            await SendCallResult(socket, messageId, new JObject());
        }

        public async Task<JObject> SendStartCharging(WebSocket socket, string stationId, int connectorId, string idTag)
        {
            var payload = new JObject
            {
                ["connectorId"] = connectorId,
                ["idTag"] = idTag
            };

            return await SendCallAndWaitAsync(socket, "RemoteStartTransaction", payload, _commandTimeout);
        }

        public async Task<JObject> SendStopCharging(WebSocket socket, string stationId, int? transactionId = null)
        {
            if (transactionId is null)
            {
                _tracer.Warning("RemoteStop", $"{stationId}: RemoteStopTransaction skipped — transactionId is null",
                    chargePointId: stationId);
                return new JObject { ["status"] = "Rejected" };
            }

            _tracer.Verbose("OCPP", $"[→ OUT] {stationId}: sending RemoteStopTransaction transactionId={transactionId}", chargePointId: stationId);

            var payload = new JObject
            {
                ["transactionId"] = transactionId.Value
            };

            var response = await SendCallAndWaitAsync(
                socket,
                "RemoteStopTransaction",
                payload,
                _commandTimeout);

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
            using (cts.Token.Register(() =>
            {
                _pendingRequests.TryRemove(messageId, out _);
                tcs.TrySetCanceled();
            }))
            {
                return await tcs.Task;
            }
        }


        /// <summary>
        /// Sends a message through the WebSocket
        /// </summary>
        public async Task SendAsync(WebSocket socket, JArray message)
        {
            var json  = message.ToString(Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(json);

            _tracer.Verbose("OCPP", $"[→ OUT] {json}");

            await socket.SendAsync(
                bytes,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }

        public async Task<JObject> SendTriggerMessage(WebSocket socket, string stationId, string requestedMessage, int? connectorId)
        {
            if (!Enum.TryParse<RequestedMessage>(requestedMessage, ignoreCase: true, out _))
                throw new ArgumentException($"Unknown requestedMessage: '{requestedMessage}'");

            var payload = new JObject { ["requestedMessage"] = requestedMessage };
            if (connectorId.HasValue)
                payload["connectorId"] = connectorId.Value;

            return await SendCallAndWaitAsync(socket, "TriggerMessage", payload, _commandTimeout);
        }

        public async Task<JObject> SendStatusNotificationRequest(WebSocket socket, string stationId, int? connectorId)
        {
            var payload = new JObject { ["requestedMessage"] = "StatusNotification" };
            if (connectorId.HasValue)
                payload["connectorId"] = connectorId.Value;

            return await SendCallAndWaitAsync(socket, "TriggerMessage", payload, _commandTimeout);
        }

        public async Task<JObject> SendGetDiagnostics(WebSocket socket, string stationId, GetDiagnosticsRequest request)
        {
            var payload = new JObject { ["location"] = request.Location };
            if (request.Retries.HasValue)       payload["retries"]       = request.Retries.Value;
            if (request.RetryInterval.HasValue) payload["retryInterval"] = request.RetryInterval.Value;
            if (request.StartTime.HasValue)     payload["startTime"]     = request.StartTime.Value.ToString("o");
            if (request.StopTime.HasValue)      payload["stopTime"]      = request.StopTime.Value.ToString("o");

            return await SendCallAndWaitAsync(socket, "GetDiagnostics", payload, _commandTimeout);
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
