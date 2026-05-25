using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Sets a network connection profile on the station.</summary>
    public class SetNetworkProfileRequest
    {
        [JsonProperty("configurationSlot")]
        public int ConfigurationSlot { get; set; }

        [JsonProperty("connectionData")]
        public NetworkConnectionProfileType ConnectionData { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SetNetworkProfileResponse
    {
        [JsonProperty("status")]
        public SetNetworkProfileStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
