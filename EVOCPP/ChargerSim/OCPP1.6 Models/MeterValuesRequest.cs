using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public partial class MeterValuesRequest
    {
        [JsonProperty("connectorId")]
        public int ConnectorId { get; set; }

        [JsonProperty("transactionId")]
        public int TransactionId { get; set; }

        [JsonProperty("meterValue")]
        public List<MeterValue> meterValue { get; set; }

        public enum Context
        {
            InterruptionBegin,
            InterruptionEnd,
            SampleClock,
            SamplePeriodic,
            TransactionBegin,
            TransactionEnd,
            Trigger,
            Other
        }

        public enum Format
        {
            Raw,
            SignedData
        }

        public enum Measurand
        {
            EnergyActiveExportRegister,
            EnergyActiveImportRegister,
            EnergyReactiveExportRegister,
            EnergyReactiveImportRegister,
            EnergyActiveExportInterval,
            EnergyActiveImportInterval,
            EnergyReactiveExportInterval,
            EnergyReactiveImportInterval,
            PowerActiveExport,
            PowerActiveImport,
            PowerOffered,
            PowerReactiveExport,
            PowerReactiveImport,
            PowerFactor,
            CurrentImport,
            CurrentExport,
            CurrentOffered,
            Voltage,
            Frequency,
            Temperature,
            SoC,
            RPM
        }

        public enum Phase
        {
            L1,
            L2,
            L3,
            N,
            L1N,
            L2N,
            L3N,
            L1L2,
            L2L3,
            L3L1
        }

        public enum Location
        {
            Cable,
            EV,
            Inlet,
            Outlet,
            Body
        }

        public enum Unit
        {
            Wh,
            kWh,
            varh,
            kvarh,
            W,
            kW,
            VA,
            kVA,
            var,
            kvar,
            A,
            V,
            K,
            Celsius,
            Fahrenheit,
            Percent
        }
    }
}
