using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests a reset of the whole station or a specific EVSE.</summary>
    public class ResetRequest
    {
        [JsonProperty("type")]
        public ResetEnumType Type { get; set; }

        /// <summary>Omit to reset the whole station.</summary>
        [JsonProperty("evseId")]
        public int? EvseId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ResetResponse
    {
        [JsonProperty("status")]
        public ResetStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
