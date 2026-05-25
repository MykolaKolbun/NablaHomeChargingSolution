using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests the list of open periodic event streams (new in OCPP 2.1).</summary>
    public class GetPeriodicEventStreamRequest
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetPeriodicEventStreamResponse
    {
        [JsonProperty("constantStreamData")]
        public List<ConstantStreamDataType>? ConstantStreamData { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
