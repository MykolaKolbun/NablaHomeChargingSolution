using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Requests OCSP revocation status for a single certificate.</summary>
    public class GetCertificateStatusRequest
    {
        [JsonProperty("ocspRequestData")]
        public OCSPRequestDataType OcspRequestData { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetCertificateStatusResponse
    {
        [JsonProperty("status")]
        public GetCertificateStatusEnumType Status { get; set; }

        [JsonProperty("ocspResult")]
        public string? OcspResult { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
