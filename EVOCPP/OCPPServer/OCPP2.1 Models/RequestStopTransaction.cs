using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests the station to stop an ongoing transaction (replaces RemoteStopTransaction from OCPP 1.6).</summary>
    public class RequestStopTransactionRequest
    {
        [JsonProperty("transactionId")]
        public string TransactionId { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class RequestStopTransactionResponse
    {
        /// <summary>Accepted = station will try to stop. Wait for TransactionEvent(Ended) to confirm.</summary>
        [JsonProperty("status")]
        public RequestStartStopStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
