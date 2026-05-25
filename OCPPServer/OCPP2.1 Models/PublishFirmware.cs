using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests a local controller to publish a firmware file for other stations to download.</summary>
    public class PublishFirmwareRequest
    {
        [JsonProperty("location")]
        public string Location { get; set; } = null!;

        [JsonProperty("checksum")]
        public string Checksum { get; set; } = null!;

        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("retries")]
        public int? Retries { get; set; }

        [JsonProperty("retryInterval")]
        public int? RetryInterval { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class PublishFirmwareResponse
    {
        [JsonProperty("status")]
        public GenericStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
