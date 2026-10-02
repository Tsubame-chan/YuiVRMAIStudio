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

        [JsonProperty("supports_thinking")]
        public bool SupportsThinking { get; set; }

        [JsonProperty("thinking_token_budget")]
        public int ThinkingTokenBudget { get; set; } = 768;

        [JsonProperty("max_output_tokens")]
        public int MaxOutputTokens { get; set; } = 1280;

        [JsonProperty("supports_speculative_decoding")]
        public bool SupportsSpeculativeDecoding { get; set; }

        [JsonProperty("context_tokens")]
        public int ContextTokens { get; set; } = 8192;
        [JsonProperty("timeout_seconds")]
        public int TimeoutSeconds { get; set; } = 120;
        [JsonProperty("temperature")]
        public float Temperature { get; set; } = .65f;
        [JsonProperty("top_k")]
        public int TopK { get; set; } = 30;
        [JsonProperty("top_p")]
        public float TopP { get; set; } = .85f;

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
