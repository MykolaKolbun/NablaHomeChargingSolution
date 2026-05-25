using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Sends the current running cost of a transaction to the station for display.</summary>
    public class CostUpdatedRequest
    {
        [JsonProperty("totalCost")]
        public decimal TotalCost { get; set; }

        [JsonProperty("transactionId")]
        public string TransactionId { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class CostUpdatedResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
