using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class HeartbeatResponse
    {
        [JsonProperty("currentTime")]
        public DateTime CurrentTime { get; set; }
    }
}
