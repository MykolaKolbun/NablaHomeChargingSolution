using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class SetChargingProfileRequest
    {
        [JsonProperty("connectorId")]
        public int ConnectorId { get; set; }

        [JsonProperty("csChargingProfiles")]
        public CsChargingProfiles csChargingProfiles { get; set; }

        public class CsChargingProfiles
        {
            [JsonProperty("chargingProfileId")]
            public int ChargingProfileId { get; set; }

            [JsonProperty("transactionId")]
            public int TransactionId { get; set; }

            [JsonProperty("stackLevel")]
            public int StackLevel { get; set; }

            [JsonProperty("chargingProfilePurpose")]
            public ChargingProfilePurpose chargingProfilePurpose { get; set; }

            [JsonProperty("chargingProfileKind")]
            public ChargingProfileKind chargingProfileKind { get; set; }

            [JsonProperty("recurrencyKind")]
            public RecurrencyKind recurrencyKind { get; set; }

            [JsonProperty("validFrom")]
            public DateTime ValidFrom { get; set; }

            [JsonProperty("validTo")]
            public DateTime ValidTo { get; set; }

            [JsonProperty("chargingSchedule")]
            public ChargingSchedule chargingSchedule { get; set; }

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

            public class ChargingSchedule
            {
                [JsonProperty("duration")]
                public int Duration { get; set; }

                [JsonProperty("startSchedule")]
                public DateTime StartSchedule { get; set; }

                [JsonProperty("chargingRateUnit")]
                public ChargingRateUnit chargingRateUnit { get; set; }

                [JsonProperty("chargingSchedulePeriod")]
                public List<ChargingSchedulePeriod> chargingSchedulePeriod { get; set; }

                [JsonProperty("minChargingRate")]
                public decimal MinChargingRate { get; set; }

                public enum ChargingRateUnit
                {
                    A,
                    W
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
            }
        }
    }
}
