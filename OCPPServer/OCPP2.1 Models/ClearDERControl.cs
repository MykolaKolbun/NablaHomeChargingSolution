using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Clears a DER control setting (new in OCPP 2.1).</summary>
    public class ClearDERControlRequest
    {
        [JsonProperty("isDefault")]
        public bool IsDefault { get; set; }

        [JsonProperty("controlId")]
        public string? ControlId { get; set; }

        [JsonProperty("controlType")]
        public DERControlEnumType? ControlType { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ClearDERControlResponse
    {
        [JsonProperty("status")]
        public DERControlStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
