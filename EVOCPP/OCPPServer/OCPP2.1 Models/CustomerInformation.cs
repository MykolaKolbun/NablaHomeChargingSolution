using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests or clears customer information stored locally on the station.</summary>
    public class CustomerInformationRequest
    {
        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("report")]
        public bool Report { get; set; }

        [JsonProperty("clear")]
        public bool Clear { get; set; }

        [JsonProperty("customerIdentifier")]
        public string? CustomerIdentifier { get; set; }

        [JsonProperty("idToken")]
        public IdTokenType? IdToken { get; set; }

        [JsonProperty("customerCertificate")]
        public CertificateHashDataType? CustomerCertificate { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class CustomerInformationResponse
    {
        [JsonProperty("status")]
        public CustomerInformationStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
