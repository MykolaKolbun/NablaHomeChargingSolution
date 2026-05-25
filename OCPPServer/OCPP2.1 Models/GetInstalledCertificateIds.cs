using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests the list of installed certificate identifiers.</summary>
    public class GetInstalledCertificateIdsRequest
    {
        [JsonProperty("certificateType")]
        public List<GetCertificateIdUseEnumType>? CertificateType { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetInstalledCertificateIdsResponse
    {
        [JsonProperty("status")]
        public GetInstalledCertificateStatusEnumType Status { get; set; }

        [JsonProperty("certificateHashDataChain")]
        public List<CertificateHashDataChainType>? CertificateHashDataChain { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
