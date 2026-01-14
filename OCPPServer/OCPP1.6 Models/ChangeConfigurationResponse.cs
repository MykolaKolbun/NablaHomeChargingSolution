using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class ChangeConfigurationResponse
    {
        /// <summary>
        /// The status of the change configuration request. Possible values: Accepted, Rejected, RebootRequired, NotSupported.
        /// </summary>
        [JsonProperty("status")]
        [Required(ErrorMessage = "The status field is required.")]
        [EnumDataType(typeof(ChangeConfigurationStatus), ErrorMessage = "Invalid status value.")]
        public ChangeConfigurationStatus Status { get; set; }


    }
    /// <summary>
    /// Enum for status values in the ChangeConfigurationResponse.
    /// </summary>
    public enum ChangeConfigurationStatus
    {
        Accepted,
        Rejected,
        RebootRequired,
        NotSupported
    }

}
