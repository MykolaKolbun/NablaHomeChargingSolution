using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Updates a dynamic charging profile with new limits/setpoints without a full replacement (new in OCPP 2.1).</summary>
    public class UpdateDynamicScheduleRequest
    {
        [JsonProperty("chargingProfileId")]
        public int ChargingProfileId { get; set; }

        [JsonProperty("scheduleUpdate")]
        public ChargingScheduleUpdateType ScheduleUpdate { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class UpdateDynamicScheduleResponse
    {
        [JsonProperty("status")]
        public ChargingProfileStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
