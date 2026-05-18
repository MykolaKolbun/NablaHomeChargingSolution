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
        /// Get or set the current meter value of the Connector.
        /// </summary>
        public decimal? MeterValue { get; set; }

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

        // ---- Relationships ----

        // many Connectors -> 1 ChargingStation
        /// <summary>
        /// Get or set the foreign key referencing the associated ChargingStation.
        /// </summary>
        public string ChargerSN { get; set; }
        /// <summary>
        /// Get or set the associated ChargingStation entity.
        /// </summary>
        public virtual ChargingStation ChargingStation { get; set; }
    }
}
