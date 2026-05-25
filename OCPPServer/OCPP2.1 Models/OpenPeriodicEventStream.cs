using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Opens a new periodic event stream for a variable monitor (new in OCPP 2.1).</summary>
    public class OpenPeriodicEventStreamRequest
    {
        [JsonProperty("constantStreamData")]
        public ConstantStreamDataType ConstantStreamData { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class OpenPeriodicEventStreamResponse
    {
        [JsonProperty("status")]
        public GenericStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
