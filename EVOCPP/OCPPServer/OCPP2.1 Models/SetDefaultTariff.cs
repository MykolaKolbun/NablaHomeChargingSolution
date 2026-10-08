using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Sets the default tariff for an EVSE (new in OCPP 2.1).</summary>
    public class SetDefaultTariffRequest
    {
        [JsonProperty("evseId")]
        public int EvseId { get; set; }

        [JsonProperty("tariff")]
        public TariffType Tariff { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SetDefaultTariffResponse
    {
        [JsonProperty("status")]
        public TariffSetStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
