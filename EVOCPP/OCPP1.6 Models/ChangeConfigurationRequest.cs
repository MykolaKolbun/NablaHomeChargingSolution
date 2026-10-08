using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace OCPP_RD.OCPP1._6_Models
{
    public class ChangeConfigurationRequest
    {
        /// <summary>
        /// The key of the configuration setting to change.
        /// </summary>
        [JsonProperty("key")]
        [Required(ErrorMessage = "The key field is required.")]
        [StringLength(50, ErrorMessage = "The key cannot exceed 50 characters.")]
        public string Key { get; set; }

        /// <summary>
        /// The value to set for the configuration key.
        /// </summary>
        [JsonProperty("value")]
        [Required(ErrorMessage = "The value field is required.")]
        [StringLength(500, ErrorMessage = "The value cannot exceed 500 characters.")]
        public string Value { get; set; }
    }
}
