using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class ClearChargingProfileRequest
    {
        /// <summary>
        /// The ID of the charging profile to clear (optional).
        /// </summary>
        [JsonProperty("id")]
        public int? Id { get; set; }

        /// <summary>
        /// The ID of the connector for which the charging profile should be cleared (optional).
        /// </summary>
        [JsonProperty("connectorId")]
        public int? ConnectorId { get; set; }

        /// <summary>
        /// The purpose of the charging profile. Possible values: ChargePointMaxProfile, TxDefaultProfile, TxProfile.
        /// </summary>
        [JsonProperty("chargingProfilePurpose")]
        [EnumDataType(typeof(ChargingProfilePurposeEnum), ErrorMessage = "Invalid charging profile purpose.")]
        public ChargingProfilePurposeEnum? ChargingProfilePurpose { get; set; }

        /// <summary>
        /// The stack level of the charging profile to clear (optional).
        /// </summary>
        [JsonProperty("stackLevel")]
        public int? StackLevel { get; set; }
    }

    /// <summary>
    /// Enum for charging profile purpose values.
    /// </summary>
    public enum ChargingProfilePurposeEnum
    {
        ChargePointMaxProfile,
        TxDefaultProfile,
        TxProfile
    }
}
