using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests the station to upload a diagnostics or security log.</summary>
    public class GetLogRequest
    {
        [JsonProperty("logType")]
        public LogEnumType LogType { get; set; }

        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("log")]
        public LogParametersType Log { get; set; } = null!;

        [JsonProperty("retries")]
        public int? Retries { get; set; }

        [JsonProperty("retryInterval")]
        public int? RetryInterval { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetLogResponse
    {
        [JsonProperty("status")]
        public LogStatusEnumType Status { get; set; }

        [JsonProperty("filename")]
        public string? Filename { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
