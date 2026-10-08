using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace OCPP_RD.OCPP1._6_Models
{
    public class AuthorizeRequest
    {
        /// <summary>
        /// The identifier tag used for authorization. Max length is 20 characters.
        /// </summary>
        [JsonProperty("idTag")]
        [Required(ErrorMessage = "The idTag field is required.")]
        [StringLength(20, ErrorMessage = "The idTag field must not exceed 20 characters.")]
        public string IdTag { get; set; }
    }
}
