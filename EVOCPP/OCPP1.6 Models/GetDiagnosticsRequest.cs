using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class GetDiagnosticsRequest
    {
        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("retries")]
        public int? Retries { get; set; }

        [JsonProperty("retryInterval")]
        public int? RetryInterval { get; set; }

        [JsonProperty("startTime")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("stopTime")]
        public DateTime? StopTime { get; set; }
    }
}
