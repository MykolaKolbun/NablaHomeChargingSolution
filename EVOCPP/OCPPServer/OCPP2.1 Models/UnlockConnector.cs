using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests the station to unlock a specific connector.</summary>
    public class UnlockConnectorRequest
    {
        [JsonProperty("evseId")]
        public int EvseId { get; set; }

        [JsonProperty("connectorId")]
        public int ConnectorId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class UnlockConnectorResponse
    {
        [JsonProperty("status")]
        public UnlockStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
