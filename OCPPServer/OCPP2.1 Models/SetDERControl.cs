using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Sets a DER control curve or setting on the station (new in OCPP 2.1).</summary>
    public class SetDERControlRequest
    {
        [JsonProperty("controlId")]
        public string ControlId { get; set; } = null!;

        [JsonProperty("controlType")]
        public DERControlEnumType ControlType { get; set; }

        [JsonProperty("isDefault")]
        public bool IsDefault { get; set; }

        [JsonProperty("curve")]
        public DERCurveType? Curve { get; set; }

        [JsonProperty("enterService")]
        public EnterServiceType? EnterService { get; set; }

        [JsonProperty("fixedPFAbsorb")]
        public FixedPFType? FixedPFAbsorb { get; set; }

        [JsonProperty("fixedPFInject")]
        public FixedPFType? FixedPFInject { get; set; }

        [JsonProperty("fixedVar")]
        public FixedVarType? FixedVar { get; set; }

        [JsonProperty("freqDroop")]
        public FreqDroopType? FreqDroop { get; set; }

        [JsonProperty("gradient")]
        public GradientType? Gradient { get; set; }

        [JsonProperty("limitMaxDischarge")]
        public LimitMaxDischargeType? LimitMaxDischarge { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SetDERControlResponse
    {
        [JsonProperty("status")]
        public DERControlStatusEnumType Status { get; set; }

        [JsonProperty("supersededIds")]
        public List<string>? SupersededIds { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
