using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Sends a signed certificate in response to a SignCertificate request.</summary>
    public class CertificateSignedRequest
    {
        [JsonProperty("certificateChain")]
        public string CertificateChain { get; set; } = null!;

        [JsonProperty("certificateType")]
        public CertificateSigningUseEnumType? CertificateType { get; set; }

        [JsonProperty("requestId")]
        public int? RequestId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class CertificateSignedResponse
    {
        [JsonProperty("status")]
        public CertificateSignedStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
