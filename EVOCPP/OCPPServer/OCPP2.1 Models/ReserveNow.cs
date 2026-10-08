using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Reserves a connector for a specific IdToken.</summary>
    public class ReserveNowRequest
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("expiryDateTime")]
        public DateTime ExpiryDateTime { get; set; }

        [JsonProperty("idToken")]
        public IdTokenType IdToken { get; set; } = null!;

        [JsonProperty("evseId")]
        public int? EvseId { get; set; }

        [JsonProperty("connectorType")]
        public string? ConnectorType { get; set; }

        [JsonProperty("groupIdToken")]
        public IdTokenType? GroupIdToken { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ReserveNowResponse
    {
        [JsonProperty("status")]
        public ReserveNowStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
