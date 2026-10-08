using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace OCPP_RD.OCPP2._1_Models
{
    // ── Core ──────────────────────────────────────────────────────────────────────

    public class CustomDataType
    {
        [JsonProperty("vendorId"), Required, MaxLength(255)]
        public string VendorId { get; set; } = null!;

        [JsonExtensionData]
        public Dictionary<string, object>? ExtensionData { get; set; }
    }

    public class StatusInfoType
    {
        [JsonProperty("reasonCode"), Required, MaxLength(20)]
        public string ReasonCode { get; set; } = null!;

        [JsonProperty("additionalInfo"), MaxLength(512)]
        public string? AdditionalInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ModemType
    {
        [JsonProperty("iccid"), MaxLength(20)]
        public string? Iccid { get; set; }

        [JsonProperty("imsi"), MaxLength(20)]
        public string? Imsi { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ChargingStationType
    {
        [JsonProperty("model"), Required, MaxLength(20)]
        public string Model { get; set; } = null!;

        [JsonProperty("vendorName"), Required, MaxLength(50)]
        public string VendorName { get; set; } = null!;

        [JsonProperty("serialNumber"), MaxLength(25)]
        public string? SerialNumber { get; set; }

        [JsonProperty("modem")]
        public ModemType? Modem { get; set; }

        [JsonProperty("firmwareVersion"), MaxLength(50)]
        public string? FirmwareVersion { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class AdditionalInfoType
    {
        [JsonProperty("additionalIdToken"), Required, MaxLength(36)]
        public string AdditionalIdToken { get; set; } = null!;

        [JsonProperty("type"), Required, MaxLength(50)]
        public string Type { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class IdTokenType
    {
        [JsonProperty("idToken"), Required, MaxLength(36)]
        public string IdToken { get; set; } = null!;

        [JsonProperty("type"), Required]
        public IdTokenEnumType Type { get; set; }

        [JsonProperty("additionalInfo")]
        public List<AdditionalInfoType>? AdditionalInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class MessageContentType
    {
        [JsonProperty("format"), Required]
        public MessageFormatEnumType Format { get; set; }

        [JsonProperty("content"), Required, MaxLength(1024)]
        public string Content { get; set; } = null!;

        [JsonProperty("language"), MaxLength(8)]
        public string? Language { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class IdTokenInfoType
    {
        [JsonProperty("status"), Required]
        public AuthorizationStatusEnumType Status { get; set; }

        [JsonProperty("cacheExpiryDateTime")]
        public DateTime? CacheExpiryDateTime { get; set; }

        [JsonProperty("chargingPriority")]
        public int? ChargingPriority { get; set; }

        [JsonProperty("groupIdToken")]
        public IdTokenType? GroupIdToken { get; set; }

        [JsonProperty("language1"), MaxLength(8)]
        public string? Language1 { get; set; }

        [JsonProperty("language2"), MaxLength(8)]
        public string? Language2 { get; set; }

        [JsonProperty("personalMessage")]
        public MessageContentType? PersonalMessage { get; set; }

        [JsonProperty("evseId")]
        public List<int>? EvseId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class EVSEType
    {
        [JsonProperty("id"), Required]
        public int Id { get; set; }

        [JsonProperty("connectorId")]
        public int? ConnectorId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class AddressType
    {
        [JsonProperty("name"), Required, MaxLength(50)]
        public string Name { get; set; } = null!;

        [JsonProperty("address1"), Required, MaxLength(100)]
        public string Address1 { get; set; } = null!;

        [JsonProperty("address2"), MaxLength(100)]
        public string? Address2 { get; set; }

        [JsonProperty("city"), Required, MaxLength(100)]
        public string City { get; set; } = null!;

        [JsonProperty("postalCode"), MaxLength(20)]
        public string? PostalCode { get; set; }

        [JsonProperty("country"), Required, MaxLength(2)]
        public string Country { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── Transaction ───────────────────────────────────────────────────────────────

    public class TransactionType
    {
        [JsonProperty("transactionId"), Required, MaxLength(36)]
        public string TransactionId { get; set; } = null!;

        [JsonProperty("chargingState")]
        public ChargingStateEnumType? ChargingState { get; set; }

        [JsonProperty("timeSpentCharging")]
        public int? TimeSpentCharging { get; set; }

        [JsonProperty("stoppedReason")]
        public ReasonEnumType? StoppedReason { get; set; }

        [JsonProperty("operationMode")]
        public OperationModeEnumType? OperationMode { get; set; }

        [JsonProperty("remoteStartId")]
        public int? RemoteStartId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class UnitOfMeasureType
    {
        [JsonProperty("unit"), MaxLength(20)]
        public string? Unit { get; set; }

        [JsonProperty("multiplier")]
        public int? Multiplier { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SignedMeterValueType
    {
        [JsonProperty("signedMeterData"), Required, MaxLength(2500)]
        public string SignedMeterData { get; set; } = null!;

        [JsonProperty("signingMethod"), Required, MaxLength(50)]
        public string SigningMethod { get; set; } = null!;

        [JsonProperty("encodingMethod"), Required, MaxLength(50)]
        public string EncodingMethod { get; set; } = null!;

        [JsonProperty("publicKey"), Required, MaxLength(2500)]
        public string PublicKey { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SampledValueType
    {
        [JsonProperty("value"), Required]
        public decimal Value { get; set; }

        [JsonProperty("context")]
        public ReadingContextEnumType? Context { get; set; }

        [JsonProperty("measurand")]
        public MeasurandEnumType? Measurand { get; set; }

        [JsonProperty("phase")]
        public PhaseEnumType? Phase { get; set; }

        [JsonProperty("location")]
        public LocationEnumType? Location { get; set; }

        [JsonProperty("signedMeterValue")]
        public SignedMeterValueType? SignedMeterValue { get; set; }

        [JsonProperty("unitOfMeasure")]
        public UnitOfMeasureType? UnitOfMeasure { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class MeterValueType
    {
        [JsonProperty("timestamp"), Required]
        public DateTime Timestamp { get; set; }

        [JsonProperty("sampledValue"), Required]
        public List<SampledValueType> SampledValue { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class TransactionLimitType
    {
        [JsonProperty("maxCost")]
        public decimal? MaxCost { get; set; }

        [JsonProperty("maxEnergy")]
        public decimal? MaxEnergy { get; set; }

        [JsonProperty("maxTime")]
        public int? MaxTime { get; set; }

        [JsonProperty("maxSoC")]
        public int? MaxSoC { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class CostDetailsType
    {
        [JsonProperty("totalFixedCost")]
        public decimal? TotalFixedCost { get; set; }

        [JsonProperty("totalEnergyCost")]
        public decimal? TotalEnergyCost { get; set; }

        [JsonProperty("totalTimeCost")]
        public decimal? TotalTimeCost { get; set; }

        [JsonProperty("totalFlatFee")]
        public decimal? TotalFlatFee { get; set; }

        [JsonProperty("totalCost"), Required]
        public decimal TotalCost { get; set; }

        [JsonProperty("currency"), Required, MaxLength(3)]
        public string Currency { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── Charging Profiles ─────────────────────────────────────────────────────────

    public class ChargingSchedulePeriodType
    {
        [JsonProperty("startPeriod"), Required]
        public int StartPeriod { get; set; }

        [JsonProperty("limit"), Required]
        public decimal Limit { get; set; }

        [JsonProperty("numberPhases")]
        public int? NumberPhases { get; set; }

        [JsonProperty("phaseToUse")]
        public int? PhaseToUse { get; set; }

        [JsonProperty("dischargeLimit")]
        public decimal? DischargeLimit { get; set; }

        [JsonProperty("setpoint")]
        public decimal? Setpoint { get; set; }

        [JsonProperty("setpointReactive")]
        public decimal? SetpointReactive { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SalesTariffEntryType
    {
        [JsonProperty("ePriceLevel")]
        public int? EPriceLevel { get; set; }

        [JsonProperty("relativeTimeInterval"), Required]
        public RelativeTimeIntervalType RelativeTimeInterval { get; set; } = null!;

        [JsonProperty("consumptionCost")]
        public List<ConsumptionCostType>? ConsumptionCost { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class RelativeTimeIntervalType
    {
        [JsonProperty("start"), Required]
        public int Start { get; set; }

        [JsonProperty("duration")]
        public int? Duration { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ConsumptionCostType
    {
        [JsonProperty("startValue"), Required]
        public decimal StartValue { get; set; }

        [JsonProperty("cost"), Required]
        public List<CostType> Cost { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class CostType
    {
        [JsonProperty("costKind"), Required]
        public CostKindEnumType CostKind { get; set; }

        [JsonProperty("amount"), Required]
        public int Amount { get; set; }

        [JsonProperty("amountMultiplier")]
        public int? AmountMultiplier { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SalesTariffType
    {
        [JsonProperty("id"), Required]
        public int Id { get; set; }

        [JsonProperty("salesTariffDescription"), MaxLength(32)]
        public string? SalesTariffDescription { get; set; }

        [JsonProperty("numEPriceLevels")]
        public int? NumEPriceLevels { get; set; }

        [JsonProperty("salesTariffEntry"), Required]
        public List<SalesTariffEntryType> SalesTariffEntry { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ChargingScheduleType
    {
        [JsonProperty("id"), Required]
        public int Id { get; set; }

        [JsonProperty("startSchedule")]
        public DateTime? StartSchedule { get; set; }

        [JsonProperty("duration")]
        public int? Duration { get; set; }

        [JsonProperty("chargingRateUnit"), Required]
        public ChargingRateUnitEnumType ChargingRateUnit { get; set; }

        [JsonProperty("minChargingRate")]
        public decimal? MinChargingRate { get; set; }

        [JsonProperty("chargingSchedulePeriod"), Required]
        public List<ChargingSchedulePeriodType> ChargingSchedulePeriod { get; set; } = [];

        [JsonProperty("salesTariff")]
        public SalesTariffType? SalesTariff { get; set; }

        [JsonProperty("powerTolerance")]
        public decimal? PowerTolerance { get; set; }

        [JsonProperty("signatureId")]
        public int? SignatureId { get; set; }

        [JsonProperty("digestValue"), MaxLength(88)]
        public string? DigestValue { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ChargingProfileType
    {
        [JsonProperty("id"), Required]
        public int Id { get; set; }

        [JsonProperty("stackLevel"), Required]
        public int StackLevel { get; set; }

        [JsonProperty("chargingProfilePurpose"), Required]
        public ChargingProfilePurposeEnumType ChargingProfilePurpose { get; set; }

        [JsonProperty("chargingProfileKind"), Required]
        public ChargingProfileKindEnumType ChargingProfileKind { get; set; }

        [JsonProperty("recurrencyKind")]
        public RecurrencyKindEnumType? RecurrencyKind { get; set; }

        [JsonProperty("validFrom")]
        public DateTime? ValidFrom { get; set; }

        [JsonProperty("validTo")]
        public DateTime? ValidTo { get; set; }

        [JsonProperty("transactionId"), MaxLength(36)]
        public string? TransactionId { get; set; }

        [JsonProperty("chargingSchedule"), Required]
        public List<ChargingScheduleType> ChargingSchedule { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ChargingProfileCriterionType
    {
        [JsonProperty("chargingProfilePurpose")]
        public ChargingProfilePurposeEnumType? ChargingProfilePurpose { get; set; }

        [JsonProperty("stackLevel")]
        public int? StackLevel { get; set; }

        [JsonProperty("chargingProfileId")]
        public List<int>? ChargingProfileId { get; set; }

        [JsonProperty("chargingLimitSource")]
        public List<ChargingLimitSourceEnumType>? ChargingLimitSource { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class CompositeScheduleType
    {
        [JsonProperty("evseId"), Required]
        public int EvseId { get; set; }

        [JsonProperty("duration"), Required]
        public int Duration { get; set; }

        [JsonProperty("scheduleStart"), Required]
        public DateTime ScheduleStart { get; set; }

        [JsonProperty("chargingRateUnit"), Required]
        public ChargingRateUnitEnumType ChargingRateUnit { get; set; }

        [JsonProperty("chargingSchedulePeriod"), Required]
        public List<ChargingSchedulePeriodType> ChargingSchedulePeriod { get; set; } = [];

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ChargingLimitType
    {
        [JsonProperty("chargingLimitSource"), Required]
        public ChargingLimitSourceEnumType ChargingLimitSource { get; set; }

        [JsonProperty("isLocalGeneration")]
        public bool? IsLocalGeneration { get; set; }

        [JsonProperty("isGridCritical")]
        public bool? IsGridCritical { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ClearChargingProfileType
    {
        [JsonProperty("evseId")]
        public int? EvseId { get; set; }

        [JsonProperty("chargingProfilePurpose")]
        public ChargingProfilePurposeEnumType? ChargingProfilePurpose { get; set; }

        [JsonProperty("stackLevel")]
        public int? StackLevel { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ChargingScheduleUpdateType
    {
        [JsonProperty("limit")]
        public decimal? Limit { get; set; }

        [JsonProperty("limit_L2")]
        public decimal? LimitL2 { get; set; }

        [JsonProperty("limit_L3")]
        public decimal? LimitL3 { get; set; }

        [JsonProperty("dischargeLimit")]
        public decimal? DischargeLimit { get; set; }

        [JsonProperty("setpoint")]
        public decimal? Setpoint { get; set; }

        [JsonProperty("setpoint_reactive")]
        public decimal? SetpointReactive { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── Variables / Device Model ──────────────────────────────────────────────────

    public class ComponentType
    {
        [JsonProperty("name"), Required, MaxLength(50)]
        public string Name { get; set; } = null!;

        [JsonProperty("instance"), MaxLength(50)]
        public string? Instance { get; set; }

        [JsonProperty("evse")]
        public EVSEType? Evse { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class VariableType
    {
        [JsonProperty("name"), Required, MaxLength(50)]
        public string Name { get; set; } = null!;

        [JsonProperty("instance"), MaxLength(50)]
        public string? Instance { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ComponentVariableType
    {
        [JsonProperty("component"), Required]
        public ComponentType Component { get; set; } = null!;

        [JsonProperty("variable"), Required]
        public VariableType Variable { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetVariableDataType
    {
        [JsonProperty("component"), Required]
        public ComponentType Component { get; set; } = null!;

        [JsonProperty("variable"), Required]
        public VariableType Variable { get; set; } = null!;

        [JsonProperty("attributeType")]
        public AttributeEnumType? AttributeType { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GetVariableResultType
    {
        [JsonProperty("attributeStatus"), Required]
        public GetVariableStatusEnumType AttributeStatus { get; set; }

        [JsonProperty("component"), Required]
        public ComponentType Component { get; set; } = null!;

        [JsonProperty("variable"), Required]
        public VariableType Variable { get; set; } = null!;

        [JsonProperty("attributeType")]
        public AttributeEnumType? AttributeType { get; set; }

        [JsonProperty("attributeValue"), MaxLength(2500)]
        public string? AttributeValue { get; set; }

        [JsonProperty("attributeStatusInfo")]
        public StatusInfoType? AttributeStatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SetVariableDataType
    {
        [JsonProperty("attributeValue"), Required, MaxLength(2500)]
        public string AttributeValue { get; set; } = null!;

        [JsonProperty("component"), Required]
        public ComponentType Component { get; set; } = null!;

        [JsonProperty("variable"), Required]
        public VariableType Variable { get; set; } = null!;

        [JsonProperty("attributeType")]
        public AttributeEnumType? AttributeType { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SetVariableResultType
    {
        [JsonProperty("attributeStatus"), Required]
        public SetVariableStatusEnumType AttributeStatus { get; set; }

        [JsonProperty("component"), Required]
        public ComponentType Component { get; set; } = null!;

        [JsonProperty("variable"), Required]
        public VariableType Variable { get; set; } = null!;

        [JsonProperty("attributeType")]
        public AttributeEnumType? AttributeType { get; set; }

        [JsonProperty("attributeStatusInfo")]
        public StatusInfoType? AttributeStatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class VariableAttributeType
    {
        [JsonProperty("type")]
        public AttributeEnumType? Type { get; set; }

        [JsonProperty("value"), MaxLength(2500)]
        public string? Value { get; set; }

        [JsonProperty("mutability")]
        public MutabilityEnumType? Mutability { get; set; }

        [JsonProperty("persistent")]
        public bool? Persistent { get; set; }

        [JsonProperty("constant")]
        public bool? Constant { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class VariableCharacteristicsType
    {
        [JsonProperty("unit"), MaxLength(16)]
        public string? Unit { get; set; }

        [JsonProperty("dataType"), Required]
        public DataEnumType DataType { get; set; }

        [JsonProperty("minLimit")]
        public decimal? MinLimit { get; set; }

        [JsonProperty("maxLimit")]
        public decimal? MaxLimit { get; set; }

        [JsonProperty("valuesList"), MaxLength(1000)]
        public string? ValuesList { get; set; }

        [JsonProperty("supportsMonitoring"), Required]
        public bool SupportsMonitoring { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ReportDataType
    {
        [JsonProperty("component"), Required]
        public ComponentType Component { get; set; } = null!;

        [JsonProperty("variable"), Required]
        public VariableType Variable { get; set; } = null!;

        [JsonProperty("variableAttribute"), Required]
        public List<VariableAttributeType> VariableAttribute { get; set; } = [];

        [JsonProperty("variableCharacteristics")]
        public VariableCharacteristicsType? VariableCharacteristics { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── Monitoring ────────────────────────────────────────────────────────────────

    public class EventDataType
    {
        [JsonProperty("eventId"), Required]
        public int EventId { get; set; }

        [JsonProperty("timestamp"), Required]
        public DateTime Timestamp { get; set; }

        [JsonProperty("trigger"), Required]
        public EventTriggerEnumType Trigger { get; set; }

        [JsonProperty("actualValue"), Required, MaxLength(2500)]
        public string ActualValue { get; set; } = null!;

        [JsonProperty("eventNotificationType"), Required]
        public EventNotificationEnumType EventNotificationType { get; set; }

        [JsonProperty("component"), Required]
        public ComponentType Component { get; set; } = null!;

        [JsonProperty("variable"), Required]
        public VariableType Variable { get; set; } = null!;

        [JsonProperty("cause")]
        public int? Cause { get; set; }

        [JsonProperty("techCode"), MaxLength(50)]
        public string? TechCode { get; set; }

        [JsonProperty("techInfo"), MaxLength(500)]
        public string? TechInfo { get; set; }

        [JsonProperty("cleared")]
        public bool? Cleared { get; set; }

        [JsonProperty("transactionId"), MaxLength(36)]
        public string? TransactionId { get; set; }

        [JsonProperty("variableMonitoringId")]
        public int? VariableMonitoringId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SetMonitoringDataType
    {
        [JsonProperty("value"), Required]
        public decimal Value { get; set; }

        [JsonProperty("type"), Required]
        public MonitorEnumType Type { get; set; }

        [JsonProperty("component"), Required]
        public ComponentType Component { get; set; } = null!;

        [JsonProperty("variable"), Required]
        public VariableType Variable { get; set; } = null!;

        [JsonProperty("severity"), Required]
        public int Severity { get; set; }

        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("transaction")]
        public bool? Transaction { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class SetMonitoringResultType
    {
        [JsonProperty("status"), Required]
        public SetMonitoringStatusEnumType Status { get; set; }

        [JsonProperty("type"), Required]
        public MonitorEnumType Type { get; set; }

        [JsonProperty("severity"), Required]
        public int Severity { get; set; }

        [JsonProperty("component"), Required]
        public ComponentType Component { get; set; } = null!;

        [JsonProperty("variable"), Required]
        public VariableType Variable { get; set; } = null!;

        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class VariableMonitoringType
    {
        [JsonProperty("id"), Required]
        public int Id { get; set; }

        [JsonProperty("transaction"), Required]
        public bool Transaction { get; set; }

        [JsonProperty("value"), Required]
        public decimal Value { get; set; }

        [JsonProperty("type"), Required]
        public MonitorEnumType Type { get; set; }

        [JsonProperty("severity"), Required]
        public int Severity { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class MonitoringDataType
    {
        [JsonProperty("component"), Required]
        public ComponentType Component { get; set; } = null!;

        [JsonProperty("variable"), Required]
        public VariableType Variable { get; set; } = null!;

        [JsonProperty("variableMonitoring")]
        public List<VariableMonitoringType>? VariableMonitoring { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ClearMonitoringResultType
    {
        [JsonProperty("status"), Required]
        public ClearMonitoringStatusEnumType Status { get; set; }

        [JsonProperty("id"), Required]
        public int Id { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── Certificates ──────────────────────────────────────────────────────────────

    public class OCSPRequestDataType
    {
        [JsonProperty("hashAlgorithm"), Required]
        public HashAlgorithmEnumType HashAlgorithm { get; set; }

        [JsonProperty("issuerNameHash"), Required, MaxLength(128)]
        public string IssuerNameHash { get; set; } = null!;

        [JsonProperty("issuerKeyHash"), Required, MaxLength(128)]
        public string IssuerKeyHash { get; set; } = null!;

        [JsonProperty("serialNumber"), Required, MaxLength(40)]
        public string SerialNumber { get; set; } = null!;

        [JsonProperty("responderURL"), Required, MaxLength(512)]
        public string ResponderURL { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class CertificateHashDataType
    {
        [JsonProperty("hashAlgorithm"), Required]
        public HashAlgorithmEnumType HashAlgorithm { get; set; }

        [JsonProperty("issuerNameHash"), Required, MaxLength(128)]
        public string IssuerNameHash { get; set; } = null!;

        [JsonProperty("issuerKeyHash"), Required, MaxLength(128)]
        public string IssuerKeyHash { get; set; } = null!;

        [JsonProperty("serialNumber"), Required, MaxLength(40)]
        public string SerialNumber { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class CertificateHashDataChainType
    {
        [JsonProperty("certificateType"), Required]
        public GetCertificateIdUseEnumType CertificateType { get; set; }

        [JsonProperty("certificateHashData"), Required]
        public CertificateHashDataType CertificateHashData { get; set; } = null!;

        [JsonProperty("childCertificateHashData")]
        public List<CertificateHashDataType>? ChildCertificateHashData { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class CertificateStatusRequestInfoType
    {
        [JsonProperty("requestedCertificate"), Required]
        public OCSPRequestDataType RequestedCertificate { get; set; } = null!;

        [JsonProperty("source"), Required]
        public CertificateStatusSourceEnumType Source { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class CertificateStatusType
    {
        [JsonProperty("status"), Required]
        public CertificateStatusEnumType Status { get; set; }

        [JsonProperty("source"), Required]
        public CertificateStatusSourceEnumType Source { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── Log / Diagnostics ─────────────────────────────────────────────────────────

    public class LogParametersType
    {
        [JsonProperty("remoteLocation"), Required, MaxLength(512)]
        public string RemoteLocation { get; set; } = null!;

        [JsonProperty("oldestTimestamp")]
        public DateTime? OldestTimestamp { get; set; }

        [JsonProperty("latestTimestamp")]
        public DateTime? LatestTimestamp { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── Network ───────────────────────────────────────────────────────────────────

    public class APNType
    {
        [JsonProperty("apn"), Required, MaxLength(512)]
        public string Apn { get; set; } = null!;

        [JsonProperty("apnAuthentication"), Required]
        public APNAuthenticationEnumType ApnAuthentication { get; set; }

        [JsonProperty("simPin"), MaxLength(20)]
        public string? SimPin { get; set; }

        [JsonProperty("apnUserName"), MaxLength(20)]
        public string? ApnUserName { get; set; }

        [JsonProperty("apnPassword"), MaxLength(20)]
        public string? ApnPassword { get; set; }

        [JsonProperty("preferredNetwork"), MaxLength(6)]
        public string? PreferredNetwork { get; set; }

        [JsonProperty("useOnlyPreferredNetwork")]
        public bool? UseOnlyPreferredNetwork { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class VPNType
    {
        [JsonProperty("server"), Required, MaxLength(512)]
        public string Server { get; set; } = null!;

        [JsonProperty("user"), Required, MaxLength(20)]
        public string User { get; set; } = null!;

        [JsonProperty("group"), MaxLength(20)]
        public string? Group { get; set; }

        [JsonProperty("password"), Required, MaxLength(20)]
        public string Password { get; set; } = null!;

        [JsonProperty("key"), Required, MaxLength(255)]
        public string Key { get; set; } = null!;

        [JsonProperty("type"), Required, MaxLength(20)]
        public string Type { get; set; } = null!;

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class NetworkConnectionProfileType
    {
        [JsonProperty("ocppVersion"), Required]
        public OCPPVersionEnumType OcppVersion { get; set; }

        [JsonProperty("ocppTransport"), Required]
        public OCPPTransportEnumType OcppTransport { get; set; }

        [JsonProperty("ocppCsmsUrl"), Required, MaxLength(512)]
        public string OcppCsmsUrl { get; set; } = null!;

        [JsonProperty("messageTimeout"), Required]
        public int MessageTimeout { get; set; }

        [JsonProperty("securityProfile"), Required]
        public int SecurityProfile { get; set; }

        [JsonProperty("ocppInterface"), Required]
        public OCPPInterfaceEnumType OcppInterface { get; set; }

        [JsonProperty("apn")]
        public APNType? Apn { get; set; }

        [JsonProperty("vpn")]
        public VPNType? Vpn { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── Local Auth ────────────────────────────────────────────────────────────────

    public class AuthorizationData
    {
        [JsonProperty("idToken"), Required]
        public IdTokenType IdToken { get; set; } = null!;

        [JsonProperty("idTokenInfo")]
        public IdTokenInfoType? IdTokenInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── Display Messages ──────────────────────────────────────────────────────────

    public class MessageInfoType
    {
        [JsonProperty("id"), Required]
        public int Id { get; set; }

        [JsonProperty("priority"), Required]
        public MessagePriorityEnumType Priority { get; set; }

        [JsonProperty("message"), Required]
        public MessageContentType Message { get; set; } = null!;

        [JsonProperty("state")]
        public MessageStateEnumType? State { get; set; }

        [JsonProperty("startDateTime")]
        public DateTime? StartDateTime { get; set; }

        [JsonProperty("endDateTime")]
        public DateTime? EndDateTime { get; set; }

        [JsonProperty("transactionId"), MaxLength(36)]
        public string? TransactionId { get; set; }

        [JsonProperty("display")]
        public ComponentType? Display { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── Firmware ──────────────────────────────────────────────────────────────────

    public class FirmwareType
    {
        [JsonProperty("location"), Required, MaxLength(512)]
        public string Location { get; set; } = null!;

        [JsonProperty("retrieveDateTime"), Required]
        public DateTime RetrieveDateTime { get; set; }

        [JsonProperty("installDateTime")]
        public DateTime? InstallDateTime { get; set; }

        [JsonProperty("signingCertificate"), MaxLength(5500)]
        public string? SigningCertificate { get; set; }

        [JsonProperty("signature"), MaxLength(800)]
        public string? Signature { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── EV Charging Needs ─────────────────────────────────────────────────────────

    public class AcChargingParametersType
    {
        [JsonProperty("energyAmount"), Required]
        public int EnergyAmount { get; set; }

        [JsonProperty("evMinCurrent"), Required]
        public int EvMinCurrent { get; set; }

        [JsonProperty("evMaxCurrent"), Required]
        public int EvMaxCurrent { get; set; }

        [JsonProperty("evMaxVoltage"), Required]
        public int EvMaxVoltage { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class DcChargingParametersType
    {
        [JsonProperty("evMaxCurrent"), Required]
        public int EvMaxCurrent { get; set; }

        [JsonProperty("evMaxVoltage"), Required]
        public int EvMaxVoltage { get; set; }

        [JsonProperty("energyAmount")]
        public int? EnergyAmount { get; set; }

        [JsonProperty("evMinCurrent")]
        public int? EvMinCurrent { get; set; }

        [JsonProperty("evMaxPower")]
        public int? EvMaxPower { get; set; }

        [JsonProperty("stateOfCharge")]
        public int? StateOfCharge { get; set; }

        [JsonProperty("evEnergyCapacity")]
        public int? EvEnergyCapacity { get; set; }

        [JsonProperty("fullSoC")]
        public int? FullSoC { get; set; }

        [JsonProperty("bulkSoC")]
        public int? BulkSoC { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class V2xChargingParametersType
    {
        [JsonProperty("minChargePower")]
        public decimal? MinChargePower { get; set; }

        [JsonProperty("minChargePower_L2")]
        public decimal? MinChargePowerL2 { get; set; }

        [JsonProperty("minChargePower_L3")]
        public decimal? MinChargePowerL3 { get; set; }

        [JsonProperty("maxChargePower")]
        public decimal? MaxChargePower { get; set; }

        [JsonProperty("maxChargePower_L2")]
        public decimal? MaxChargePowerL2 { get; set; }

        [JsonProperty("maxChargePower_L3")]
        public decimal? MaxChargePowerL3 { get; set; }

        [JsonProperty("minDischargePower")]
        public decimal? MinDischargePower { get; set; }

        [JsonProperty("maxDischargePower")]
        public decimal? MaxDischargePower { get; set; }

        [JsonProperty("minChargeEnergy")]
        public decimal? MinChargeEnergy { get; set; }

        [JsonProperty("maxChargeEnergy")]
        public decimal? MaxChargeEnergy { get; set; }

        [JsonProperty("minDischargeEnergy")]
        public decimal? MinDischargeEnergy { get; set; }

        [JsonProperty("maxDischargeEnergy")]
        public decimal? MaxDischargeEnergy { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ChargingNeedsType
    {
        [JsonProperty("requestedEnergyTransfer"), Required]
        public EnergyTransferModeEnumType RequestedEnergyTransfer { get; set; }

        [JsonProperty("acChargingParameters")]
        public AcChargingParametersType? AcChargingParameters { get; set; }

        [JsonProperty("dcChargingParameters")]
        public DcChargingParametersType? DcChargingParameters { get; set; }

        [JsonProperty("v2xChargingParameters")]
        public V2xChargingParametersType? V2xChargingParameters { get; set; }

        [JsonProperty("departureTime")]
        public DateTime? DepartureTime { get; set; }

        [JsonProperty("mobilityNeedsMode")]
        public MobilityNeedsModeEnumType? MobilityNeedsMode { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── Tariff (new in OCPP 2.1) ──────────────────────────────────────────────────

    public class TariffConditionsType
    {
        [JsonProperty("startTimeOfDay"), MaxLength(8)]
        public string? StartTimeOfDay { get; set; }

        [JsonProperty("endTimeOfDay"), MaxLength(8)]
        public string? EndTimeOfDay { get; set; }

        [JsonProperty("dayOfWeek")]
        public List<DayOfWeekEnumType>? DayOfWeek { get; set; }

        [JsonProperty("validFrom"), MaxLength(10)]
        public string? ValidFrom { get; set; }

        [JsonProperty("validTo"), MaxLength(10)]
        public string? ValidTo { get; set; }

        [JsonProperty("minEnergy")]
        public decimal? MinEnergy { get; set; }

        [JsonProperty("maxEnergy")]
        public decimal? MaxEnergy { get; set; }

        [JsonProperty("minCurrent")]
        public decimal? MinCurrent { get; set; }

        [JsonProperty("maxCurrent")]
        public decimal? MaxCurrent { get; set; }

        [JsonProperty("minPower")]
        public decimal? MinPower { get; set; }

        [JsonProperty("maxPower")]
        public decimal? MaxPower { get; set; }

        [JsonProperty("minTime")]
        public int? MinTime { get; set; }

        [JsonProperty("maxTime")]
        public int? MaxTime { get; set; }

        [JsonProperty("minSoC")]
        public int? MinSoC { get; set; }

        [JsonProperty("maxSoC")]
        public int? MaxSoC { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class TariffEnergyPriceType
    {
        [JsonProperty("priceKwh"), Required]
        public decimal PriceKwh { get; set; }

        [JsonProperty("conditions")]
        public TariffConditionsType? Conditions { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class TariffEnergyType
    {
        [JsonProperty("prices")]
        public List<TariffEnergyPriceType>? Prices { get; set; }

        [JsonProperty("taxRates")]
        public List<TaxRateType>? TaxRates { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class TariffTimePriceType
    {
        [JsonProperty("priceMinute"), Required]
        public decimal PriceMinute { get; set; }

        [JsonProperty("conditions")]
        public TariffConditionsType? Conditions { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class TariffTimeType
    {
        [JsonProperty("prices")]
        public List<TariffTimePriceType>? Prices { get; set; }

        [JsonProperty("taxRates")]
        public List<TaxRateType>? TaxRates { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class TariffFixedFeeType
    {
        [JsonProperty("prices")]
        public List<TariffFixedPriceType>? Prices { get; set; }

        [JsonProperty("taxRates")]
        public List<TaxRateType>? TaxRates { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class TariffFixedPriceType
    {
        [JsonProperty("priceFixed"), Required]
        public decimal PriceFixed { get; set; }

        [JsonProperty("conditions")]
        public TariffConditionsType? Conditions { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class TaxRateType
    {
        [JsonProperty("type"), Required, MaxLength(20)]
        public string Type { get; set; } = null!;

        [JsonProperty("tax"), Required]
        public decimal Tax { get; set; }

        [JsonProperty("stack")]
        public int? Stack { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class TariffType
    {
        [JsonProperty("tariffId"), Required, MaxLength(36)]
        public string TariffId { get; set; } = null!;

        [JsonProperty("currency"), Required, MaxLength(3)]
        public string Currency { get; set; } = null!;

        [JsonProperty("tariffKind")]
        public TariffKindEnumType? TariffKind { get; set; }

        [JsonProperty("description")]
        public List<MessageContentType>? Description { get; set; }

        [JsonProperty("minCost")]
        public TariffFixedFeeType? MinCost { get; set; }

        [JsonProperty("maxCost")]
        public TariffFixedFeeType? MaxCost { get; set; }

        [JsonProperty("energy")]
        public TariffEnergyType? Energy { get; set; }

        [JsonProperty("chargingTime")]
        public TariffTimeType? ChargingTime { get; set; }

        [JsonProperty("idleTime")]
        public TariffTimeType? IdleTime { get; set; }

        [JsonProperty("fixedFee")]
        public TariffFixedFeeType? FixedFee { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class TariffAssignmentType
    {
        [JsonProperty("tariffId"), Required, MaxLength(36)]
        public string TariffId { get; set; } = null!;

        [JsonProperty("tariffKind"), Required]
        public TariffKindEnumType TariffKind { get; set; }

        [JsonProperty("evseIds")]
        public List<int>? EvseIds { get; set; }

        [JsonProperty("idTokens")]
        public List<string>? IdTokens { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ClearTariffsResultType
    {
        [JsonProperty("status"), Required]
        public TariffClearStatusEnumType Status { get; set; }

        [JsonProperty("tariffId"), MaxLength(36)]
        public string? TariffId { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── Battery Swap (new in OCPP 2.1) ────────────────────────────────────────────

    public class BatteryDataType
    {
        [JsonProperty("evseId"), Required]
        public int EvseId { get; set; }

        [JsonProperty("serialNumber"), MaxLength(25)]
        public string? SerialNumber { get; set; }

        [JsonProperty("soC")]
        public int? SoC { get; set; }

        [JsonProperty("soH")]
        public int? SoH { get; set; }

        [JsonProperty("productionDate"), MaxLength(10)]
        public string? ProductionDate { get; set; }

        [JsonProperty("vendorInfo"), MaxLength(100)]
        public string? VendorInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── Periodic Event Stream (new in OCPP 2.1) ───────────────────────────────────

    public class PeriodicEventStreamParamsType
    {
        [JsonProperty("interval"), Required]
        public int Interval { get; set; }

        [JsonProperty("values"), Required]
        public int Values { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ConstantStreamDataType
    {
        [JsonProperty("id"), Required]
        public int Id { get; set; }

        [JsonProperty("params"), Required]
        public PeriodicEventStreamParamsType Params { get; set; } = null!;

        [JsonProperty("variableMonitoringId"), Required]
        public int VariableMonitoringId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // ── DER Control (new in OCPP 2.1) ─────────────────────────────────────────────

    public class DERCurvePointType
    {
        [JsonProperty("x"), Required]
        public decimal X { get; set; }

        [JsonProperty("y"), Required]
        public decimal Y { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class DERCurveType
    {
        [JsonProperty("priority"), Required]
        public int Priority { get; set; }

        [JsonProperty("curveData"), Required]
        public List<DERCurvePointType> CurveData { get; set; } = [];

        [JsonProperty("yUnit"), Required]
        public DERUnitEnumType YUnit { get; set; }

        [JsonProperty("responseTime")]
        public decimal? ResponseTime { get; set; }

        [JsonProperty("startTime")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("duration")]
        public decimal? Duration { get; set; }

        [JsonProperty("hysteresis")]
        public decimal? Hysteresis { get; set; }

        [JsonProperty("islandingDetection")]
        public List<IslandingDetectionEnumType>? IslandingDetection { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class EnterServiceType
    {
        [JsonProperty("priority"), Required]
        public int Priority { get; set; }

        [JsonProperty("highFreq"), Required]
        public decimal HighFreq { get; set; }

        [JsonProperty("lowFreq"), Required]
        public decimal LowFreq { get; set; }

        [JsonProperty("highVoltage"), Required]
        public decimal HighVoltage { get; set; }

        [JsonProperty("lowVoltage"), Required]
        public decimal LowVoltage { get; set; }

        [JsonProperty("delay"), Required]
        public decimal Delay { get; set; }

        [JsonProperty("randomisedDelay")]
        public decimal? RandomisedDelay { get; set; }

        [JsonProperty("gradConnect")]
        public decimal? GradConnect { get; set; }

        [JsonProperty("softStartGrad")]
        public decimal? SoftStartGrad { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class FixedPFType
    {
        [JsonProperty("priority"), Required]
        public int Priority { get; set; }

        [JsonProperty("displacement"), Required]
        public decimal Displacement { get; set; }

        [JsonProperty("excitation"), Required]
        public bool Excitation { get; set; }

        [JsonProperty("startTime")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("duration")]
        public decimal? Duration { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class FixedVarType
    {
        [JsonProperty("priority"), Required]
        public int Priority { get; set; }

        [JsonProperty("value"), Required]
        public decimal Value { get; set; }

        [JsonProperty("unit"), Required]
        public DERUnitEnumType Unit { get; set; }

        [JsonProperty("startTime")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("duration")]
        public decimal? Duration { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class FreqDroopType
    {
        [JsonProperty("priority"), Required]
        public int Priority { get; set; }

        [JsonProperty("overFreq"), Required]
        public decimal OverFreq { get; set; }

        [JsonProperty("underFreq"), Required]
        public decimal UnderFreq { get; set; }

        [JsonProperty("overDroop"), Required]
        public decimal OverDroop { get; set; }

        [JsonProperty("underDroop"), Required]
        public decimal UnderDroop { get; set; }

        [JsonProperty("responseTime")]
        public decimal? ResponseTime { get; set; }

        [JsonProperty("randomisedDelay")]
        public decimal? RandomisedDelay { get; set; }

        [JsonProperty("startTime")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("duration")]
        public decimal? Duration { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class GradientType
    {
        [JsonProperty("priority"), Required]
        public int Priority { get; set; }

        [JsonProperty("gradPlus")]
        public decimal? GradPlus { get; set; }

        [JsonProperty("gradMinus")]
        public decimal? GradMinus { get; set; }

        [JsonProperty("startTime")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("duration")]
        public decimal? Duration { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class LimitMaxDischargeType
    {
        [JsonProperty("priority"), Required]
        public int Priority { get; set; }

        [JsonProperty("pct")]
        public decimal? Pct { get; set; }

        [JsonProperty("value")]
        public decimal? Value { get; set; }

        [JsonProperty("unit")]
        public DERUnitEnumType? Unit { get; set; }

        [JsonProperty("startTime")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("duration")]
        public decimal? Duration { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    // "Get" variants returned by ReportDERControl (include controlId and isDefault)
    public class DERControlGetBase
    {
        [JsonProperty("controlId"), Required, MaxLength(36)]
        public string ControlId { get; set; } = null!;

        [JsonProperty("isDefault"), Required]
        public bool IsDefault { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class DERCurveGetType : DERControlGetBase
    {
        [JsonProperty("curve"), Required]
        public DERCurveType Curve { get; set; } = null!;
    }

    public class EnterServiceGetType : DERControlGetBase
    {
        [JsonProperty("enterService"), Required]
        public EnterServiceType EnterService { get; set; } = null!;
    }

    public class FixedPFGetType : DERControlGetBase
    {
        [JsonProperty("fixedPF"), Required]
        public FixedPFType FixedPF { get; set; } = null!;
    }

    public class FixedVarGetType : DERControlGetBase
    {
        [JsonProperty("fixedVar"), Required]
        public FixedVarType FixedVar { get; set; } = null!;
    }

    public class FreqDroopGetType : DERControlGetBase
    {
        [JsonProperty("freqDroop"), Required]
        public FreqDroopType FreqDroop { get; set; } = null!;
    }

    public class GradientGetType : DERControlGetBase
    {
        [JsonProperty("gradient"), Required]
        public GradientType Gradient { get; set; } = null!;
    }

    public class LimitMaxDischargeGetType : DERControlGetBase
    {
        [JsonProperty("limitMaxDischarge"), Required]
        public LimitMaxDischargeType LimitMaxDischarge { get; set; } = null!;
    }
}
