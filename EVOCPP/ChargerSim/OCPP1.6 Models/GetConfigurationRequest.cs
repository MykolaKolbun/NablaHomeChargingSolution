using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class GetConfigurationRequest
    {
        [JsonProperty("key")]
        public List<string> Key { get; set; }

        // Constructor to initialize the key list
        public GetConfigurationRequest(List<string> key)
        {
            if (key != null)
            {
                foreach (var k in key)
                {
                    if (k.Length > 50)
                    {
                        throw new ArgumentException("Each key cannot exceed 50 characters.");
                    }
                }
            }

            Key = key;
        }
    }
}
