using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests a firmware update. Station reports progress via FirmwareStatusNotification.</summary>
    public class UpdateFirmwareRequest
    {
        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("firmware")]
        public FirmwareType Firmware { get; set; } = null!;

        [JsonProperty("retries")]
        public int? Retries { get; set; }

        [JsonProperty("retryInterval")]
        public int? RetryInterval { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class UpdateFirmwareResponse
    {
        [JsonProperty("status")]
        public UpdateFirmwareStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
