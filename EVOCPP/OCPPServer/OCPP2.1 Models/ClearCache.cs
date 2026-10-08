using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests the station to clear its local authorization cache.</summary>
    public class ClearCacheRequest
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ClearCacheResponse
    {
        [JsonProperty("status")]
        public ClearCacheStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
