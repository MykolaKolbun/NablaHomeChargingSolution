using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class SendLocalListRequest
    {
        [JsonProperty("listVersion")]
        public int ListVersion { get; set; }

        [JsonProperty("localAuthorizationList")]
        public List<LocalAuthorization> LocalAuthorizationList { get; set; }

        [JsonProperty("updateType")]
        public UpdateType updateType { get; set; }

        public enum UpdateType
        {
            Differential,
            Full
        }

        public class LocalAuthorization
        {
            [JsonProperty("idTag")]
            public string IdTag { get; set; }

            [JsonProperty("idTagInfo")]
            public IdTagInfo IdTagInfo { get; set; }
        }

        public class IdTagInfo
        {
            [JsonProperty("expiryDate")]
            public DateTime ExpiryDate { get; set; }

            [JsonProperty("parentIdTag")]
            public string ParentIdTag { get; set; }

            [JsonProperty("status")]
            public Status status { get; set; }

            public enum Status
            {
                Accepted,
                Blocked,
                Expired,
                Invalid,
                ConcurrentTx
            }
        }
    }
}
