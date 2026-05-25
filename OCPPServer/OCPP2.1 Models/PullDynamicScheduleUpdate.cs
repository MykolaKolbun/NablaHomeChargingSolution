using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Requests the latest update to a dynamic charging profile (new in OCPP 2.1).</summary>
    public class PullDynamicScheduleUpdateRequest
    {
        [JsonProperty("chargingProfileId")]
        public int ChargingProfileId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class PullDynamicScheduleUpdateResponse
    {
        [JsonProperty("status")]
        public ChargingProfileStatusEnumType Status { get; set; }

        [JsonProperty("scheduleUpdate")]
        public ChargingScheduleUpdateType? ScheduleUpdate { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
