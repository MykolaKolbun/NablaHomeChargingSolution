using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Requests the station to send a specific message type immediately.</summary>
    public class TriggerMessageRequest
    {
        [JsonProperty("requestedMessage")]
        public MessageTriggerEnumType RequestedMessage { get; set; }

        /// <summary>Target a specific EVSE/connector. Omit for station-wide trigger.</summary>
        [JsonProperty("evse")]
        public EVSEType? Evse { get; set; }

        /// <summary>Used with CustomTrigger to identify a vendor-specific message.</summary>
        [JsonProperty("customTrigger")]
        public string? CustomTrigger { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class TriggerMessageResponse
    {
        [JsonProperty("status")]
        public TriggerMessageStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
