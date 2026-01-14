using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace OCPP_RD.OCPP1._6_Models
{
    public class BootNotificationRequest
    {
        /// <summary>
        /// The vendor of the charge point. Max length is 20 characters.
        /// </summary>
        [JsonProperty("chargePointVendor")]
        [Required(ErrorMessage = "The chargePointVendor field is required.")]
        [StringLength(20, ErrorMessage = "The chargePointVendor field must not exceed 20 characters.")]
        public string ChargePointVendor { get; set; }

        /// <summary>
        /// The model of the charge point. Max length is 20 characters.
        /// </summary>
        [JsonProperty("chargePointModel")]
        [Required(ErrorMessage = "The chargePointModel field is required.")]
        [StringLength(20, ErrorMessage = "The chargePointModel field must not exceed 20 characters.")]
        public string ChargePointModel { get; set; }

        /// <summary>
        /// The serial number of the charge point. Max length is 25 characters.
        /// </summary>
        [JsonProperty("chargePointSerialNumber")]
        [StringLength(25, ErrorMessage = "The chargePointSerialNumber field must not exceed 25 characters.")]
        public string ChargePointSerialNumber { get; set; }

        /// <summary>
        /// The serial number of the charge box. Max length is 25 characters.
        /// </summary>
        [JsonProperty("chargeBoxSerialNumber")]
        [StringLength(25, ErrorMessage = "The chargeBoxSerialNumber field must not exceed 25 characters.")]
        public string ChargeBoxSerialNumber { get; set; }

        /// <summary>
        /// The firmware version. Max length is 50 characters.
        /// </summary>
        [JsonProperty("firmwareVersion")]
        [StringLength(50, ErrorMessage = "The firmwareVersion field must not exceed 50 characters.")]
        public string FirmwareVersion { get; set; }

        /// <summary>
        /// The ICCID of the SIM card. Max length is 20 characters.
        /// </summary>
        [JsonProperty("iccid")]
        [StringLength(20, ErrorMessage = "The iccid field must not exceed 20 characters.")]
        public string Iccid { get; set; }

        /// <summary>
        /// The IMSI of the SIM card. Max length is 20 characters.
        /// </summary>
        [JsonProperty("imsi")]
        [StringLength(20, ErrorMessage = "The imsi field must not exceed 20 characters.")]
        public string Imsi { get; set; }

        /// <summary>
        /// The type of meter. Max length is 25 characters.
        /// </summary>
        [JsonProperty("meterType")]
        [StringLength(25, ErrorMessage = "The meterType field must not exceed 25 characters.")]
        public string MeterType { get; set; }

        /// <summary>
        /// The serial number of the meter. Max length is 25 characters.
        /// </summary>
        [JsonProperty("meterSerialNumber")]
        [StringLength(25, ErrorMessage = "The meterSerialNumber field must not exceed 25 characters.")]
        public string MeterSerialNumber { get; set; }
    }
}
