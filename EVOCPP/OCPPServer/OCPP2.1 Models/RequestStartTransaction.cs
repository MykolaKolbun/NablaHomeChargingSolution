using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests the station to start a transaction (replaces RemoteStartTransaction from OCPP 1.6).</summary>
    public class RequestStartTransactionRequest
    {
        [JsonProperty("remoteStartId")]
        public int RemoteStartId { get; set; }

        [JsonProperty("idToken")]
        public IdTokenType IdToken { get; set; } = null!;

        /// <summary>Target EVSE. Omit to let the station choose.</summary>
        [JsonProperty("evseId")]
        public int? EvseId { get; set; }

        [JsonProperty("groupIdToken")]
        public IdTokenType? GroupIdToken { get; set; }

        [JsonProperty("chargingProfile")]
        public ChargingProfileType? ChargingProfile { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class RequestStartTransactionResponse
    {
        /// <summary>Accepted = station will try to start. Wait for TransactionEvent(Started) to confirm.</summary>
        [JsonProperty("status")]
        public RequestStartStopStatusEnumType Status { get; set; }

        [JsonProperty("transactionId")]
        public string? TransactionId { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
