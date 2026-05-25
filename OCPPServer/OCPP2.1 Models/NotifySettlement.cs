using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Reports the outcome of a payment settlement (new in OCPP 2.1).</summary>
    public class NotifySettlementRequest
    {
        [JsonProperty("pspRef")]
        public string PspRef { get; set; } = null!;

        [JsonProperty("status")]
        public PaymentStatusEnumType Status { get; set; }

        [JsonProperty("settlementAmount")]
        public decimal SettlementAmount { get; set; }

        [JsonProperty("settlementTime")]
        public DateTime SettlementTime { get; set; }

        [JsonProperty("transactionId")]
        public string? TransactionId { get; set; }

        [JsonProperty("receiptId")]
        public string? ReceiptId { get; set; }

        [JsonProperty("receiptUrl")]
        public string? ReceiptUrl { get; set; }

        [JsonProperty("vatNumber")]
        public string? VatNumber { get; set; }

        [JsonProperty("vatCompany")]
        public AddressType? VatCompany { get; set; }

        [JsonProperty("statusInfo")]
        public string? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class NotifySettlementResponse
    {
        [JsonProperty("receiptId")]
        public string? ReceiptId { get; set; }

        [JsonProperty("receiptUrl")]
        public string? ReceiptUrl { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
