using System;

namespace OCPPServer.ChargingStationInterface
{
    public interface IConnector
    {
        /// <summary>
        /// Defines the unique identifier of the connector.
        /// </summary> 
        int Id { get; set; }

        /// <summary>
        /// Defines the OCPP identifier of the connector.
        /// </summary>
        string OcppId { get; set; }
         
        /// <summary>
        /// Defines the status of the connector.
        /// </summary>
        string Status { get; set; }

        /// <summary>
        /// Defines the description of the connector status.
        /// </summary>
        string StatusDescription { get; set; }

        /// <summary>
        /// Defines the meter value of the connector.
        /// </summary>
        decimal MeterValue { get; set; }

        /// <summary>
        /// Gets or sets the price of the item.
        /// </summary>
        decimal Price { get; set; }

        /// <summary>
        /// Defines the type of the connector.
        /// </summary>
        Enumerators.Type Type { get; set; }

        void UpdateStatus(string status, string statusDescription);
        bool StartChargingSession(Guid sessionId);
        bool StopChargingSession(Guid sessionId);
        string GetConnectedVehicleID();
        ChargingSessionInfo GetChargingSessionInfo(Guid sesionId);
        void UpdateFirmware(string firmwareBin);
    }
}