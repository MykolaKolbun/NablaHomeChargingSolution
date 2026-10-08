using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests deletion of an installed certificate.</summary>
    public class DeleteCertificateRequest
    {
        [JsonProperty("certificateHashData")]
        public CertificateHashDataType CertificateHashData { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class DeleteCertificateResponse
    {
        [JsonProperty("status")]
        public DeleteCertificateStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
