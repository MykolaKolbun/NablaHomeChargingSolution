using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace OCPP_RD.OCPP1._6_Models
{
    public class AuthorizeResponse
    {
        /// <summary>
        /// Information about the identifier tag.
        /// </summary>
        [JsonProperty("idTagInfo")]
        [Required(ErrorMessage = "The idTagInfo field is required.")]
        public IdTagInfo IdTagInfo { get; set; }
    }
}
