using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP_RD.OCPP1._6_Models
{
    public class IdTagInfo
    {
        /// <summary>
        /// The expiry date of the authorization, in ISO 8601 format.
        /// </summary>
        [JsonProperty("expiryDate")]
        public DateTime? ExpiryDate { get; set; }

        /// <summary>
        /// The parent identifier tag associated with the current tag.
        /// </summary>
        [JsonProperty("parentIdTag")]
        [StringLength(20, ErrorMessage = "The parentIdTag field must not exceed 20 characters.")]
        public string ParentIdTag { get; set; }

        /// <summary>
        /// The status of the authorization request.
        /// </summary>
        [JsonProperty("status")]
        [Required(ErrorMessage = "The status field is required.")]
        [EnumDataType(typeof(StatusEnum), ErrorMessage = "Invalid status value.")]
        public StatusEnum Status { get; set; }
    }

    /// <summary>
    /// Enum for status values in the idTagInfo object.
    /// </summary>
    public enum StatusEnum
    {
        Accepted,
        Blocked,
        Expired,
        Invalid,
        ConcurrentTx
    }
}
