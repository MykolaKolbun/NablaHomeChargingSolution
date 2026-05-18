using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCPP_RD.OCPP1._6_Models;
using OCPPServer.ChargingStationInterface;
using OCPPServer.Data;
using OCPPServer.DataBase.DBModels;
using System.Collections.Concurrent;
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
            var message = JArray.Parse(json);

            int messageType = message[0].Value<int>();
            string messageId = message[1].Value<string>();

            // HANDLE RESPONSE FIRST
            if (messageType == OcppMessageType.CALLRESULT ||
                messageType == OcppMessageType.CALLERROR)
            {
                if (_pendingRequests.TryRemove(messageId, out var tcs))
                {
                    var payload = messageType == OcppMessageType.CALLRESULT
                        ? (JObject)message[2]
                        : new JObject
                        {
                            ["errorCode"] = message[2],
                            ["errorDescription"] = message[3]
                        };

                    tcs.TrySetResult(payload);
                }
                return;
            }

            // Existing CALL handling (unchanged)
            if (messageType != OcppMessageType.CALL)
                return;

            string action = message[2].Value<string>();
            JObject payloadCall = (JObject)message[3];
            Console.WriteLine($"Received CALL: {action} from {stationId}");
            Console.WriteLine($"Payload: {payloadCall}");
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
                    await HandleStartTransaction(socket, messageId, payloadCall, stationId);
                    break;

                case "StopTransaction":
                    await HandleStopTransaction(socket, messageId, stationId);
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

        static async Task HandleStartTransaction(WebSocket socket, string messageId, JObject payload, string stationId)
        {
            var transactionId = ChargingStationConnections.AssignTransaction(stationId);
            Console.WriteLine($"StartTransaction from {stationId}, assigned transactionId={transactionId}");

            var response = new JObject
            {
                ["transactionId"] = transactionId,
                ["idTagInfo"] = new JObject { ["status"] = "Accepted" }
            };
            await SendCallResult(socket, messageId, response);
        }

        static async Task HandleStopTransaction(WebSocket socket, string messageId, string stationId)
        {
            Console.WriteLine($"StopTransaction from {stationId}");
            ChargingStationConnections.ClearTransaction(stationId);
            await SendCallResult(socket, messageId, new JObject
            {
                ["idTagInfo"] = new JObject { ["status"] = "Accepted" }
            });
        }

        static async Task HandleMeterValueNotification(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db)
        {
            var req = payload.ToObject<MeterValuesRequest>();
            Console.WriteLine($"MeterValue notification");
            Console.WriteLine($"MeterValue: {req}");
            var existingConnector = await db.Connectors
                .FirstOrDefaultAsync(c => c.OcppId == stationId);

            //if (existingConnector == null)
            //{
            //    return;
            //}
            //else
            //{
            //    // Update existing
            //    existingConnector.Vendor = req.VendorId;
            //    existingConnector.Status = req.Status;
            //    existingConnector.LastUpdate = DateTime.UtcNow;
            //}

            //await db.SaveChangesAsync();

            var responsePayload = new JObject
            {
            };

            await SendCallResult(socket, messageId, responsePayload);
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
            var json = message.ToString(Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(json);

            await socket.SendAsync(
                bytes,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }
    }
}
