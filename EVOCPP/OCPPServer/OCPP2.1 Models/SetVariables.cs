using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Writes one or more configuration variables to the device model.</summary>
    public class SetVariablesRequest
    {
        [JsonProperty("setVariableData")]
        public List<SetVariableDataType> SetVariableData { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SetVariablesResponse
    {
        [JsonProperty("setVariableResult")]
        public List<SetVariableResultType> SetVariableResult { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
