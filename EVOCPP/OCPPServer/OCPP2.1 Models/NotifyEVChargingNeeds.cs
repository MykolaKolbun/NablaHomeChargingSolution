using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Reports ISO 15118 charging needs from the EV (AC/DC parameters, departure time).</summary>
    public class NotifyEVChargingNeedsRequest
    {
        [JsonProperty("evseId")]
        public int EvseId { get; set; }

        [JsonProperty("chargingNeeds")]
        public ChargingNeedsType ChargingNeeds { get; set; } = null!;

        [JsonProperty("maxScheduleTuples")]
        public int? MaxScheduleTuples { get; set; }

        [JsonProperty("timestamp")]
        public DateTime? Timestamp { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class NotifyEVChargingNeedsResponse
    {
        [JsonProperty("status")]
        public NotifyEVChargingNeedsStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
