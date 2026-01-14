using System;

namespace OCPPServer.ChargingStationInterface
{
    public class ChargingSessionInfo
    {
        /// <summary>
        /// Defines the unique identifier of the charging session.
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>
        /// Defines the start time of the charging session.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Defines the end time of the charging session.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Defines the total energy consumed during the charging session in kWh.
        /// </summary>
        public decimal EnergyConsumed { get; set; }

        /// <summary>
        /// Defines the total cost of the charging session.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Defines the current battery percentage of the connected vehicle.
        /// </summary>
        public int CurrentBatteryPercentage { get; set; }

        /// <summary>
        /// Defines the current power delivery in kW.
        /// </summary>
        public decimal CurrentPowerDelivery { get; set; }
    }
}