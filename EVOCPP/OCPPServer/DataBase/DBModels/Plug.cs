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

        public bool IsOnline { get; set; } = false;

        /// <summary>
        /// Get or set the timestamp of the last update received from the Connector.
        /// </summary>
        public DateTime? LastStatusUpdate { get; set; }

        /// <summary>
        /// UTC timestamp of the most recent Heartbeat received from this charger.
        /// Used by the watchdog to detect chargers that have gone silent.
        /// Null until the first Heartbeat is processed.
        /// </summary>
        public DateTime? LastHeartbeatAt { get; set; }

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

        /// <summary>
        /// Meter reading (Wh) at the start of the current or most-recent session.
        /// Written by HandleStartTransaction / HandleTxStarted so the value survives
        /// an OCPP server restart.  Cleared on StopTransaction / HandleTxEnded.
        /// On restart the MeterValues handler reads this column back into
        /// ConnectorState.MeterStartWh so PublishMeterUpdated carries a valid baseline.
        /// </summary>
        public decimal? MeterStartWh { get; set; }
    }
}
