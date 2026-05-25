using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Reports that a DER control has started or stopped executing (new in OCPP 2.1).</summary>
    public class NotifyDERStartStopRequest
    {
        [JsonProperty("controlId")]
        public string ControlId { get; set; } = null!;

        [JsonProperty("started")]
        public bool Started { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("supersededIds")]
        public List<string>? SupersededIds { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class NotifyDERStartStopResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
