using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class ClearChargingProfileResponse
    {
        /// <summary>
        /// The status of the clear charging profile request. Possible values: Accepted, Unknown.
        /// </summary>
        [JsonProperty("status")]
        [Required(ErrorMessage = "The status field is required.")]
        [EnumDataType(typeof(ClearChargingProfileStatus), ErrorMessage = "Invalid status value.")]
        public ClearChargingProfileStatus Status { get; set; }
    }

    /// <summary>
    /// Enum for status values in the ClearChargingProfileResponse.
    /// </summary>
    public enum ClearChargingProfileStatus
    {
        Accepted,
        Unknown
    }
}
