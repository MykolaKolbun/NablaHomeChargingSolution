using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Notifies that a previously reported charging limit has been cleared.</summary>
    public class ClearedChargingLimitRequest
    {
        [JsonProperty("chargingLimitSource")]
        public ChargingLimitSourceEnumType ChargingLimitSource { get; set; }

        [JsonProperty("evseId")]
        public int? EvseId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ClearedChargingLimitResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
