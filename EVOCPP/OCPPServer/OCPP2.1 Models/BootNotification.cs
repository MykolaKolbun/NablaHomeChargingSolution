using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Sent on startup; registers the charging station with the CSMS.</summary>
    public class BootNotificationRequest
    {
        [JsonProperty("chargingStation")]
        public ChargingStationType ChargingStation { get; set; } = null!;

        [JsonProperty("reason")]
        public BootReasonEnumType Reason { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class BootNotificationResponse
    {
        [JsonProperty("currentTime")]
        public DateTime CurrentTime { get; set; }

        [JsonProperty("interval")]
        public int Interval { get; set; }

        [JsonProperty("status")]
        public RegistrationStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
