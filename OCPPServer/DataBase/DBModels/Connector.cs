using OCPPServer.ChargingStationInterface;
using System.ComponentModel.DataAnnotations;
namespace OCPPServer.DataBase.DBModels
{
    public class Connector
    {
        /// <summary>
        /// Get or set the unique identifier for the Connector.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Get or set the vendor of the Charge Point.
        /// </summary>
        public string? Vendor { get; set; } = string.Empty;

        /// <summary>
        /// Get or set the model of the Charge Point.
        /// </summary>
        public string? ChargePointModel { get; set; }

        /// <summary>
        /// Get or set the serial number of the Charge Point.
        /// </summary>
        [Required]
        public string ChargePointSN { get; set; }

        /// <summary>
        /// Get or set the firmware version of the Charge Point.
        /// </summary>
        public string? FirmwareVersion { get; set; }

        /// <summary>
        /// Get or set the SIM number of the Charge Point.
        /// </summary>
        public string? SIMNr { get; set; }

        /// <summary>
        /// Get or set the OCPP identifier of the Connector.
        /// </summary>
        [Required]
        public string OcppId { get; set; }

        /// <summary>
        /// Get or set the current status of the Connector.
        /// </summary>
        [Required]
        public Enumerators.ChargePointStatus Status { get; set; }
            = Enumerators.ChargePointStatus.Available;

        /// <summary>
        /// UTC timestamp when the current/last transaction started (from StartTransaction).
        /// Null when no session is active. Used to calculate average charging power.
        /// </summary>
        public DateTime? SessionStartedAt { get; set; }

        /// <summary>
        /// OCPP transactionId assigned to the current active session.
        /// Persisted in DB so it survives server restarts — needed for RemoteStopTransaction.
        /// Null when no session is active.
        /// </summary>
        public int? ActiveTransactionId { get; set; }

        /// <summary>
        /// Live meter reading (Wh) updated by MeterValues messages during charging.
        /// </summary>
        public decimal? MeterValue { get; set; }

        /// <summary>
        /// UTC timestamp of the last MeterValues message — used to calculate delivery power.
        /// </summary>
        public DateTime? LastMeterValueAt { get; set; }

        /// <summary>
        /// Instantaneous delivery power (kW) computed from the two most recent MeterValues readings.
        /// Null when no session is active or only one reading received so far.
        /// </summary>
        public double? CurrentPowerKw { get; set; }

        /// <summary>
        /// Get or set the timestamp of the last update received from the Connector.
        /// </summary>
        public DateTime? LastUpdate { get; set; }

        /// <summary>
        /// Get or set the timestamp when the Connector record was created.
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; }

        // ---- Admin-editable info ----

        public string? Name { get; set; }
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool IsFastCharger { get; set; } = false;
        public bool ShowOnMap { get; set; } = true;
        public double? MaxPowerKw { get; set; }
        public int NumberOfConnectors { get; set; } = 1;

        // ---- Informational fields from BootNotification ----

        /// <summary>
        /// ChargeBox serial number reported by the charger in BootNotification.
        /// Stored for reference only — not a FK.
        /// </summary>
        public string ChargerSN { get; set; } = string.Empty;
    }
}
