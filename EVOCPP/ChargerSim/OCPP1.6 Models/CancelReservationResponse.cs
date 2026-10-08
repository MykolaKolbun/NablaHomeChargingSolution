using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class CancelReservationResponse
    {
        /// <summary>
        /// The status of the cancel reservation request. Possible values: Accepted, Rejected.
        /// </summary>
        [JsonProperty("status")]
        [Required(ErrorMessage = "The status field is required.")]
        [EnumDataType(typeof(CancelReservationStatusEnum), ErrorMessage = "Invalid status value.")]
        public CancelReservationStatusEnum Status { get; set; }
    }

    /// <summary>
    /// Enum for status values in the CancelReservationResponse.
    /// </summary>
    public enum CancelReservationStatusEnum
    {
        Accepted,
        Rejected
    }
}
