using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>
    /// CS → CSMS. The single transaction lifecycle message in OCPP 2.x.
    /// Replaces StartTransaction, StopTransaction, and transaction-related MeterValues/StatusNotification from OCPP 1.6.
    /// eventType=Started  → transaction beginning.
    /// eventType=Updated  → mid-transaction meter/state change.
    /// eventType=Ended    → transaction complete.
    /// </summary>
    public class TransactionEventRequest
    {
        [JsonProperty("eventType")]
        public TransactionEventEnumType EventType { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("triggerReason")]
        public TriggerReasonEnumType TriggerReason { get; set; }

        [JsonProperty("seqNo")]
        public int SeqNo { get; set; }

        [JsonProperty("transactionInfo")]
        public TransactionType TransactionInfo { get; set; } = null!;

        [JsonProperty("offline")]
        public bool Offline { get; set; } = false;

        [JsonProperty("evse")]
        public EVSEType? Evse { get; set; }

        [JsonProperty("idToken")]
        public IdTokenType? IdToken { get; set; }

        [JsonProperty("meterValue")]
        public List<MeterValueType>? MeterValue { get; set; }

        [JsonProperty("numberOfPhasesUsed")]
        public int? NumberOfPhasesUsed { get; set; }

        [JsonProperty("cableMaxCurrent")]
        public int? CableMaxCurrent { get; set; }

        [JsonProperty("reservationId")]
        public int? ReservationId { get; set; }

        [JsonProperty("preconditioningStatus")]
        public PreconditioningStatusEnumType? PreconditioningStatus { get; set; }

        [JsonProperty("evseSleep")]
        public bool? EvseSleep { get; set; }

        [JsonProperty("costDetails")]
        public CostDetailsType? CostDetails { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class TransactionEventResponse
    {
        [JsonProperty("totalCost")]
        public decimal? TotalCost { get; set; }

        [JsonProperty("chargingPriority")]
        public int? ChargingPriority { get; set; }

        [JsonProperty("idTokenInfo")]
        public IdTokenInfoType? IdTokenInfo { get; set; }

        [JsonProperty("transactionLimit")]
        public TransactionLimitType? TransactionLimit { get; set; }

        [JsonProperty("updatedPersonalMessage")]
        public MessageContentType? UpdatedPersonalMessage { get; set; }

        [JsonProperty("updatedPersonalMessageExtra")]
        public List<MessageContentType>? UpdatedPersonalMessageExtra { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
