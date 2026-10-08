using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Creates or replaces variable monitors on the station.</summary>
    public class SetVariableMonitoringRequest
    {
        [JsonProperty("setMonitoringData")]
        public List<SetMonitoringDataType> SetMonitoringData { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SetVariableMonitoringResponse
    {
        [JsonProperty("setMonitoringResult")]
        public List<SetMonitoringResultType> SetMonitoringResult { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
