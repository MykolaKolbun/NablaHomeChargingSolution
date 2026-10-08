using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class GetCompositeScheduleRequest
    {
        [JsonProperty("connectorId")]
        public int ConnectorId { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("chargingRateUnit")]
        public string ChargingRateUnit { get; set; }

        // Enum for the allowed values of ChargingRateUnit
        public enum ChargingRateUnitEnum
        {
            A, // Amperes
            W  // Watts
        }

        // Constructor to ensure valid charging rate unit
        public GetCompositeScheduleRequest(int connectorId, int duration, string chargingRateUnit)
        {
            if (!Enum.IsDefined(typeof(ChargingRateUnitEnum), chargingRateUnit))
            {
                throw new ArgumentException("Invalid charging rate unit.");
            }

            ConnectorId = connectorId;
            Duration = duration;
            ChargingRateUnit = chargingRateUnit;
        }
    }
}
