using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests a report of active variable monitors. Station replies with NotifyMonitoringReport.</summary>
    public class GetMonitoringReportRequest
    {
        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("monitoringCriteria")]
        public List<MonitoringCriterionEnumType>? MonitoringCriteria { get; set; }

        [JsonProperty("componentVariable")]
        public List<ComponentVariableType>? ComponentVariable { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetMonitoringReportResponse
    {
        [JsonProperty("status")]
        public GenericDeviceModelStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
