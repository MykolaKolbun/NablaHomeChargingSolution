using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Reports progress of a firmware publish operation.</summary>
    public class PublishFirmwareStatusNotificationRequest
    {
        [JsonProperty("status")]
        public PublishFirmwareStatusEnumType Status { get; set; }

        [JsonProperty("location")]
        public List<string>? Location { get; set; }

        [JsonProperty("requestId")]
        public int? RequestId { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class PublishFirmwareStatusNotificationResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
