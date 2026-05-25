using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Sets the minimum severity level for reported monitoring events.</summary>
    public class SetMonitoringLevelRequest
    {
        /// <summary>0 (Danger) … 9 (Debug). Only events with severity ≤ this value are reported.</summary>
        [JsonProperty("severity")]
        public int Severity { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SetMonitoringLevelResponse
    {
        [JsonProperty("status")]
        public GenericStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
