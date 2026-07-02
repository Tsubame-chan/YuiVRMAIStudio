using System;
using Newtonsoft.Json;

namespace YuiPhysicalAI.LocalAI
{
    [Serializable]
    public sealed class YuiGoogleAiEdgeBridgeRequest
    {
        [JsonProperty("capability")]
        public string Capability { get; set; }

        [JsonProperty("model_pack_id")]
        public string ModelPackId { get; set; }

        [JsonProperty("model_path")]
        public string ModelPath { get; set; }

        [JsonProperty("cache_directory")]
        public string CacheDirectory { get; set; }

        [JsonProperty("runtime_model_ref")]
        public string RuntimeModelRef { get; set; }

        [JsonProperty("system_instruction")]
        public string SystemInstruction { get; set; }

        [JsonProperty("payload_json")]
        public string PayloadJson { get; set; }
    }

    [Serializable]
    public sealed class YuiGoogleAiEdgeBridgeResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("error_code")]
        public string ErrorCode { get; set; }

        [JsonProperty("error_message")]
        public string ErrorMessage { get; set; }

        [JsonProperty("model_id")]
        public string ModelId { get; set; }

        [JsonProperty("payload_json")]
        public string PayloadJson { get; set; }
    }
}
