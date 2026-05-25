using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests display messages stored on the station. Station replies with NotifyDisplayMessages.</summary>
    public class GetDisplayMessagesRequest
    {
        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("id")]
        public List<int>? Id { get; set; }

        [JsonProperty("priority")]
        public MessagePriorityEnumType? Priority { get; set; }

        [JsonProperty("state")]
        public MessageStateEnumType? State { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetDisplayMessagesResponse
    {
        [JsonProperty("status")]
        public GetDisplayMessagesStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
