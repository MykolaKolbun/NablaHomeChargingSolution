using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Notifies that a web-based payment flow has been initiated on the station (new in OCPP 2.1).</summary>
    public class NotifyWebPaymentStartedRequest
    {
        [JsonProperty("evseId")]
        public int EvseId { get; set; }

        [JsonProperty("timeout")]
        public int Timeout { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class NotifyWebPaymentStartedResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
