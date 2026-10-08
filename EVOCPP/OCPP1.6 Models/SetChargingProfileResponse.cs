using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class SetChargingProfileResponse
    {
        [JsonProperty("status")]
        public ChargingProfileStatus Status { get; set; }

        public enum ChargingProfileStatus
        {
            Accepted,
            Rejected,
            NotSupported
        }
    }
}
