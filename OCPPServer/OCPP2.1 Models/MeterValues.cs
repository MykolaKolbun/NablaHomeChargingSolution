using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Sends sampled meter readings for an EVSE outside of a TransactionEvent.</summary>
    public class MeterValuesRequest
    {
        [JsonProperty("evseId")]
        public int EvseId { get; set; }

        [JsonProperty("meterValue")]
        public List<MeterValueType> MeterValue { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class MeterValuesResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
