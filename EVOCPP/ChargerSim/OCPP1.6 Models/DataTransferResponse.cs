using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class DataTransferResponse
    {
        /// <summary>
        /// The status of the data transfer request. Possible values: Accepted, Rejected, UnknownMessageId, UnknownVendorId.
        /// </summary>
        [JsonProperty("status")]
        [Required(ErrorMessage = "The status field is required.")]
        [EnumDataType(typeof(DataTransferStatus), ErrorMessage = "Invalid status value.")]
        public DataTransferStatus Status { get; set; }

        /// <summary>
        /// The optional data returned as part of the response.
        /// </summary>
        [JsonProperty("data")]
        public string Data { get; set; }
    }

    /// <summary>
    /// Enum for status values in the DataTransferResponse.
    /// </summary>
    public enum DataTransferStatus
    {
        Accepted,
        Rejected,
        UnknownMessageId,
        UnknownVendorId
    }
}
