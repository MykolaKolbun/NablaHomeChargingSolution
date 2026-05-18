using OCPPServer.ChargingStationInterface;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;
//using static OCPP_RD.OCPP1._6_Models.MeterValuesRequest;

namespace OCPPServer.DataBase.DBModels
{
    public class ChargingStation
    {
        /// <summary>
        /// Gets or sets the unique serial number of the charger.
        /// </summary>
        [Key]
        public string ChargerSN { get; set; }

        /// <summary>
        /// Gets or sets the name associated with the object.
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the number of connectors to use.
        /// </summary>
        public int NOfConnectors { get; set; } = 1;

        /// <summary>
        /// Gets or sets the current reading of the meter.
        /// </summary>
        public decimal MeterValue { get; set; }

        // ---- Relationships ----

        // 1 ChargingStation -> many Connectors
        /// <summary>
        /// Gets or sets the collection of connectors associated with the charging station.
        /// </summary>
        /// <remarks>Each connector represents an individual charging interface available at the station.
        /// Modifying this collection updates the set of connectors linked to the charging station.</remarks>
        public virtual ICollection<Connector> Connectors { get; set; } = new List<Connector>();

        // 1 ChargingStation -> 1 Location
        /// <summary>
        /// Gets or sets the unique identifier of the location associated with the charging station.
        /// </summary>
        public int LocationId { get; set; }
        /// <summary>
        /// Gets or sets the geographical location associated with this entity.
        /// </summary>
        public virtual Location Location { get; set; }
    }
}
