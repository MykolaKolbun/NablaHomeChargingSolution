using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Removes a specific display message from the station.</summary>
    public class ClearDisplayMessageRequest
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ClearDisplayMessageResponse
    {
        [JsonProperty("status")]
        public ClearMessageStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
