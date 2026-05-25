using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Reports a security event (e.g. firmware tamper, invalid certificate).</summary>
    public class SecurityEventNotificationRequest
    {
        [JsonProperty("type")]
        public string Type { get; set; } = null!;

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("techInfo")]
        public string? TechInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SecurityEventNotificationResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
