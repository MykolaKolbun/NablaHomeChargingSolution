using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Reports that priority charging has been activated or deactivated (new in OCPP 2.1).</summary>
    public class NotifyPriorityChargingRequest
    {
        [JsonProperty("transactionId")]
        public string TransactionId { get; set; } = null!;

        [JsonProperty("activated")]
        public bool Activated { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class NotifyPriorityChargingResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
