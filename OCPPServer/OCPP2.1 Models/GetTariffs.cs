using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests the tariffs currently assigned to an EVSE (new in OCPP 2.1).</summary>
    public class GetTariffsRequest
    {
        [JsonProperty("evseId")]
        public int EvseId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetTariffsResponse
    {
        [JsonProperty("status")]
        public TariffGetStatusEnumType Status { get; set; }

        [JsonProperty("tariffAssignments")]
        public List<TariffAssignmentType>? TariffAssignments { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
