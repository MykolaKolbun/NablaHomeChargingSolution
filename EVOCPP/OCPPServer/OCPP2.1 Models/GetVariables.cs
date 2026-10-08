using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Reads one or more configuration variables from the device model.</summary>
    public class GetVariablesRequest
    {
        [JsonProperty("getVariableData")]
        public List<GetVariableDataType> GetVariableData { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetVariablesResponse
    {
        [JsonProperty("getVariableResult")]
        public List<GetVariableResultType> GetVariableResult { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
