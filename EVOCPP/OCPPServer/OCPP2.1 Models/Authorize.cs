using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Requests authorization for an IdToken before or during a transaction.</summary>
    public class AuthorizeRequest
    {
        [JsonProperty("idToken")]
        public IdTokenType IdToken { get; set; } = null!;

        [JsonProperty("certificate")]
        public string? Certificate { get; set; }

        [JsonProperty("iso15118CertificateHashData")]
        public List<OCSPRequestDataType>? Iso15118CertificateHashData { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class AuthorizeResponse
    {
        [JsonProperty("idTokenInfo")]
        public IdTokenInfoType IdTokenInfo { get; set; } = null!;

        [JsonProperty("allowedEnergyTransfer")]
        public List<EnergyTransferModeEnumType>? AllowedEnergyTransfer { get; set; }

        [JsonProperty("certificateStatus")]
        public AuthorizeCertificateStatusEnumType? CertificateStatus { get; set; }

        [JsonProperty("tariff")]
        public TariffType? Tariff { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
