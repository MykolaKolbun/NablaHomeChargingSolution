using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Notifies the CSMS about a battery swap event (in/out).</summary>
    public class BatterySwapRequest
    {
        [JsonProperty("eventType")]
        public BatterySwapEventEnumType EventType { get; set; }

        [JsonProperty("idToken")]
        public IdTokenType IdToken { get; set; } = null!;

        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("batteryData")]
        public List<BatteryDataType> BatteryData { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class BatterySwapResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
