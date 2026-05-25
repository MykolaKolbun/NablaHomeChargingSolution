using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Reports that a DER control alarm condition has started or ended (new in OCPP 2.1).</summary>
    public class NotifyDERAlarmRequest
    {
        [JsonProperty("controlType")]
        public DERControlEnumType ControlType { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("alarmEnded")]
        public bool? AlarmEnded { get; set; }

        [JsonProperty("gridEventFault")]
        public GridEventFaultEnumType? GridEventFault { get; set; }

        [JsonProperty("extraInfo")]
        public string? ExtraInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class NotifyDERAlarmResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
