using OCPPServer.ChargingStationInterface;
using System.ComponentModel.DataAnnotations;

namespace OCPPServer.DataBase.DBModels
{
    public class Plug
    {
        /// <summary>
        /// Get or set the unique identifier for the Connector.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Get or set the current status of the Connector.
        /// </summary>
        [Required]
        public Enumerators.ChargePointStatus Status { get; set; }
            = Enumerators.ChargePointStatus.Available;

        /// <summary>
        /// OCPP transactionId assigned to the current active session.
        /// Persisted in DB so it survives server restarts — needed for RemoteStopTransaction.
        /// Null when no session is active.
        /// </summary>
        public bool IsOnline { get; set; } = false;

        /// <summary>
        /// Live meter reading (Wh) updated by MeterValues messages during charging.
        /// </summary>
        public decimal? MeterValue { get; set; }

        /// <summary>
        /// UTC timestamp of the last MeterValues message — used to calculate delivery power.
        /// </summary>
        public DateTime? LastMeterValueAt { get; set; }

        /// <summary>
        /// State of Charge (%) reported by the vehicle via the charger.
        /// Only populated by DC fast chargers that communicate with the vehicle BMS.
        /// Null for AC chargers or when the charger does not report SoC.
        /// </summary>
        public decimal? StateOfCharge { get; set; }


        /// <summary>
        /// Get or set the timestamp of the last update received from the Connector.
        /// </summary>
        public DateTime? LastStatusUpdate { get; set; }

        /// <summary>
        /// Get or set the timestamp when the Connector record was created.
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; }

        // ---- Admin-editable info ----

        /// <summary>
        /// Gets or sets a value indicating whether the charger supports fast charging.
        /// </summary>
        /// <remarks>True when the charger can deliver higher-power fast charging (for example, DC fast
        /// charging). Use for filtering, display, and capability checks.</remarks>
        public bool IsFastCharger { get; set; } = false;

        /// <summary>
        /// Max power (kW).
        /// Should be set by the admin to reflect the real max power of the charger, so EVChargingApi could use in Application.
        /// </summary>
        public int MaxPower { get; set; } = 21;


        // ---- Informational fields from BootNotification ----

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
        public string ChargePointSN { get; set; } = string.Empty;

        /// <summary>
        /// Get or set the firmware version of the Charge Point.
        /// </summary>
        public string? FirmwareVersion { get; set; }

        /// <summary>
        /// Get or set the SIM number of the Charge Point.
        /// </summary>
        public string? SIMNr { get; set; }

        /// <summary>
        /// Get or set the OCPP identifier of the Connector during BootNotification. This is the unique identifier that the charger uses to identify itself in OCPP messages.
        /// </summary>
        [Required]
        public string OcppId { get; set; } = string.Empty;

        /// <summary>
        /// Negotiated OCPP sub-protocol, e.g. "ocpp1.6", "ocpp2.1".
        /// Set at WebSocket handshake and persisted on first BootNotification.
        /// </summary>
        public string OcppVersion { get; set; } = "ocpp1.6";
    }
}
