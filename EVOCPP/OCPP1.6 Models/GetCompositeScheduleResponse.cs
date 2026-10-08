using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class GetCompositeScheduleResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("connectorId")]
        public int ConnectorId { get; set; }

        [JsonProperty("scheduleStart")]
        public DateTime ScheduleStart { get; set; }

        [JsonProperty("chargingSchedule")]
        public ChargingSchedule chargingSchedule { get; set; }

        // Enum for status values
        public enum StatusEnum
        {
            Accepted,
            Rejected
        }

        public class ChargingSchedule
        {
            [JsonProperty("duration")]
            public int Duration { get; set; }

            [JsonProperty("startSchedule")]
            public DateTime StartSchedule { get; set; }

            [JsonProperty("chargingRateUnit")]
            public string ChargingRateUnit { get; set; }

            // Enum for the allowed values of ChargingRateUnit
            public enum ChargingRateUnitEnum
            {
                A, // Amperes
                W  // Watts
            }

            [JsonProperty("chargingSchedulePeriod")]
            public List<ChargingSchedulePeriod> ChargingSchedulePeriod { get; set; }

            [JsonProperty("minChargingRate")]
            public double MinChargingRate { get; set; }
        }

        public class ChargingSchedulePeriod
        {
            [JsonProperty("startPeriod")]
            public int StartPeriod { get; set; }

            [JsonProperty("limit")]
            public double Limit { get; set; }

            [JsonProperty("numberPhases")]
            public int NumberPhases { get; set; }
        }

        // Constructor to ensure valid status and chargingRateUnit
        public GetCompositeScheduleResponse(string status, int connectorId, DateTime scheduleStart, ChargingSchedule chargingSchedule)
        {
            if (!Enum.IsDefined(typeof(StatusEnum), status))
            {
                throw new ArgumentException("Invalid status value.");
            }

            Status = status;
            ConnectorId = connectorId;
            ScheduleStart = scheduleStart;
            chargingSchedule = this.chargingSchedule;
        }
    }
}
