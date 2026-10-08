using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace YuiPhysicalAI.LocalAI
{
    public enum YuiLocalAiCapability
    {
        Chat,
        Transcription,
        SpeechSynthesis,
        Vision,
        VoiceChat,
        Summarization,
        Translation,
        Extraction
    }

    public enum YuiLocalAiStartupPolicy
    {
        Manual,
        OnDemand,
        WarmTextOnly
    }

    public enum YuiLocalAiRuntimeMode
    {
        Disabled,
        OnDevice,
        BackendFallback,
        Advanced
    }

    public enum YuiLocalAiDeploymentKind
    {
        OnDeviceEmbedded,
        DesktopAudition,
        BackendFallback,
        Cloud
    }

    [Serializable]
    public sealed class YuiLocalAiModelPack
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("provider")]
        public string Provider { get; set; }

        [JsonProperty("model_id")]
        public string ModelId { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("runtime_model_ref")]
        public string RuntimeModelRef { get; set; }

        // Runtime features belong to the registered artifact, not its filename.
        [JsonProperty("supports_thinking")]
        public bool SupportsThinking { get; set; }

        [JsonProperty("thinking_token_budget")]
        public int ThinkingTokenBudget { get; set; } = 768;

        [JsonProperty("work_thinking_token_budget")]
        public int WorkThinkingTokenBudget { get; set; } = 768;

        [JsonProperty("talk_output_token_budget")]
        public int TalkOutputTokenBudget { get; set; } = 1280;

        [JsonProperty("work_output_token_budget")]
        public int WorkOutputTokenBudget { get; set; } = 2304;

        [JsonProperty("download_url")]
        public string DownloadUrl { get; set; }

        [JsonProperty("sha256")]
        public string Sha256 { get; set; }

        [JsonProperty("supports_speculative_decoding")]
        public bool SupportsSpeculativeDecoding { get; set; }

        [JsonProperty("apple_asset_pack_id")]
        public string AppleAssetPackId { get; set; }

        [JsonProperty("local_server_base_url")]
        public string LocalServerBaseUrl { get; set; }

        [JsonProperty("deployment_kind")]
        public YuiLocalAiDeploymentKind DeploymentKind { get; set; } = YuiLocalAiDeploymentKind.OnDeviceEmbedded;

        [JsonProperty("capabilities")]
        public YuiLocalAiCapability[] Capabilities { get; set; } = Array.Empty<YuiLocalAiCapability>();

        [JsonProperty("enabled_by_default")]
        public bool EnabledByDefault { get; set; }

        [JsonProperty("download_required")]
        public bool DownloadRequired { get; set; } = true;

        [JsonProperty("memory_budget_mb")]
        public int MemoryBudgetMb { get; set; }

        [JsonProperty("disk_budget_mb")]
        public int DiskBudgetMb { get; set; }

        [JsonProperty("priority")]
        public int Priority { get; set; }

        [JsonProperty("startup_policy")]
        public YuiLocalAiStartupPolicy StartupPolicy { get; set; } = YuiLocalAiStartupPolicy.OnDemand;

        [JsonProperty("platforms")]
        public string[] Platforms { get; set; } = Array.Empty<string>();

        [JsonProperty("notes")]
        public string Notes { get; set; }
    }

    [Serializable]
    public sealed class YuiLocalAiModelPackManifest
    {
        [JsonProperty("schema_version")]
        public string SchemaVersion { get; set; } = "2026-06-28";

        [JsonProperty("packs")]
        public List<YuiLocalAiModelPack> Packs { get; set; } = new List<YuiLocalAiModelPack>();
    }

    public sealed class YuiLocalAiStatus
    {
        public bool Available { get; set; }
        public string RuntimeName { get; set; }
        public string Detail { get; set; }
        public IReadOnlyCollection<YuiLocalAiCapability> Capabilities { get; set; } = Array.Empty<YuiLocalAiCapability>();
    }

    public abstract class YuiLocalAiResponse
    {
        public bool Success { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public string ModelId { get; set; }
        public long LatencyMs { get; set; }
    }

    public sealed class YuiLocalAiChatRequest
    {
        public string LanguageCode { get; set; } = "ja";
        public string Mode { get; set; } = "talk";
        public string RequestId { get; set; }
        public string UserId { get; set; } = "local_user";
        public string Message { get; set; }
        public string CharacterName { get; set; }
        public string CustomInstruction { get; set; }
        public string ResponseInstruction { get; set; }
        public string ScreenContext { get; set; }
        public string SystemInstruction { get; set; }
        public string Prompt { get; set; }
        // Structured LiteRT conversation input; Prompt remains for older adapters.
        public string Input { get; set; }
        public List<YuiLocalAiChatMessage> History { get; set; } = new List<YuiLocalAiChatMessage>();
        public Dictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();
    }

    public sealed class YuiLocalAiChatMessage
    {
        [JsonProperty("role")] public string Role { get; set; }
        [JsonProperty("content")] public string Content { get; set; }
    }

    public sealed class YuiLocalAiChatResponse : YuiLocalAiResponse
    {
        public string Text { get; set; }
        public string Face { get; set; } = "neutral";
        public string Animation { get; set; } = "idle";
        public string VoiceStyle { get; set; } = "normal";
        public bool ShouldTts { get; set; } = true;
    }

    public sealed class YuiLocalAiAudioRequest
    {
        public byte[] AudioBytes { get; set; }
        public string MimeType { get; set; } = "audio/wav";
        public int SampleRate { get; set; } = 24000;
        public string LanguageCode { get; set; } = "ja";
    }

    public sealed class YuiLocalAiTranscriptionResponse : YuiLocalAiResponse
    {
        public string Text { get; set; }
        public float? Confidence { get; set; }
    }

    public sealed class YuiLocalAiSpeechRequest
    {
        public string Text { get; set; }
        public string LanguageCode { get; set; } = "ja";
        public string VoiceStyle { get; set; } = "normal";
        public float SpeedScale { get; set; } = 1.0f;
        public float PitchScale { get; set; } = 0.0f;
    }

    public sealed class YuiLocalAiSpeechResponse : YuiLocalAiResponse
    {
        public byte[] AudioBytes { get; set; }
        public string MimeType { get; set; } = "audio/wav";
        public int SampleRate { get; set; } = 24000;
        public int? DurationMs { get; set; }
    }

    public sealed class YuiLocalAiVisionRequest
    {
        public byte[] ImageBytes { get; set; }
        public string MimeType { get; set; } = "image/jpeg";
        public string PromptType { get; set; } = "screen";
        public string Prompt { get; set; }
    }

    public sealed class YuiLocalAiVisionResponse : YuiLocalAiResponse
    {
        public string VisionResultId { get; set; }
        public string Summary { get; set; }
        public Dictionary<string, object> Structured { get; set; } = new Dictionary<string, object>();
    }
}
