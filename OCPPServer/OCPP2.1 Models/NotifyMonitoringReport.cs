using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Returns active variable monitors in response to GetMonitoringReport. Paginated via tbc/seqNo.</summary>
    public class NotifyMonitoringReportRequest
    {
        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("seqNo")]
        public int SeqNo { get; set; }

        [JsonProperty("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [JsonProperty("monitor")]
        public List<MonitoringDataType>? Monitor { get; set; }

        [JsonProperty("tbc")]
        public bool Tbc { get; set; } = false;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class NotifyMonitoringReportResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
