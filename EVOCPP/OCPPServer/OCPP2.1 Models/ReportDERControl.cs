using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Returns DER control settings in response to GetDERControl. Paginated via tbc (new in OCPP 2.1).</summary>
    public class ReportDERControlRequest
    {
        [JsonProperty("requestId")]
        public int RequestId { get; set; }

        [JsonProperty("tbc")]
        public bool? Tbc { get; set; }

        [JsonProperty("curve")]
        public List<DERCurveGetType>? Curve { get; set; }

        [JsonProperty("enterService")]
        public List<EnterServiceGetType>? EnterService { get; set; }

        [JsonProperty("fixedPFAbsorb")]
        public List<FixedPFGetType>? FixedPFAbsorb { get; set; }

        [JsonProperty("fixedPFInject")]
        public List<FixedPFGetType>? FixedPFInject { get; set; }

        [JsonProperty("fixedVar")]
        public List<FixedVarGetType>? FixedVar { get; set; }

        [JsonProperty("freqDroop")]
        public List<FreqDroopGetType>? FreqDroop { get; set; }

        [JsonProperty("gradient")]
        public List<GradientGetType>? Gradient { get; set; }

        [JsonProperty("limitMaxDischarge")]
        public List<LimitMaxDischargeGetType>? LimitMaxDischarge { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ReportDERControlResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
