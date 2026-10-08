using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Closes a periodic event stream by ID (new in OCPP 2.1).</summary>
    public class ClosePeriodicEventStreamRequest
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ClosePeriodicEventStreamResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
