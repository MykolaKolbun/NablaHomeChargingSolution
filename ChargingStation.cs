using OCPP_RD.ChargingStationInterface;
using System.Collections.Generic;

namespace OCPP_RD
{
    public class ChargingStation
    {
        /// <summary>
        /// DESCRIPTION: Unique identifier for the charging station
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// DESCRIPTION: Name of the charging station
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// DESCRIPTION: Number of connectors available at the charging station
        /// </summary>
        public int NOfConnectors { get; set; }

        /// <summary>
        /// DESCRIPTION: Location details of the charging station
        /// </summary>
        public Location Location { get; set; }
        /// <summary>
        /// DESCRIPTION: List of connectors associated with the charging station
        /// </summary>
        public List<IConnector> Connectors { get; set; }

        /// <summary>
        /// DESCRIPTION: Current meter value of the charging station
        /// </summary>
        public decimal MeterValue { get; set; }
    }
}
