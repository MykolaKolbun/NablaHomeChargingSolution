using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Requests OCSP revocation status for a chain of certificates (new in OCPP 2.1).</summary>
    public class GetCertificateChainStatusRequest
    {
        [JsonProperty("certificateStatusRequests")]
        public List<CertificateStatusRequestInfoType> CertificateStatusRequests { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetCertificateChainStatusResponse
    {
        [JsonProperty("certificateStatus")]
        public List<CertificateStatusType> CertificateStatus { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
