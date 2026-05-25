using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Sends a message to be displayed on the station's screen.</summary>
    public class SetDisplayMessageRequest
    {
        [JsonProperty("message")]
        public MessageInfoType Message { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SetDisplayMessageResponse
    {
        [JsonProperty("status")]
        public DisplayMessageStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
