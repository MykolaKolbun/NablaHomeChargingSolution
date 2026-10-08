using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Installs a root certificate on the station.</summary>
    public class InstallCertificateRequest
    {
        [JsonProperty("certificateType")]
        public InstallCertificateUseEnumType CertificateType { get; set; }

        [JsonProperty("certificate")]
        public string Certificate { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class InstallCertificateResponse
    {
        [JsonProperty("status")]
        public InstallCertificateStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
