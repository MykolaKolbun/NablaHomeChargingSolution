using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests whether a transaction has pending TransactionEvent messages in the queue.</summary>
    public class GetTransactionStatusRequest
    {
        [JsonProperty("transactionId")]
        public string? TransactionId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetTransactionStatusResponse
    {
        [JsonProperty("messagesInQueue")]
        public bool MessagesInQueue { get; set; }

        [JsonProperty("ongoingIndicator")]
        public bool? OngoingIndicator { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
