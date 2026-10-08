using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Returns charging profiles in response to GetChargingProfiles. Paginated via tbc.</summary>
    public class ReportChargingProfilesRequest
    {
        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("chargingLimitSource")]
        public ChargingLimitSourceEnumType ChargingLimitSource { get; set; }

        [JsonProperty("evseId")]
        public int EvseId { get; set; }

        [JsonProperty("chargingProfile")]
        public List<ChargingProfileType> ChargingProfile { get; set; } = [];

        [JsonProperty("tbc")]
        public bool Tbc { get; set; } = false;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ReportChargingProfilesResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
