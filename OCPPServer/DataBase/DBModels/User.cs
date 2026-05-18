using OCPPServer.ChargingStationInterface;
using System.ComponentModel.DataAnnotations;

namespace OCPPServer.DataBase.DBModels
{
    public class User
    {
        /// <summary>
        /// Gets or sets the unique identifier for the user.
        /// </summary>
        [Key]
        public int UserId { get; set; }

        /// <summary>
        /// Gets or sets the username of the user.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the phone number of the user.
        /// </summary>
        [Required]
        [Phone]
        public string PhoneNr { get; set; }

        /// <summary>
        /// Gets or sets the email address of the user.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the electric vehicle identifier associated with the user.
        /// </summary>
        public string EVId { get; set; }

        // ---- Relationships ----

        // 1 User -> many ChargingSessions

        /// <summary>
        /// Gets or sets the collection of charging sessions associated with the user.
        /// </summary>
        public virtual ICollection<ChargingSessionInfo> ChargingSessions { get; set; }
            = new List<ChargingSessionInfo>();
    }
}
