using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Reports monitoring events (threshold breaches, delta changes, periodic readings).</summary>
    public class NotifyEventRequest
    {
        [JsonProperty("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [JsonProperty("seqNo")]
        public int SeqNo { get; set; }

        [JsonProperty("eventData")]
        public List<EventDataType> EventData { get; set; } = [];

        [JsonProperty("tbc")]
        public bool Tbc { get; set; } = false;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class NotifyEventResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
