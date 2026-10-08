using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Removes charging profiles matching the given criteria.</summary>
    public class ClearChargingProfileRequest
    {
        [JsonProperty("chargingProfileId")]
        public int? ChargingProfileId { get; set; }

        [JsonProperty("chargingProfileCriteria")]
        public ClearChargingProfileType? ChargingProfileCriteria { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ClearChargingProfileResponse
    {
        [JsonProperty("status")]
        public ClearChargingProfileStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
