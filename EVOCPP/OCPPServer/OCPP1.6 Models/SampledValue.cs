using Newtonsoft.Json;

namespace OCPP_RD.OCPP1._6_Models
{
    public partial class MeterValuesRequest
    {
        public class SampledValue
        {
            [JsonProperty("value")]
            public string Value { get; set; }

            [JsonProperty("context")]
            public Context Context { get; set; }

            [JsonProperty("format")]
            public Format Format { get; set; }

            [JsonProperty("measurand")]
            public Measurand Measurand { get; set; }

            [JsonProperty("phase")]
            public Phase Phase { get; set; }

            [JsonProperty("location")]
            public Location Location { get; set; }

            [JsonProperty("unit")]
            public Unit Unit { get; set; }
        }
    }
}
