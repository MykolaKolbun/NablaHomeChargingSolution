using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace OCPP_RD.OCPP1._6_Models
{
    public partial class MeterValuesRequest
    {
        public class MeterValue
        {
            [JsonProperty("timestamp")]
            public DateTime Timestamp { get; set; }

            [JsonProperty("sampledValue")]
            public List<SampledValue> SampledValue { get; set; }
        }
    }
}
