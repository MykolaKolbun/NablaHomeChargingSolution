using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class TriggerMessageResponse
    {
        [JsonProperty("status")]
        public Status Status { get; set; }
    }

    public enum Status
    {
        Accepted,
        Rejected,
        NotImplemented
    }
}
