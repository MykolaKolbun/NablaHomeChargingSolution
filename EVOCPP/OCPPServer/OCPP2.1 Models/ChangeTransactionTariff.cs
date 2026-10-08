using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Updates the tariff for an ongoing transaction (new in OCPP 2.1).</summary>
    public class ChangeTransactionTariffRequest
    {
        [JsonProperty("transactionId")]
        public string TransactionId { get; set; } = null!;

        [JsonProperty("tariff")]
        public TariffType Tariff { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ChangeTransactionTariffResponse
    {
        [JsonProperty("status")]
        public TariffChangeStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
