using OCPPServer.DataBase.DBModels;
using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;

namespace OCPPServer.ChargingStationInterface
{
    public class ChargingSessionInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the session.
        /// </summary>
        [Key]
        public Guid SessionId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the tag.
        /// </summary>
        public string TagId { get; set; }

        /// <summary>
        /// Gets or sets the start time for the operation or event in Coordinated Universal Time (UTC).
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Gets or sets the end time for the operation or event, if specified.
        /// </summary>
        public DateTime? EndTime { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Gets or sets the total amount of energy consumed.
        /// </summary>
        public decimal EnergyConsumed { get; set; }

        /// <summary>
        /// Gets or sets the total cost for the current transaction.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Gets or sets the current battery charge level as a percentage.
        /// </summary>
        public int CurrentBatteryPercentage { get; set; }

        /// <summary>
        /// Gets or sets the current amount of power being delivered.
        /// </summary>
        public decimal CurrentPowerDelivery { get; set; }

        /// <summary>
        /// Gets or sets the prepaid amount associated with the transaction.
        /// </summary>
        public decimal PrepaidAmount { get; set; } = 50m;

        // ---- Relationships ----

        // many Sessions -> 1 User
        public int UserId { get; set; }
        public virtual User User { get; set; }

        // OPTIONAL but very useful:
        // many Sessions -> 1 Connector
        public int? ConnectorId { get; set; }
        public virtual Connector Connector { get; set; }
    }
}