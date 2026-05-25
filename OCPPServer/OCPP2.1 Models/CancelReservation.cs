using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CSMS → CS. Cancels an existing reservation.</summary>
    public class CancelReservationRequest
    {
        [JsonProperty("reservationId")]
        public int ReservationId { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class CancelReservationResponse
    {
        [JsonProperty("status")]
        public CancelReservationStatusEnumType Status { get; set; }

        [JsonProperty("statusInfo")]
        public StatusInfoType? StatusInfo { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
