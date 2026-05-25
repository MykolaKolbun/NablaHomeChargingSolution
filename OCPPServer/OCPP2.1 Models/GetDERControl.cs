using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests DER control settings stored on the station (new in OCPP 2.1). Station replies with ReportDERControl.</summary>
    public class GetDERControlRequest
    {
        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("isDefault")]
        public bool? IsDefault { get; set; }

        [JsonProperty("controlId")]
        public string? ControlId { get; set; }

        [JsonProperty("controlType")]
        public DERControlEnumType? ControlType { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetDERControlResponse
    {
        [JsonProperty("status")]
        public DERControlStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
