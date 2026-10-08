using Newtonsoft.Json;

namespace OCPP_RD.OCPP2._1_Models
{
    /// <summary>CS → CSMS. Reports a change to the status of a reservation.</summary>
    public class ReservationStatusUpdateRequest
    {
        [JsonProperty("reservationId")]
        public int ReservationId { get; set; }

        [JsonProperty("reservationUpdateStatus")]
        public ReservationUpdateStatusEnumType ReservationUpdateStatus { get; set; }

        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }

    public class ReservationStatusUpdateResponse
    {
        [JsonProperty("customData")]
        public CustomDataType? CustomData { get; set; }
    }
}
