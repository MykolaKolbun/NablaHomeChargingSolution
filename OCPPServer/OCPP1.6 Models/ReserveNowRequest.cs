using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class ReserveNowRequest
    {
        [JsonProperty("connectorId")]
        public int ConnectorId { get; set; }

        [JsonProperty("expiryDate")]
        public DateTime ExpiryDate { get; set; }

        [JsonProperty("idTag")]
        public string IdTag { get; set; }

        [JsonProperty("parentIdTag")]
        public string ParentIdTag { get; set; }

        [JsonProperty("reservationId")]
        public int ReservationId { get; set; }
    }
}
