using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Activates or deactivates priority charging for a transaction (new in OCPP 2.1).</summary>
    public class UsePriorityChargingRequest
    {
        [JsonProperty("transactionId")]
        public string TransactionId { get; set; } = null!;

        [JsonProperty("activate")]
        public bool Activate { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class UsePriorityChargingResponse
    {
        [JsonProperty("status")]
        public PriorityChargingStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
