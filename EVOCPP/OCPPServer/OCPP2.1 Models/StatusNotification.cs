using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Reports the connector status for a specific EVSE/connector.</summary>
    public class StatusNotificationRequest
    {
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("connectorStatus")]
        public ConnectorStatusEnumType ConnectorStatus { get; set; }

        [JsonProperty("evseId")]
        public int EvseId { get; set; }

        [JsonProperty("connectorId")]
        public int ConnectorId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class StatusNotificationResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
