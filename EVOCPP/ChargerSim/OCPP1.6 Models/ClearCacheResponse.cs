using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class ClearCacheResponse
    {
        /// <summary>
        /// The status of the clear cache request. Possible values: Accepted, Rejected.
        /// </summary>
        [JsonProperty("status")]
        [Required(ErrorMessage = "The status field is required.")]
        [EnumDataType(typeof(ClearCacheStatus), ErrorMessage = "Invalid status value.")]
        public ClearCacheStatus Status { get; set; }
    }

    /// <summary>
    /// Enum for status values in the ClearCacheResponse.
    /// </summary>
    public enum ClearCacheStatus
    {
        Accepted,
        Rejected
    }
}
