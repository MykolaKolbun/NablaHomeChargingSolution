using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class StartTransactionRequest
    {
        [JsonProperty("connectorId")]
        public int ConnectorId { get; set; }

        [JsonProperty("idTag")]
        public string IdTag { get; set; }

        [JsonProperty("meterStart")]
        public int MeterStart { get; set; }

        [JsonProperty("reservationId")]
        public int? ReservationId { get; set; } // Optional field

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
