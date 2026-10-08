using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests the list of charging profiles matching criteria. Station replies with ReportChargingProfiles.</summary>
    public class GetChargingProfilesRequest
    {
        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("chargingProfile")]
        public ChargingProfileCriterionType ChargingProfile { get; set; } = null!;

        [JsonProperty("evseId")]
        public int? EvseId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetChargingProfilesResponse
    {
        [JsonProperty("status")]
        public GetChargingProfileStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
