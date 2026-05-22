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

namespace OCPPServer.OCPP1._6_Models
{
    public class Communicator
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
        public static void PushDisconnect(string ocppId)
            => _ = RabbitMqPublisher.PublishStatusChangedAsync(
                ocppId, "Offline", connectorId: 0, isConnected: false);

        public static async Task RouteOcppMessage(WebSocket socket, string stationId, string json, ChargingDBContext db)
        {
            JArray message;
            try { message = JArray.Parse(json); }
            catch (Exception ex)
            {
                OcppTrace.Error("OCPP", $"[PARSE ERR] {stationId}: failed to parse JSON — {ex.Message}");
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
                    OcppTrace.Msg("OCPP", $"[← {typeLabel}] {stationId} msgId={messageId} matched");
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
                    OcppTrace.Error("OCPP", $"[← {typeLabel}] {stationId} msgId={messageId} — no matching pending request (timed out?)");
                }
                return;
            }

            if (messageType != OcppMessageType.CALL)
            {
                OcppTrace.Error("OCPP", $"[WARN] {stationId}: unknown messageType={messageType}, ignoring");
                return;
            }

            string action       = message[2].Value<string>();
            JObject payloadCall = (JObject)message[3];
            OcppTrace.Msg("OCPP", $"[← CALL] {stationId}: action={action} payload={payloadCall}");

            // Wrap in try/catch so a handler exception never crashes the WebSocket connection
            try
            {
                switch (action)
                {
                    case "BootNotification":
                        await HandleBootNotification(socket, messageId, payloadCall, stationId, db);
                        break;

                    case "Heartbeat":
                        await HandleHeartbeat(socket, messageId);
                        break;

                    case "StatusNotification":
                        await HandleStatusNotification(socket, messageId, payloadCall, stationId, db);
                        break;

                    case "StartTransaction":
                        await HandleStartTransaction(socket, messageId, payloadCall, stationId, db);
                        break;

                    case "StopTransaction":
                        await HandleStopTransaction(socket, messageId, payloadCall, stationId, db);
                        break;

                    case "MeterValues":
                        await HandleMeterValueNotification(socket, messageId, payloadCall, stationId, db);
                        break;

                    default:
                        await SendCallError(socket, messageId, "NotSupported",
                            $"Action {action} not supported");
                        break;
                }
            }
            catch (Exception ex)
            {
                OcppTrace.Error("OCPP", $"[EXCEPTION] {stationId} handling {action}: {ex.Message}");
                try { await SendCallError(socket, messageId, "InternalError", ex.Message); } catch { }
            }
        }

        static async Task HandleBootNotification(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            var req = payload.ToObject<BootNotificationRequest>();

            OcppTrace.Dbg("OCPP", $"BootNotification from {stationId}: vendor={req.ChargePointVendor} model={req.ChargePointModel}");

            var existingConnector = await db.Connectors
                .FirstOrDefaultAsync(c => c.OcppId == stationId);

            if (existingConnector == null)
            {
                // Create new
                var station = new Connector
                {
                    Vendor = req.ChargePointVendor,
                    ChargePointModel = req.ChargePointModel,
                    ChargePointSN = req.ChargePointSerialNumber,
                    ChargerSN = req.ChargeBoxSerialNumber ?? string.Empty,
                    FirmwareVersion = req.FirmwareVersion,
                    SIMNr = req.Iccid,
                    OcppId = stationId,
                    Status = Enumerators.ChargePointStatus.Available,
                    MeterValue = 0m,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdate = DateTime.UtcNow
                };

                db.Connectors.Add(station);
            }
            else
            {
                // Update existing
                existingConnector.Vendor = req.ChargePointVendor;
                existingConnector.ChargePointModel = req.ChargePointModel;
                existingConnector.ChargePointSN = req.ChargePointSerialNumber;
                existingConnector.ChargerSN = req.ChargeBoxSerialNumber ?? existingConnector.ChargerSN;
                existingConnector.FirmwareVersion = req.FirmwareVersion;
                existingConnector.SIMNr = req.Iccid;
                existingConnector.Status = Enumerators.ChargePointStatus.Available;
                existingConnector.LastUpdate = DateTime.UtcNow;
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

        static async Task HandleHeartbeat(WebSocket socket, string messageId)
        {
            OcppTrace.Dbg("OCPP", "Heartbeat received");
            var payload = new JObject
            {
                ["currentTime"] = DateTime.UtcNow.ToString("o")
            };

            await SendCallResult(socket, messageId, payload);
        }

        static async Task HandleStatusNotification(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
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

            var existingConnector = await db.Connectors
                .FirstOrDefaultAsync(c => c.OcppId == stationId);

            if (existingConnector != null)
            {
                if (Enum.TryParse<Enumerators.ChargePointStatus>(statusStr, out var parsedStatus))
                    existingConnector.Status = parsedStatus;
                else
                    OcppTrace.Error("OCPP", $"[WARN] Unknown ChargePointStatus '{statusStr}' for {stationId} — DB status not changed");

                existingConnector.LastUpdate = DateTime.UtcNow;
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

        static async Task HandleStartTransaction(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
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

                var connector = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == stationId);
                if (connector != null)
                {
                    connector.MeterValue          = meterStartRaw.Value; // reset so energyKwh = 0 at session start
                    connector.SessionStartedAt    = DateTime.UtcNow;
                    connector.ActiveTransactionId = transactionId;       // persist so Stop survives restarts
                    connector.CurrentPowerKw      = null;               // no reading yet
                    connector.LastMeterValueAt    = null;
                    connector.LastUpdate          = DateTime.UtcNow;
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

            // Publish event → RabbitMQ → EVChargingApi stores transactionId + MeterStart on the session
            _ = RabbitMqPublisher.PublishTransactionStartedAsync(stationId, transactionId, meterStartRaw);
        }

        static async Task HandleStopTransaction(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
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
                var connector = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == stationId);
                if (connector != null)
                {
                    // MeterStop is not persisted in OCPP DB — EVChargingApi stores it in ChargingSession
                    connector.MeterValue          = meterStopRaw.Value; // sync live reading to final value
                    connector.SessionStartedAt    = null;               // session over
                    connector.ActiveTransactionId = null;
                    connector.CurrentPowerKw      = null;
                    connector.LastMeterValueAt    = null;
                    connector.LastUpdate          = DateTime.UtcNow;
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

            // Publish StopTransaction event — EVChargingApi will finalize the session
            // and push SessionFinalized via SignalR to the app.
            if (meterStopRaw.HasValue)
            {
                _ = RabbitMqPublisher.PublishTransactionStoppedAsync(
                    stationId, stoppedTxId, meterStopRaw.Value, meterStartForStop);
            }
        }

        static async Task HandleMeterValueNotification(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            OcppTrace.Msg("OCPP", $"MeterValues {stationId}: {payload}");

            // Parse raw JTokens — avoids enum deserialization issues with strings like
            // "Energy.Active.Import.Register" that don't map to C# enum names.
            var meterValues = payload["meterValue"] as JArray;
            if (meterValues != null)
            {
                var lastEntry = meterValues.LastOrDefault();
                var sampledValues = lastEntry?["sampledValue"] as JArray;

                if (sampledValues != null)
                {
                    decimal? energyWh = null;

                    foreach (JToken sv in sampledValues)
                    {
                        // Default measurand per OCPP 1.6 spec is Energy.Active.Import.Register
                        var measurand = sv["measurand"]?.Value<string>() ?? "Energy.Active.Import.Register";
                        if (!measurand.StartsWith("Energy")) continue;

                        var valueStr = sv["value"]?.Value<string>() ?? "";
                        var unit     = sv["unit"]?.Value<string>()  ?? "Wh";

                        if (decimal.TryParse(valueStr,
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var val))
                        {
                            // Normalise to Wh
                            energyWh = unit.Equals("kWh", StringComparison.OrdinalIgnoreCase)
                                ? val * 1000m
                                : val;
                            break;
                        }
                    }

                    if (energyWh.HasValue)
                    {
                        var connector = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == stationId);
                        if (connector != null)
                        {
                            // Calculate delivery power from consecutive readings
                            var now = DateTime.UtcNow;
                            if (connector.MeterValue.HasValue && connector.LastMeterValueAt.HasValue)
                            {
                                var deltaWh    = (double)(energyWh.Value - connector.MeterValue.Value);
                                var deltaHours = (now - connector.LastMeterValueAt.Value).TotalHours;
                                if (deltaHours > 0 && deltaWh >= 0)
                                    connector.CurrentPowerKw = Math.Round(deltaWh / 1000.0 / deltaHours, 2);
                            }

                            connector.MeterValue       = energyWh.Value;
                            connector.LastMeterValueAt = now;
                            connector.LastUpdate       = now;
                            await db.SaveChangesAsync();
                            OcppTrace.Dbg("OCPP", $"Meter stored: {energyWh.Value} Wh, power={connector.CurrentPowerKw} kW for {stationId}");

                        // Publish meter event — EVChargingApi pushes live data to the app
                        // and checks the wallet balance (event-driven, no more 30-second polling).
                        _ = RabbitMqPublisher.PublishMeterUpdatedAsync(
                            stationId,
                            energyWh.Value,
                            ChargingStationConnections.GetMeterStart(stationId),
                            connector.CurrentPowerKw);
                        }
                    }
                }
            }

            await SendCallResult(socket, messageId, new JObject());
        }

        public static async Task<JObject> SendStartCharging(WebSocket socket)
        {
            var payload = new JObject
            {
                ["connectorId"] = 1,
                ["idTag"] = "TEST123"
            };

            // This now WAITS for CALLRESULT
            var response = await SendCallAndWaitAsync(
                socket,
                "RemoteStartTransaction",
                payload,
                TimeSpan.FromSeconds(30));

            return response;
        }

        public static async Task<JObject> SendStopCharging(WebSocket socket, string stationId, int? callerTransactionId, ChargingDBContext db)
        {
            // Priority: 1) caller (from EVChargingApi session table)
            //           2) in-memory (survives within same process lifetime)
            //           3) OCPP DB Connector.ActiveTransactionId (survives restarts)
            int? transactionId = callerTransactionId
                ?? ChargingStationConnections.GetTransaction(stationId);
            if (transactionId == null)
            {
                var connector = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == stationId);
                transactionId = connector?.ActiveTransactionId;
            }

            OcppTrace.Msg("OCPP", $"[→ OUT] {stationId}: sending RemoteStopTransaction transactionId={transactionId} (source: {(callerTransactionId.HasValue ? "session" : transactionId.HasValue ? "fallback" : "none")})");

            var payload = new JObject
            {
                ["transactionId"] = transactionId ?? 0
            };

            var response = await SendCallAndWaitAsync(
                socket,
                "RemoteStopTransaction",
                payload,
                TimeSpan.FromSeconds(30));

            return response;
        }

        static async Task SendCallResult(WebSocket socket, string messageId, JObject payload)
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
        static async Task SendCallError(WebSocket socket, string messageId, string errorCode, string errorDescription)
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
        public static async Task<JObject> SendCallAndWaitAsync(WebSocket socket, string action, JObject payload, TimeSpan timeout)
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
        static async Task SendAsync(WebSocket socket, JArray message)
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
    }
}
