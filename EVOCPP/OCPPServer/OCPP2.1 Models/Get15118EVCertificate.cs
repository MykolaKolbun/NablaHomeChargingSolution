using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Requests a contract certificate (ISO 15118) for the EV.</summary>
    public class Get15118EVCertificateRequest
    {
        [JsonProperty("iso15118SchemaVersion")]
        public string Iso15118SchemaVersion { get; set; } = null!;

        [JsonProperty("action")]
        public CertificateActionEnumType Action { get; set; }

        [JsonProperty("exiRequest")]
        public string ExiRequest { get; set; } = null!;

        [JsonProperty("maximumContractCertificateChains")]
        public int? MaximumContractCertificateChains { get; set; }

        [JsonProperty("prioritizedEmaids")]
        public List<string>? PrioritizedEmaids { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class Get15118EVCertificateResponse
    {
        [JsonProperty("status")]
        public Iso15118EVCertificateStatusEnumType Status { get; set; }

        [JsonProperty("exiResponse")]
        public string ExiResponse { get; set; } = null!;

        [JsonProperty("remainingContracts")]
        public int? RemainingContracts { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
