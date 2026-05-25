using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Requests validation of a VAT number entered by the driver (new in OCPP 2.1).</summary>
    public class VatNumberValidationRequest
    {
        [JsonProperty("vatNumber")]
        public string VatNumber { get; set; } = null!;

        [JsonProperty("evseId")]
        public int? EvseId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class VatNumberValidationResponse
    {
        [JsonProperty("status")]
        public GenericStatusEnumType Status { get; set; }

        [JsonProperty("vatNumber")]
        public string VatNumber { get; set; } = null!;

        [JsonProperty("company")]
        public AddressType? Company { get; set; }

        [JsonProperty("evseId")]
        public int? EvseId { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
