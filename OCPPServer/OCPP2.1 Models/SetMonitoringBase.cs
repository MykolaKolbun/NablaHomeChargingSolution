using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Sets the active monitoring base (e.g. FactoryDefault, HardWiredOnly).</summary>
    public class SetMonitoringBaseRequest
    {
        [JsonProperty("monitoringBase")]
        public MonitoringBaseEnumType MonitoringBase { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SetMonitoringBaseResponse
    {
        [JsonProperty("status")]
        public GenericDeviceModelStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
