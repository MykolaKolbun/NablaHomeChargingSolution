using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Removes variable monitoring entries by their IDs.</summary>
    public class ClearVariableMonitoringRequest
    {
        [JsonProperty("id")]
        public List<int> Id { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ClearVariableMonitoringResponse
    {
        [JsonProperty("clearMonitoringResult")]
        public List<ClearMonitoringResultType> ClearMonitoringResult { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
