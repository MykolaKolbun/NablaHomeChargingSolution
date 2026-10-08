using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class DiagnosticsStatusNotificationRequest
    {
        /// <summary>
        /// The status of the diagnostics notification. Possible values: Idle, Uploaded, UploadFailed, Uploading.
        /// </summary>
        [JsonProperty("status")]
        [Required(ErrorMessage = "The status field is required.")]
        [EnumDataType(typeof(DiagnosticsStatus), ErrorMessage = "Invalid status value.")]
        public DiagnosticsStatus Status { get; set; }
    }

    /// <summary>
    /// Enum for status values in the DiagnosticsStatusNotificationRequest.
    /// </summary>
    public enum DiagnosticsStatus
    {
        Idle,
        Uploaded,
        UploadFailed,
        Uploading
    }
}
