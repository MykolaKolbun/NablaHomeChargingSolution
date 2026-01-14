using Newtonsoft.Json.Converters;
using Newtonsoft.Json;

namespace OCPP_RD.OCPP1._6_Models
{
    public class ChangeAvailabilityResponse
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public StatusEnum Status { get; set; }

        public enum StatusEnum
        {
            Accepted,
            Rejected,
            Scheduled
        }
    }
}
