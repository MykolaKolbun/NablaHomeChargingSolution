using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests a filtered device model report. Station replies with NotifyReport.</summary>
    public class GetReportRequest
    {
        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("componentCriteria")]
        public List<ComponentCriterionEnumType>? ComponentCriteria { get; set; }

        [JsonProperty("componentVariable")]
        public List<ComponentVariableType>? ComponentVariable { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetReportResponse
    {
        [JsonProperty("status")]
        public GenericDeviceModelStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
