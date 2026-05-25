using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class DataTransferRequest
    {
        /// <summary>
        /// The vendor ID associated with the data transfer. Required field.
        /// </summary>
        [JsonProperty("vendorId")]
        [Required(ErrorMessage = "The vendorId field is required.")]
        [StringLength(255, ErrorMessage = "The vendorId cannot exceed 255 characters.")]
        public string VendorId { get; set; }

        /// <summary>
        /// The message ID for the data transfer. Optional field, max length is 50 characters.
        /// </summary>
        [JsonProperty("messageId")]
        [StringLength(50, ErrorMessage = "The messageId cannot exceed 50 characters.")]
        public string MessageId { get; set; }

        /// <summary>
        /// The data associated with the transfer. Optional field.
        /// </summary>
        [JsonProperty("data")]
        public string Data { get; set; }
    }
}
