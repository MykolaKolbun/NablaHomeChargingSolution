using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS ↔ CSMS. Vendor-specific data exchange.</summary>
    public class DataTransferRequest
    {
        [JsonProperty("vendorId")]
        public string VendorId { get; set; } = null!;

        [JsonProperty("messageId")]
        public string? MessageId { get; set; }

        [JsonProperty("data")]
        public object? Data { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class DataTransferResponse
    {
        [JsonProperty("status")]
        public DataTransferStatusEnumType Status { get; set; }

        [JsonProperty("data")]
        public object? Data { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
