using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Removes one or more tariffs from the station (new in OCPP 2.1).</summary>
    public class ClearTariffsRequest
    {
        [JsonProperty("tariffIds")]
        public List<string>? TariffIds { get; set; }

        [JsonProperty("evseId")]
        public int? EvseId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ClearTariffsResponse
    {
        [JsonProperty("clearTariffsResult")]
        public List<ClearTariffsResultType> ClearTariffsResult { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
