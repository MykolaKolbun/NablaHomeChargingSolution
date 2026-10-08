using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests the local controller to stop publishing a previously published firmware.</summary>
    public class UnpublishFirmwareRequest
    {
        [JsonProperty("checksum")]
        public string Checksum { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class UnpublishFirmwareResponse
    {
        [JsonProperty("status")]
        public UnpublishFirmwareStatusEnumType Status { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
