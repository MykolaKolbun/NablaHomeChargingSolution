using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Sends a full or differential update of the local authorization list.</summary>
    public class SendLocalListRequest
    {
        [JsonProperty("versionNumber")]
        public int VersionNumber { get; set; }

        [JsonProperty("updateType")]
        public UpdateEnumType UpdateType { get; set; }

        [JsonProperty("localAuthorizationList")]
        public List<AuthorizationData>? LocalAuthorizationList { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SendLocalListResponse
    {
        [JsonProperty("status")]
        public SendLocalListStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
