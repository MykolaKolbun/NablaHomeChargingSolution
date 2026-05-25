using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class ChangeAvailabilityRequest
    {
        /// <summary>
        /// The ID of the connector to change availability for.
        /// </summary>
        [JsonProperty("connectorId")]
        [Required(ErrorMessage = "The connectorId field is required.")]
        public int ConnectorId { get; set; }

        /// <summary>
        /// The availability status of the connector. Possible values: Inoperative, Operative.
        /// </summary>
        [JsonProperty("type")]
        [Required(ErrorMessage = "The type field is required.")]
        [EnumDataType(typeof(AvailabilityType), ErrorMessage = "Invalid type value.")]
        public AvailabilityType Type { get; set; }
    }

    /// <summary>
    /// Enum for type values in the ChangeAvailabilityRequest.
    /// </summary>
    public enum AvailabilityType
    {
        Inoperative,
        Operative
    }
}
