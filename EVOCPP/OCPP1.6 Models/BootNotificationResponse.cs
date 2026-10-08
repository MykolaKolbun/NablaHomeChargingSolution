using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class BootNotificationResponse
    {
        /// <summary>
        /// The status of the boot notification request. Possible values: Accepted, Pending, Rejected.
        /// </summary>
        [JsonProperty("status")]
        [Required(ErrorMessage = "The status field is required.")]
        [EnumDataType(typeof(BootNotificationStatusEnum), ErrorMessage = "Invalid status value.")]
        public BootNotificationStatusEnum Status { get; set; }

        /// <summary>
        /// The current time at the server in ISO 8601 format.
        /// </summary>
        [JsonProperty("currentTime")]
        [Required(ErrorMessage = "The currentTime field is required.")]
        public DateTime CurrentTime { get; set; }

        /// <summary>
        /// Heartbeat interval in seconds.
        /// </summary>
        [JsonProperty("interval")]
        [Required(ErrorMessage = "The interval field is required.")]
        public int Interval { get; set; }
    }

    /// <summary>
    /// Enum for status values in the BootNotificationResponse.
    /// </summary>
    public enum BootNotificationStatusEnum
    {
        Accepted,
        Pending,
        Rejected
    }
}
