using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class CancelReservationRequest
    {
        /// <summary>
        /// The ID of the reservation to cancel.
        /// </summary>
        [JsonProperty("reservationId")]
        [Required(ErrorMessage = "The reservationId field is required.")]
        public int ReservationId { get; set; }
    }
}
