using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Sends the EV-side charging schedule (ISO 15118-20) to the CSMS.</summary>
    public class NotifyEVChargingScheduleRequest
    {
        [JsonProperty("timeBase")]
        public DateTime TimeBase { get; set; }

        [JsonProperty("evseId")]
        public int EvseId { get; set; }

        [JsonProperty("chargingSchedule")]
        public ChargingScheduleType ChargingSchedule { get; set; } = null!;

        [JsonProperty("selectedChargingScheduleId")]
        public int? SelectedChargingScheduleId { get; set; }

        [JsonProperty("powerToleranceAcceptance")]
        public bool? PowerToleranceAcceptance { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class NotifyEVChargingScheduleResponse
    {
        [JsonProperty("status")]
        public GenericStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
