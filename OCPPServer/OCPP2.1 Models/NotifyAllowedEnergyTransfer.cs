using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Tells the station which energy transfer modes are allowed for a transaction (new in OCPP 2.1).</summary>
    public class NotifyAllowedEnergyTransferRequest
    {
        [JsonProperty("transactionId")]
        public string TransactionId { get; set; } = null!;

        [JsonProperty("allowedEnergyTransfer")]
        public List<EnergyTransferModeEnumType> AllowedEnergyTransfer { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class NotifyAllowedEnergyTransferResponse
    {
        [JsonProperty("status")]
        public NotifyAllowedEnergyTransferStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
