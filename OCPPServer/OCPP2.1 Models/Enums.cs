using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OCPP_RD.OCPP2._1_Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum APNAuthenticationEnumType
    {
        PAP, CHAP, None, Auto
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum AttributeEnumType
    {
        Actual, Target, MinSet, MaxSet
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum AuthorizationStatusEnumType
    {
        Accepted, Blocked, ConcurrentTx, Expired, Invalid,
        NoCredit, NotAllowedTypeEVSE, NotAtThisLocation, NotAtThisTime, Unknown
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum AuthorizeCertificateStatusEnumType
    {
        Accepted, SignatureError, CertificateExpired, CertificateRevoked,
        NoCertificateAvailable, CertChainError, ContractCancelled
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BatterySwapEventEnumType
    {
        BatteryIn, BatteryOut, BatteryOutTimeout
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BootReasonEnumType
    {
        ApplicationReset, FirmwareUpdate, LocalReset, PowerUp,
        RemoteReset, ScheduledReset, Triggered, Unknown, Watchdog
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CancelReservationStatusEnumType
    {
        Accepted, Rejected
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CertificateActionEnumType
    {
        Install, Update
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CertificateSignedStatusEnumType
    {
        Accepted, Rejected
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CertificateSigningUseEnumType
    {
        ChargingStationCertificate,
        [EnumMember(Value = "V2GCertificate")]
        V2GCertificate,
        [EnumMember(Value = "V2G20Certificate")]
        V2G20Certificate
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CertificateStatusEnumType
    {
        Good, Revoked, Unknown, Failed
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CertificateStatusSourceEnumType
    {
        [EnumMember(Value = "CRL")]
        CRL,
        [EnumMember(Value = "OCSP")]
        OCSP
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ChangeAvailabilityStatusEnumType
    {
        Accepted, Rejected, Scheduled
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ChargingLimitSourceEnumType
    {
        [EnumMember(Value = "EMS")]
        EMS, Other,
        [EnumMember(Value = "SO")]
        SO,
        [EnumMember(Value = "CSO")]
        CSO
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ChargingProfileKindEnumType
    {
        Absolute, Recurring, Relative, Dynamic
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ChargingProfilePurposeEnumType
    {
        ChargingStationExternalConstraints, ChargingStationMaxProfile,
        TxDefaultProfile, TxProfile, PriorityCharging, LocalGeneration
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ChargingProfileStatusEnumType
    {
        Accepted, Rejected
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ChargingRateUnitEnumType
    {
        [EnumMember(Value = "W")] W,
        [EnumMember(Value = "A")] A
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ChargingStateEnumType
    {
        [EnumMember(Value = "EVConnected")] EVConnected,
        Charging, SuspendedEV, SuspendedEVSE, Idle
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ClearCacheStatusEnumType
    {
        Accepted, Rejected
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ClearChargingProfileStatusEnumType
    {
        Accepted, Unknown
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ClearMessageStatusEnumType
    {
        Accepted, Unknown, Rejected
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ClearMonitoringStatusEnumType
    {
        Accepted, Rejected, NotFound
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ComponentCriterionEnumType
    {
        Active, Available, Enabled, Problem
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ConnectorStatusEnumType
    {
        Available, Occupied, Reserved, Unavailable, Faulted
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ControlModeEnumType
    {
        ScheduledControl, DynamicControl
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CostDimensionEnumType
    {
        Energy, MaxCurrent, MinCurrent, MaxPower, MinPower, IdleTime, ChargingTime
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CostKindEnumType
    {
        CarbonDioxideEmission, RelativePricePercentage, RenewableGenerationPercentage
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CustomerInformationStatusEnumType
    {
        Accepted, Rejected, Invalid
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DataEnumType
    {
        [EnumMember(Value = "string")]    String,
        [EnumMember(Value = "decimal")]   Decimal,
        [EnumMember(Value = "integer")]   Integer,
        [EnumMember(Value = "dateTime")]  DateTime,
        [EnumMember(Value = "boolean")]   Boolean,
        OptionList, SequenceList, MemberList
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DataTransferStatusEnumType
    {
        Accepted, Rejected, UnknownMessageId, UnknownVendorId
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DayOfWeekEnumType
    {
        Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DeleteCertificateStatusEnumType
    {
        Accepted, Failed, NotFound
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DERControlEnumType
    {
        EnterService, FreqDroop, FreqWatt, FixedPFAbsorb, FixedPFInject, FixedVar,
        Gradients, HFMustTrip, HFMayTrip, HVMustTrip, HVMomCess, HVMayTrip,
        LimitMaxDischarge, LFMustTrip, LVMustTrip, LVMomCess, LVMayTrip,
        PowerMonitoringMustTrip, VoltVar, VoltWatt, WattPF, WattVar
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DERControlStatusEnumType
    {
        Accepted, Rejected, NotSupported, NotFound
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DERUnitEnumType
    {
        NotApplicable, PctMaxW, PctMaxVar, PctWAvail, PctVarAvail, PctEffectiveV
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DisplayMessageStatusEnumType
    {
        Accepted, NotSupportedMessageFormat, Rejected, NotSupportedPriority,
        NotSupportedState, UnknownTransaction, LanguageNotSupported
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum EnergyTransferModeEnumType
    {
        [EnumMember(Value = "AC_single_phase")]   AC_single_phase,
        [EnumMember(Value = "AC_two_phase")]      AC_two_phase,
        [EnumMember(Value = "AC_three_phase")]    AC_three_phase,
        [EnumMember(Value = "DC")]                DC,
        [EnumMember(Value = "AC_BPT")]            AC_BPT,
        [EnumMember(Value = "AC_BPT_DER")]        AC_BPT_DER,
        [EnumMember(Value = "AC_DER")]            AC_DER,
        [EnumMember(Value = "DC_BPT")]            DC_BPT,
        [EnumMember(Value = "DC_ACDP")]           DC_ACDP,
        [EnumMember(Value = "DC_ACDP_BPT")]       DC_ACDP_BPT,
        [EnumMember(Value = "WPT")]               WPT
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum EventNotificationEnumType
    {
        HardWiredNotification, HardWiredMonitor, PreconfiguredMonitor, CustomMonitor
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum EventTriggerEnumType
    {
        Alerting, Delta, Periodic
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum EvseKindEnumType
    {
        [EnumMember(Value = "AC")] AC,
        [EnumMember(Value = "DC")] DC
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum FirmwareStatusEnumType
    {
        Downloaded, DownloadFailed, Downloading, DownloadScheduled, DownloadPaused,
        Idle, InstallationFailed, Installing, Installed, InstallRebooting,
        InstallScheduled, InstallVerificationFailed, InvalidSignature, SignatureVerified
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum GenericDeviceModelStatusEnumType
    {
        Accepted, Rejected, NotSupported, EmptyResultSet
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum GenericStatusEnumType
    {
        Accepted, Rejected
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum GetCertificateIdUseEnumType
    {
        [EnumMember(Value = "V2GRootCertificate")]          V2GRootCertificate,
        [EnumMember(Value = "MORootCertificate")]           MORootCertificate,
        CsmsRootCertificate,
        [EnumMember(Value = "V2GCertificateChain")]         V2GCertificateChain,
        ManufacturerRootCertificate,
        [EnumMember(Value = "OEMRootCertificate")]          OEMRootCertificate
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum GetCertificateStatusEnumType
    {
        Accepted, Failed
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum GetChargingProfileStatusEnumType
    {
        Accepted, NoProfiles
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum GetDisplayMessagesStatusEnumType
    {
        Accepted, Unknown
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum GetInstalledCertificateStatusEnumType
    {
        Accepted, NotFound
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum GetVariableStatusEnumType
    {
        Accepted, Rejected, UnknownComponent, UnknownVariable, NotSupportedAttributeType
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum GridEventFaultEnumType
    {
        CurrentImbalance, LocalEmergency, LowInputPower, OverCurrent, OverFrequency,
        OverVoltage, PhaseRotation, RemoteEmergency, UnderFrequency, UnderVoltage, VoltageImbalance
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum HashAlgorithmEnumType
    {
        [EnumMember(Value = "SHA256")] SHA256,
        [EnumMember(Value = "SHA384")] SHA384,
        [EnumMember(Value = "SHA512")] SHA512
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum IdTokenEnumType
    {
        Central,
        [EnumMember(Value = "eMAID")]           eMAID,
        [EnumMember(Value = "ISO14443")]         ISO14443,
        [EnumMember(Value = "ISO15693")]         ISO15693,
        KeyCode, Local, MacAddress, NoAuthorization
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum InstallCertificateStatusEnumType
    {
        Accepted, Rejected, Failed
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum InstallCertificateUseEnumType
    {
        [EnumMember(Value = "V2GRootCertificate")]   V2GRootCertificate,
        [EnumMember(Value = "MORootCertificate")]    MORootCertificate,
        ManufacturerRootCertificate, CsmsRootCertificate,
        [EnumMember(Value = "OEMRootCertificate")]   OEMRootCertificate
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum IslandingDetectionEnumType
    {
        NoAntiIslandingSupport, RoCoF, UVPOVP, UFPOFP, VoltageVectorShift,
        ZeroCrossingDetection, OtherPassive, ImpedanceMeasurement, ImpedanceAtFrequency,
        SlipModeFrequencyShift, SandiaFrequencyShift, SandiaVoltageShift,
        FrequencyJump, RCLQFactor, OtherActive
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum Iso15118EVCertificateStatusEnumType
    {
        Accepted, Failed
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum LocationEnumType
    {
        Body, Cable,
        [EnumMember(Value = "EV")] EV,
        Inlet, Outlet, Upstream
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum LogEnumType
    {
        DiagnosticsLog, SecurityLog, DataCollectorLog
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum LogStatusEnumType
    {
        Accepted, Rejected, AcceptedCanceled
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MeasurandEnumType
    {
        [EnumMember(Value = "Current.Export")]                         CurrentExport,
        [EnumMember(Value = "Current.Export.Offered")]                 CurrentExportOffered,
        [EnumMember(Value = "Current.Export.Minimum")]                 CurrentExportMinimum,
        [EnumMember(Value = "Current.Import")]                         CurrentImport,
        [EnumMember(Value = "Current.Import.Offered")]                 CurrentImportOffered,
        [EnumMember(Value = "Current.Import.Minimum")]                 CurrentImportMinimum,
        [EnumMember(Value = "Current.Offered")]                        CurrentOffered,
        [EnumMember(Value = "Display.PresentSoC")]                     DisplayPresentSoC,
        [EnumMember(Value = "Display.MinimumSoC")]                     DisplayMinimumSoC,
        [EnumMember(Value = "Display.TargetSoC")]                      DisplayTargetSoC,
        [EnumMember(Value = "Display.MaximumSoC")]                     DisplayMaximumSoC,
        [EnumMember(Value = "Display.RemainingTimeToMinimumSoC")]      DisplayRemainingTimeToMinimumSoC,
        [EnumMember(Value = "Display.RemainingTimeToTargetSoC")]       DisplayRemainingTimeToTargetSoC,
        [EnumMember(Value = "Display.RemainingTimeToMaximumSoC")]      DisplayRemainingTimeToMaximumSoC,
        [EnumMember(Value = "Display.ChargingComplete")]               DisplayChargingComplete,
        [EnumMember(Value = "Display.BatteryEnergyCapacity")]          DisplayBatteryEnergyCapacity,
        [EnumMember(Value = "Display.InletHot")]                       DisplayInletHot,
        [EnumMember(Value = "Energy.Active.Export.Interval")]          EnergyActiveExportInterval,
        [EnumMember(Value = "Energy.Active.Export.Register")]          EnergyActiveExportRegister,
        [EnumMember(Value = "Energy.Active.Import.Interval")]          EnergyActiveImportInterval,
        [EnumMember(Value = "Energy.Active.Import.Register")]          EnergyActiveImportRegister,
        [EnumMember(Value = "Energy.Active.Import.CableLoss")]         EnergyActiveImportCableLoss,
        [EnumMember(Value = "Energy.Active.Import.LocalGenerationRegister")] EnergyActiveImportLocalGenerationRegister,
        [EnumMember(Value = "Energy.Active.Net")]                      EnergyActiveNet,
        [EnumMember(Value = "Energy.Active.Setpoint.Interval")]        EnergyActiveSetpointInterval,
        [EnumMember(Value = "Energy.Apparent.Export")]                 EnergyApparentExport,
        [EnumMember(Value = "Energy.Apparent.Import")]                 EnergyApparentImport,
        [EnumMember(Value = "Energy.Apparent.Net")]                    EnergyApparentNet,
        [EnumMember(Value = "Energy.Reactive.Export.Interval")]        EnergyReactiveExportInterval,
        [EnumMember(Value = "Energy.Reactive.Export.Register")]        EnergyReactiveExportRegister,
        [EnumMember(Value = "Energy.Reactive.Import.Interval")]        EnergyReactiveImportInterval,
        [EnumMember(Value = "Energy.Reactive.Import.Register")]        EnergyReactiveImportRegister,
        [EnumMember(Value = "Energy.Reactive.Net")]                    EnergyReactiveNet,
        [EnumMember(Value = "Energy.Request.Target")]                  EnergyRequestTarget,
        [EnumMember(Value = "Energy.Request.Minimum")]                 EnergyRequestMinimum,
        [EnumMember(Value = "Energy.Request.Maximum")]                 EnergyRequestMaximum,
        [EnumMember(Value = "Energy.Request.Minimum.V2X")]             EnergyRequestMinimumV2X,
        [EnumMember(Value = "Energy.Request.Maximum.V2X")]             EnergyRequestMaximumV2X,
        [EnumMember(Value = "Energy.Request.Bulk")]                    EnergyRequestBulk,
        Frequency,
        [EnumMember(Value = "Power.Active.Export")]                    PowerActiveExport,
        [EnumMember(Value = "Power.Active.Import")]                    PowerActiveImport,
        [EnumMember(Value = "Power.Active.Setpoint")]                  PowerActiveSetpoint,
        [EnumMember(Value = "Power.Active.Residual")]                  PowerActiveResidual,
        [EnumMember(Value = "Power.Export.Minimum")]                   PowerExportMinimum,
        [EnumMember(Value = "Power.Export.Offered")]                   PowerExportOffered,
        [EnumMember(Value = "Power.Factor")]                           PowerFactor,
        [EnumMember(Value = "Power.Import.Offered")]                   PowerImportOffered,
        [EnumMember(Value = "Power.Import.Minimum")]                   PowerImportMinimum,
        [EnumMember(Value = "Power.Offered")]                          PowerOffered,
        [EnumMember(Value = "Power.Reactive.Export")]                  PowerReactiveExport,
        [EnumMember(Value = "Power.Reactive.Import")]                  PowerReactiveImport,
        [EnumMember(Value = "SoC")]                                    SoC,
        Voltage,
        [EnumMember(Value = "Voltage.Minimum")]                        VoltageMinimum,
        [EnumMember(Value = "Voltage.Maximum")]                        VoltageMaximum
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MessageFormatEnumType
    {
        [EnumMember(Value = "ASCII")]   ASCII,
        [EnumMember(Value = "HTML")]    HTML,
        [EnumMember(Value = "URI")]     URI,
        [EnumMember(Value = "UTF8")]    UTF8,
        [EnumMember(Value = "QRCode")]  QRCode
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MessagePriorityEnumType
    {
        AlwaysFront, InFront, NormalCycle
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MessageStateEnumType
    {
        Charging, Faulted, Idle, Unavailable, Suspended, Discharging
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MessageTriggerEnumType
    {
        BootNotification, LogStatusNotification, FirmwareStatusNotification,
        Heartbeat, MeterValues, SignChargingStationCertificate, SignV2GCertificate,
        SignV2G20Certificate, StatusNotification, TransactionEvent,
        SignCombinedCertificate, PublishFirmwareStatusNotification, CustomTrigger
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MobilityNeedsModeEnumType
    {
        [EnumMember(Value = "EVCC")]       EVCC,
        [EnumMember(Value = "EVCC_SECC")]  EVCC_SECC
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MonitorEnumType
    {
        UpperThreshold, LowerThreshold, Delta, Periodic,
        PeriodicClockAligned, TargetDelta, TargetDeltaRelative
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MonitoringBaseEnumType
    {
        All, FactoryDefault, HardWiredOnly
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MonitoringCriterionEnumType
    {
        ThresholdMonitoring, DeltaMonitoring, PeriodicMonitoring
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MutabilityEnumType
    {
        ReadOnly, WriteOnly, ReadWrite
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum NotifyAllowedEnergyTransferStatusEnumType
    {
        Accepted, Rejected
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum NotifyEVChargingNeedsStatusEnumType
    {
        Accepted, Rejected, Processing, NoChargingProfile
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum OCPPInterfaceEnumType
    {
        Wired0, Wired1, Wired2, Wired3, Wireless0, Wireless1, Wireless2, Wireless3, Any
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum OCPPTransportEnumType
    {
        [EnumMember(Value = "SOAP")] SOAP,
        [EnumMember(Value = "JSON")] JSON
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum OCPPVersionEnumType
    {
        [EnumMember(Value = "OCPP12")]  OCPP12,
        [EnumMember(Value = "OCPP15")]  OCPP15,
        [EnumMember(Value = "OCPP16")]  OCPP16,
        [EnumMember(Value = "OCPP20")]  OCPP20,
        [EnumMember(Value = "OCPP201")] OCPP201,
        [EnumMember(Value = "OCPP21")]  OCPP21
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum OperationModeEnumType
    {
        Idle, ChargingOnly, CentralSetpoint, ExternalSetpoint, ExternalLimits,
        CentralFrequency, LocalFrequency, LocalLoadBalancing
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum OperationalStatusEnumType
    {
        Inoperative, Operative
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PaymentStatusEnumType
    {
        Settled, Canceled, Rejected, Failed
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PhaseEnumType
    {
        L1, L2, L3, N,
        [EnumMember(Value = "L1-N")] L1_N,
        [EnumMember(Value = "L2-N")] L2_N,
        [EnumMember(Value = "L3-N")] L3_N,
        [EnumMember(Value = "L1-L2")] L1_L2,
        [EnumMember(Value = "L2-L3")] L2_L3,
        [EnumMember(Value = "L3-L1")] L3_L1
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PowerDuringCessationEnumType
    {
        Active, Reactive
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PreconditioningStatusEnumType
    {
        Unknown, Ready, NotReady, Preconditioning
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PriorityChargingStatusEnumType
    {
        Accepted, Rejected, NoProfile
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PublishFirmwareStatusEnumType
    {
        Idle, DownloadScheduled, Downloading, Downloaded, Published,
        DownloadFailed, DownloadPaused, InvalidChecksum, ChecksumVerified, PublishFailed
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReadingContextEnumType
    {
        [EnumMember(Value = "Interruption.Begin")] InterruptionBegin,
        [EnumMember(Value = "Interruption.End")]   InterruptionEnd,
        Other,
        [EnumMember(Value = "Sample.Clock")]       SampleClock,
        [EnumMember(Value = "Sample.Periodic")]    SamplePeriodic,
        [EnumMember(Value = "Transaction.Begin")]  TransactionBegin,
        [EnumMember(Value = "Transaction.End")]    TransactionEnd,
        Trigger
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReasonEnumType
    {
        DeAuthorized, EmergencyStop, EnergyLimitReached, EVDisconnected, GroundFault,
        ImmediateReset, MasterPass, Local, LocalOutOfCredit, Other, OvercurrentFault,
        PowerLoss, PowerQuality, Reboot, Remote, SOCLimitReached, StoppedByEV,
        TimeLimitReached, Timeout, ReqEnergyTransferRejected
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum RecurrencyKindEnumType
    {
        Daily, Weekly
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum RegistrationStatusEnumType
    {
        Accepted, Pending, Rejected
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReportBaseEnumType
    {
        ConfigurationInventory, FullInventory, SummaryInventory
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum RequestStartStopStatusEnumType
    {
        Accepted, Rejected
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReservationUpdateStatusEnumType
    {
        Expired, Removed, NoTransaction
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReserveNowStatusEnumType
    {
        Accepted, Faulted, Occupied, Rejected, Unavailable
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ResetEnumType
    {
        Immediate, OnIdle, ImmediateAndResume
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ResetStatusEnumType
    {
        Accepted, Rejected, Scheduled
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum SendLocalListStatusEnumType
    {
        Accepted, Failed, VersionMismatch
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum SetMonitoringStatusEnumType
    {
        Accepted, UnknownComponent, UnknownVariable, UnsupportedMonitorType, Rejected, Duplicate
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum SetNetworkProfileStatusEnumType
    {
        Accepted, Rejected, Failed
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum SetVariableStatusEnumType
    {
        Accepted, Rejected, UnknownComponent, UnknownVariable,
        NotSupportedAttributeType, RebootRequired
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TariffChangeStatusEnumType
    {
        Accepted, Rejected, TooManyElements, ConditionNotSupported, TxNotFound, NoCurrencyChange
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TariffClearStatusEnumType
    {
        Accepted, Rejected, NoTariff
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TariffCostEnumType
    {
        NormalCost, MinCost, MaxCost
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TariffGetStatusEnumType
    {
        Accepted, Rejected, NoTariff
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TariffKindEnumType
    {
        DefaultTariff, DriverTariff
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TariffSetStatusEnumType
    {
        Accepted, Rejected, TooManyElements, ConditionNotSupported, DuplicateTariffId
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TransactionEventEnumType
    {
        Ended, Started, Updated
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TriggerMessageStatusEnumType
    {
        Accepted, Rejected, NotImplemented
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TriggerReasonEnumType
    {
        AbnormalCondition, Authorized, CablePluggedIn, ChargingRateChanged,
        ChargingStateChanged, CostLimitReached, Deauthorized, EnergyLimitReached,
        EVCommunicationLost, EVConnectTimeout, EVDeparted, EVDetected, LimitSet,
        MeterValueClock, MeterValuePeriodic, OperationModeChanged, RemoteStart,
        RemoteStop, ResetCommand, RunningCost, SignedDataReceived, SoCLimitReached,
        StopAuthorized, TariffChanged, TariffNotAccepted, TimeLimitReached,
        Trigger, TxResumed, UnlockCommand
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum UnlockStatusEnumType
    {
        Unlocked, UnlockFailed, OngoingAuthorizedTransaction, UnknownConnector
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum UnpublishFirmwareStatusEnumType
    {
        DownloadOngoing, NoFirmware, Unpublished
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum UpdateEnumType
    {
        Differential, Full
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum UpdateFirmwareStatusEnumType
    {
        Accepted, Rejected, AcceptedCanceled, InvalidCertificate, RevokedCertificate
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum UploadLogStatusEnumType
    {
        BadMessage, Idle, NotSupportedOperation, PermissionDenied,
        Uploaded, UploadFailure, Uploading, AcceptedCanceled
    }

    /// <summary>
    /// OCPP 2.1 message type identifiers (extends 1.6 with SEND=6 and CALLRESULTERROR=5).
    /// </summary>
    public enum Ocpp21MessageType
    {
        Call            = 2,
        CallResult      = 3,
        CallError       = 4,
        CallResultError = 5,
        Send            = 6
    }
}
