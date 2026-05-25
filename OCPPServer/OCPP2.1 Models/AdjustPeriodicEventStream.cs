using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Adjusts parameters of an open periodic event stream.</summary>
    public class AdjustPeriodicEventStreamRequest
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("params")]
        public PeriodicEventStreamParamsType Params { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class AdjustPeriodicEventStreamResponse
    {
        [JsonProperty("status")]
        public GenericStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
