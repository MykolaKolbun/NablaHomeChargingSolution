using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Periodic keep-alive; response carries the current CSMS time.</summary>
    public class HeartbeatRequest
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class HeartbeatResponse
    {
        [JsonProperty("currentTime")]
        public DateTime CurrentTime { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
