using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP_RD.OCPP1._6_Models
{
    public class StopTransactionRequest
    {
        [JsonProperty("idTag")]
        public string IdTag { get; set; }

        [JsonProperty("meterStop")]
        public int MeterStop { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("transactionId")]
        public int TransactionId { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("transactionData")]
        public List<TransactionData> TransactionData { get; set; }
    }

    public class TransactionData
    {
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("sampledValue")]
        public List<SampledValue> SampledValue { get; set; }
    }

    public class SampledValue
    {
        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("context")]
        public string Context { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("measurand")]
        public string Measurand { get; set; }

        [JsonProperty("phase")]
        public string Phase { get; set; }

        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; }
    }
}
