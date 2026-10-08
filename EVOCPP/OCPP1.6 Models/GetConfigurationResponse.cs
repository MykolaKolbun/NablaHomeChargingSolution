using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class GetConfigurationResponse
    {
        [JsonProperty("configurationKey")]
        public List<ConfigurationKey> configurationKey { get; set; }

        [JsonProperty("unknownKey")]
        public List<string> UnknownKey { get; set; }

        public class ConfigurationKey
        {
            [JsonProperty("key")]
            public string Key { get; set; }

            [JsonProperty("readonly")]
            public bool Readonly { get; set; }

            [JsonProperty("value")]
            public string Value { get; set; }
        }
    }
}
