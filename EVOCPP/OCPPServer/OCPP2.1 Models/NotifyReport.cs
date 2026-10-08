using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Returns device model data in response to GetBaseReport or GetReport. Paginated via tbc/seqNo.</summary>
    public class NotifyReportRequest
    {
        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("seqNo")]
        public int SeqNo { get; set; }

        [JsonProperty("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [JsonProperty("reportData")]
        public List<ReportDataType>? ReportData { get; set; }

        [JsonProperty("tbc")]
        public bool Tbc { get; set; } = false;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class NotifyReportResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
