using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests the combined charging schedule for an EVSE.</summary>
    public class GetCompositeScheduleRequest
    {
        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("evseId")]
        public int EvseId { get; set; }

        [JsonProperty("chargingRateUnit")]
        public ChargingRateUnitEnumType? ChargingRateUnit { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetCompositeScheduleResponse
    {
        [JsonProperty("status")]
        public GenericStatusEnumType Status { get; set; }

        [JsonProperty("schedule")]
        public CompositeScheduleType? Schedule { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
