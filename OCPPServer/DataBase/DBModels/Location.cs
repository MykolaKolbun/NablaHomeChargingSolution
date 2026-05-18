using System.ComponentModel.DataAnnotations;

namespace OCPPServer.DataBase.DBModels
{
    public class Location
    {
        /// <summary>
        /// Gets or sets the unique identifier for the location.
        /// </summary>
        [Key]
        public int LocationId { get; set; }

        /// <summary>
        /// Gets or sets the country associated with the entity.
        /// </summary>
        public string Country { get; set; } = "Unknown";

        /// <summary>
        /// Gets or sets the name of the city.
        /// </summary>
        public string City { get; set; } = "Unknown";

        /// <summary>
        /// Gets or sets the postal code associated with the address.
        /// </summary>
        public string PostalCode { get; set; } = "Unknown";

        /// <summary>
        /// Gets or sets the address associated with the entity.
        /// </summary>
        public string Address { get; set; } = "Unknown";

        /// <summary>
        /// Gets or sets the GPS location coordinates.
        /// </summary>
        public string GPSLocation { get; set; } = "Unknown";

        // ---- Relationships ----

        // 1 Location -> many ChargingStations
        public virtual ICollection<ChargingStation> ChargingStations { get; set; } = new List<ChargingStation>();
    }
}