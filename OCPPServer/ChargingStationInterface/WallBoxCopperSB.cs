using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPPServer.ChargingStationInterface
{
    internal class WallBoxCopperSB : IConnector
    {
        // Properties required by IConnector
        public int Id { get; set; }
        public string OcppId { get; set; }
        public string Status { get; set; }
        public string StatusDescription { get; set; }
        public decimal MeterValue { get; set; }
        public decimal Price { get; set; }
        public Enumerators.Type Type { get; set; }

        /// <summary>
        /// Initializes a new instance of the Connector class with all required fields.
        /// </summary>
        public WallBoxCopperSB(
            int id,
            string ocppId,
            string status,
            string statusDescription,
            decimal meterValue,
            decimal price,
            Enumerators.Type type)
        {
            Id = id;
            OcppId = ocppId;
            Status = status;
            StatusDescription = statusDescription;
            MeterValue = meterValue;
            Price = price;
            Type = type;
        }

        // --- Method Implementations ---

        public void UpdateStatus(string status, string statusDescription)
        {
            Status = status;
            StatusDescription = statusDescription;
        }

        public bool StartChargingSession(Guid sessionId)
        {
            // Example logic: Only start if available
            if (Status == "Available")
            {
                UpdateStatus("Charging", $"Session {sessionId} started.");
                return true;
            }
            return false;
        }

        public bool StopChargingSession(Guid sessionId)
        {
            UpdateStatus("Available", "Session finished.");
            return true;
        }

        public string GetConnectedVehicleID()
        {
            // This would typically interface with hardware/OCPP tags
            return "EV-12345-SAMPLE";
        }

        public void UpdateFirmware(string firmwareBin)
        {
            // Logic to push binary to the charging pole
            Console.WriteLine($"Deploying firmware: {firmwareBin}");
        }

        public ChargingSessionInfo GetChargingSessionInfo(Guid sesionId)
        {
            throw new NotImplementedException();
        }
    }
}
