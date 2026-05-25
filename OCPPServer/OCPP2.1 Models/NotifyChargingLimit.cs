using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Reports that an external source has set a charging limit on the station.</summary>
    public class NotifyChargingLimitRequest
    {
        [JsonProperty("chargingLimit")]
        public ChargingLimitType ChargingLimit { get; set; } = null!;

        [JsonProperty("chargingSchedule")]
        public List<ChargingScheduleType>? ChargingSchedule { get; set; }

        [JsonProperty("evseId")]
        public int? EvseId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class NotifyChargingLimitResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
