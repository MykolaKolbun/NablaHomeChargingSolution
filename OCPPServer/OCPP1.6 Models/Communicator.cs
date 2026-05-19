using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCPP_RD.OCPP1._6_Models;
using OCPPServer.ChargingStationInterface;
using OCPPServer.Data;
using OCPPServer.DataBase.DBModels;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.WebSockets;
using System.Text;

namespace OCPPServer.OCPP1._6_Models
{
    public class Communicator
    {
        /// <summary>
        /// contains pending requests waiting for a response from the charging station
        /// </summary>
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>>_pendingRequests = new();

        public static async Task RouteOcppMessage(WebSocket socket, string stationId, string json, ChargingDBContext db)
        {
            JArray message;
            try { message = JArray.Parse(json); }
            catch (Exception ex)
            {
                Console.WriteLine($"[OCPP ERR  ] {stationId}: failed to parse JSON — {ex.Message}");
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
                    Console.WriteLine($"[OCPP ← {typeLabel}] {stationId} msgId={messageId} matched pending request");
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
                    Console.WriteLine($"[OCPP ← {typeLabel}] {stationId} msgId={messageId} — NO matching pending request (already timed out?)");
                }
                return;
            }

            if (messageType != OcppMessageType.CALL)
            {
                Console.WriteLine($"[OCPP WARN ] {stationId}: unknown messageType={messageType}, ignoring");
                return;
            }

            string action      = message[2].Value<string>();
            JObject payloadCall = (JObject)message[3];
            Console.WriteLine($"[OCPP ← CALL] {stationId}: action={action} payload={payloadCall}");
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

                case "MeterValue":
                    await HandleMeterValueNotification(socket, messageId, payloadCall, stationId, db);
                    break;

                default:
                    await SendCallError(socket, messageId, "NotSupported",
                        $"Action {action} not supported");
                    break;
            }
        }

        static async Task HandleBootNotification(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            var req = payload.ToObject<BootNotificationRequest>();

            Console.WriteLine($"BootNotification from {stationId}");

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
            Console.WriteLine($"Heartbeat ");
            var payload = new JObject
            {
                ["currentTime"] = DateTime.UtcNow.ToString("o")
            };

            await SendCallResult(socket, messageId, payload);
        }

        static async Task HandleStatusNotification(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            
            var req = payload.ToObject<StatusNotificationRequest>();
            Console.WriteLine($"Status notification");

            var existingConnector = await db.Connectors
                .FirstOrDefaultAsync(c => c.OcppId == stationId);

            if (existingConnector == null)
            {
                return;
            }
            else
            {
                // Update existing
                existingConnector.Vendor = req.VendorId;
                existingConnector.Status = req.Status;
                existingConnector.LastUpdate = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            var responsePayload = new JObject
            {
            };

            await SendCallResult(socket, messageId, responsePayload);
        }

        static async Task HandleStartTransaction(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            var transactionId = ChargingStationConnections.AssignTransaction(stationId);
            Console.WriteLine($"StartTransaction from {stationId}, assigned transactionId={transactionId}");

            // Save meter reading at transaction start
            var meterStartRaw = payload["meterStart"]?.Value<decimal?>();
            if (meterStartRaw.HasValue)
            {
                var connector = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == stationId);
                if (connector != null)
                {
                    connector.MeterStart = meterStartRaw.Value;
                    connector.MeterStop  = null; // clear previous session's stop value
                    connector.LastUpdate = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    Console.WriteLine($"MeterStart={meterStartRaw.Value} Wh saved for {stationId}");
                }
            }

            var response = new JObject
            {
                ["transactionId"] = transactionId,
                ["idTagInfo"] = new JObject { ["status"] = "Accepted" }
            };
            await SendCallResult(socket, messageId, response);
        }

        static async Task HandleStopTransaction(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            Console.WriteLine($"[OCPP STOP ] {stationId}: StopTransaction payload keys=[{string.Join(", ", payload.Properties().Select(p => p.Name))}]");
            Console.WriteLine($"[OCPP STOP ] meterStop raw value = {payload["meterStop"]}");
            ChargingStationConnections.ClearTransaction(stationId);

            // Save meter reading at transaction end
            var meterStopRaw = payload["meterStop"]?.Value<decimal?>();
            if (meterStopRaw.HasValue)
            {
                var connector = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == stationId);
                if (connector != null)
                {
                    connector.MeterStop  = meterStopRaw.Value;
                    connector.MeterValue = meterStopRaw.Value; // sync live reading to final value
                    connector.LastUpdate = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    var consumed = connector.MeterStart.HasValue
                        ? $"{(meterStopRaw.Value - connector.MeterStart.Value) / 1000m:F3} kWh consumed"
                        : "no MeterStart on record";
                    Console.WriteLine($"MeterStop={meterStopRaw.Value} Wh for {stationId} — {consumed}");
                }
            }

            await SendCallResult(socket, messageId, new JObject
            {
                ["idTagInfo"] = new JObject { ["status"] = "Accepted" }
            });
        }

        static async Task HandleMeterValueNotification(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            Console.WriteLine($"MeterValues from {stationId}: {payload}");

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
                            connector.MeterValue = energyWh.Value;
                            connector.LastUpdate  = DateTime.UtcNow;
                            await db.SaveChangesAsync();
                            Console.WriteLine($"Stored {energyWh.Value} Wh for {stationId}");
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

        public static async Task<JObject> SendStopCharging(WebSocket socket, string stationId)
        {
            var transactionId = ChargingStationConnections.GetTransaction(stationId);

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

            Console.WriteLine($"[OCPP → OUT] {json}");

            await socket.SendAsync(
                bytes,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }
    }
}
