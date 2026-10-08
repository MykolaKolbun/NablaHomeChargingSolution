using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class FirmwareStatusNotificationRequest
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        // Enum for the status values, representing the allowed states
        public enum StatusEnum
        {
            Downloaded,
            DownloadFailed,
            Downloading,
            Idle,
            InstallationFailed,
            Installing,
            Installed
        }

        // Constructor to ensure the status is valid
        public FirmwareStatusNotificationRequest(string status)
        {
            if (!Enum.IsDefined(typeof(StatusEnum), status))
            {
                throw new ArgumentException("Invalid status value.");
            }
            Status = status;
        }
    }
}
