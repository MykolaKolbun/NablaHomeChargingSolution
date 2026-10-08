using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Changes the operational status of the station or a specific EVSE.</summary>
    public class ChangeAvailabilityRequest
    {
        [JsonProperty("operationalStatus")]
        public OperationalStatusEnumType OperationalStatus { get; set; }

        /// <summary>Omit to target the entire station.</summary>
        [JsonProperty("evse")]
        public EVSEType? Evse { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ChangeAvailabilityResponse
    {
        [JsonProperty("status")]
        public ChangeAvailabilityStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
