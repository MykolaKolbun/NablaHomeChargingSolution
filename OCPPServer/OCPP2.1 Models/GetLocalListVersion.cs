using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests the version number of the local authorization list.</summary>
    public class GetLocalListVersionRequest
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetLocalListVersionResponse
    {
        [JsonProperty("versionNumber")]
        public int VersionNumber { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
