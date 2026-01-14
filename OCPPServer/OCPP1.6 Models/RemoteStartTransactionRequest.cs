using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class RemoteStartTransactionRequest
    {
        [JsonProperty("connectorId")]
        public int ConnectorId { get; set; }

        [JsonProperty("idTag")]
        public string IdTag { get; set; }

        [JsonProperty("chargingProfile")]
        public ChargingProfile chargingProfile { get; set; }

        public class ChargingProfile
        {
            [JsonProperty("chargingProfileId")]
            public int ChargingProfileId { get; set; }

            [JsonProperty("transactionId")]
            public int TransactionId { get; set; }

            [JsonProperty("stackLevel")]
            public int StackLevel { get; set; }

            [JsonProperty("chargingProfilePurpose")]
            public ChargingProfilePurpose ChargingProfilePurpose { get; set; }

            [JsonProperty("chargingProfileKind")]
            public ChargingProfileKind ChargingProfileKind { get; set; }

            [JsonProperty("recurrencyKind")]
            public RecurrencyKind RecurrencyKind { get; set; }

            [JsonProperty("validFrom")]
            public DateTime? ValidFrom { get; set; }

            [JsonProperty("validTo")]
            public DateTime? ValidTo { get; set; }

            [JsonProperty("chargingSchedule")]
            public ChargingSchedule ChargingSchedule { get; set; }
        }

        public class ChargingSchedule
        {
            [JsonProperty("duration")]
            public int Duration { get; set; }

            [JsonProperty("startSchedule")]
            public DateTime StartSchedule { get; set; }

            [JsonProperty("chargingRateUnit")]
            public ChargingRateUnit ChargingRateUnit { get; set; }

            [JsonProperty("chargingSchedulePeriod")]
            public List<ChargingSchedulePeriod> ChargingSchedulePeriod { get; set; }

            [JsonProperty("minChargingRate")]
            public decimal MinChargingRate { get; set; }
        }

        public class ChargingSchedulePeriod
        {
            [JsonProperty("startPeriod")]
            public int StartPeriod { get; set; }

            [JsonProperty("limit")]
            public decimal Limit { get; set; }

            [JsonProperty("numberPhases")]
            public int NumberPhases { get; set; }
        }

        public enum ChargingProfilePurpose
        {
            ChargePointMaxProfile,
            TxDefaultProfile,
            TxProfile
        }

        public enum ChargingProfileKind
        {
            Absolute,
            Recurring,
            Relative
        }

        public enum RecurrencyKind
        {
            Daily,
            Weekly
        }

        public enum ChargingRateUnit
        {
            A,
            W
        }
    }
}
