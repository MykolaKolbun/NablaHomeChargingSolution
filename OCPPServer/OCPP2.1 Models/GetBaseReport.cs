using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests a full or summary device model report from the station.</summary>
    public class GetBaseReportRequest
    {
        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("reportBase")]
        public ReportBaseEnumType ReportBase { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetBaseReportResponse
    {
        [JsonProperty("status")]
        public GenericDeviceModelStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
