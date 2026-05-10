using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace YuiPhysicalAI.Api
{
    [Serializable]
    public sealed class HealthResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("api_schema_version")]
        public string ApiSchemaVersion { get; set; }

        [JsonProperty("min_client_schema_version")]
        public string MinClientSchemaVersion { get; set; }

        [JsonProperty("database")]
        public string Database { get; set; }

        [JsonProperty("providers")]
        public Dictionary<string, object> Providers { get; set; }

        [JsonProperty("features")]
        public Dictionary<string, bool> Features { get; set; }
    }

    [Serializable]
    public sealed class ConfigResponse
    {
        [JsonProperty("character_name")]
        public string CharacterName { get; set; }

        [JsonProperty("chat_provider")]
        public string ChatProvider { get; set; }

        [JsonProperty("vision_provider")]
        public string VisionProvider { get; set; }

        [JsonProperty("tts_provider")]
        public string TtsProvider { get; set; }

        [JsonProperty("stt_provider")]
        public string SttProvider { get; set; }

        [JsonProperty("default_user_id")]
        public string DefaultUserId { get; set; }

        [JsonProperty("limits")]
        public Dictionary<string, int> Limits { get; set; }
    }

    [Serializable]
    public sealed class RealtimeStatusResponse
    {
        [JsonProperty("configured")]
        public bool Configured { get; set; }

        [JsonProperty("default_mode")]
        public string DefaultMode { get; set; }

        [JsonProperty("voice_model")]
        public string VoiceModel { get; set; }

        [JsonProperty("translation_model")]
        public string TranslationModel { get; set; }

        [JsonProperty("transcription_model")]
        public string TranscriptionModel { get; set; }

        [JsonProperty("modes")]
        public List<string> Modes { get; set; }

        [JsonProperty("warning")]
        public string Warning { get; set; }
    }

    [Serializable]
    public sealed class RealtimeProbeRequest
    {
        [JsonProperty("mode")]
        public string Mode { get; set; } = "voice";

        [JsonProperty("connect")]
        public bool Connect { get; set; }
    }

    [Serializable]
    public sealed class RealtimeProbeResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("mode")]
        public string Mode { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("endpoint")]
        public string Endpoint { get; set; }

        [JsonProperty("connected")]
        public bool Connected { get; set; }

        [JsonProperty("first_event_type")]
        public string FirstEventType { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    [Serializable]
    public sealed class RealtimeAudioResponse
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("audio_base64")]
        public string AudioBase64 { get; set; }

        [JsonProperty("audio_format")]
        public string AudioFormat { get; set; }

        [JsonProperty("sample_rate")]
        public int SampleRate { get; set; } = 24000;

        [JsonProperty("events")]
        public List<string> Events { get; set; }
    }

    [Serializable]
    public sealed class RequestContext
    {
        [JsonProperty("vision_result_id")]
        public string VisionResultId { get; set; }

        [JsonProperty("screen_context")]
        public string ScreenContext { get; set; }

        [JsonProperty("extra")]
        public Dictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();
    }

    [Serializable]
    public sealed class ChatRequest
    {
        [JsonProperty("request_id")]
        public string RequestId { get; set; }

        [JsonProperty("user_id")]
        public string UserId { get; set; } = "local_user";

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("context")]
        public RequestContext Context { get; set; } = new RequestContext();

        [JsonProperty("mode")]
        public string Mode { get; set; } = "standard";

        [JsonProperty("secret")]
        public bool Secret { get; set; }

        [JsonProperty("custom_instruction")]
        public string CustomInstruction { get; set; } = "";

        [JsonProperty("character_name")]
        public string CharacterName { get; set; } = "";
    }

    [Serializable]
    public sealed class ChatResponse
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("face")]
        public string Face { get; set; }

        [JsonProperty("animation")]
        public string Animation { get; set; }

        [JsonProperty("voice_style")]
        public string VoiceStyle { get; set; }

        [JsonProperty("should_use_vision")]
        public bool ShouldUseVision { get; set; }

        [JsonProperty("memory_action")]
        public string MemoryAction { get; set; }

        [JsonProperty("should_tts")]
        public bool ShouldTts { get; set; }
    }

    [Serializable]
    public sealed class TtsRequest
    {
        [JsonProperty("request_id")]
        public string RequestId { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("speaker_id")]
        public int SpeakerId { get; set; } = 1;

        [JsonProperty("speed_scale")]
        public float? SpeedScale { get; set; }

        [JsonProperty("pitch_scale")]
        public float? PitchScale { get; set; }

        [JsonProperty("intonation_scale")]
        public float? IntonationScale { get; set; }

        [JsonProperty("volume_scale")]
        public float? VolumeScale { get; set; }

        [JsonProperty("pre_phoneme_length")]
        public float? PrePhonemeLength { get; set; }

        [JsonProperty("post_phoneme_length")]
        public float? PostPhonemeLength { get; set; }
    }

    [Serializable]
    public sealed class TtsResponse
    {
        [JsonProperty("audio_url")]
        public string AudioUrl { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("duration_ms")]
        public int? DurationMs { get; set; }
    }

    [Serializable]
    public sealed class SttResponse
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("confidence")]
        public float? Confidence { get; set; }
    }

    [Serializable]
    public sealed class VisionObject
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("brand_or_type")]
        public string BrandOrType { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("shape")]
        public string Shape { get; set; }

        [JsonProperty("position")]
        public string Position { get; set; }

        [JsonProperty("visible_details")]
        public List<string> VisibleDetails { get; set; } = new List<string>();

        [JsonProperty("estimated_total_volume_ml")]
        public int? EstimatedTotalVolumeMl { get; set; }

        [JsonProperty("estimated_remaining_ratio")]
        public float? EstimatedRemainingRatio { get; set; }

        [JsonProperty("estimated_consumed_ml")]
        public int? EstimatedConsumedMl { get; set; }

        [JsonProperty("confidence")]
        public string Confidence { get; set; }
    }

    [Serializable]
    public sealed class VisionStructured
    {
        [JsonProperty("objects")]
        public List<VisionObject> Objects { get; set; } = new List<VisionObject>();

        [JsonProperty("extra")]
        public Dictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();
    }

    [Serializable]
    public sealed class VisionResponse
    {
        [JsonProperty("vision_result_id")]
        public string VisionResultId { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("structured")]
        public VisionStructured Structured { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    [Serializable]
    public sealed class MemorySaveRequest
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; } = "local_user";

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("importance")]
        public int Importance { get; set; } = 3;

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class MemorySearchRequest
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; } = "local_user";

        [JsonProperty("query")]
        public string Query { get; set; }

        [JsonProperty("limit")]
        public int Limit { get; set; } = 5;
    }

    [Serializable]
    public sealed class MemoryItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("importance")]
        public int Importance { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }
    }

    [Serializable]
    public sealed class MemorySearchResponse
    {
        [JsonProperty("items")]
        public List<MemoryItem> Items { get; set; }
    }

    [Serializable]
    public sealed class UsageLimits
    {
        [JsonProperty("chat_count")]
        public int ChatCount { get; set; }

        [JsonProperty("vision_count")]
        public int VisionCount { get; set; }

        [JsonProperty("stt_minutes")]
        public int SttMinutes { get; set; }

        [JsonProperty("tts_count")]
        public int TtsCount { get; set; }
    }

    [Serializable]
    public sealed class UsageResponse
    {
        [JsonProperty("date")]
        public string Date { get; set; }

        [JsonProperty("chat_count")]
        public int ChatCount { get; set; }

        [JsonProperty("vision_count")]
        public int VisionCount { get; set; }

        [JsonProperty("stt_minutes")]
        public float SttMinutes { get; set; }

        [JsonProperty("tts_count")]
        public int TtsCount { get; set; }

        [JsonProperty("limits")]
        public UsageLimits Limits { get; set; }
    }

    [Serializable]
    public sealed class ConversationItem
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    [Serializable]
    public sealed class RecentConversationsResponse
    {
        [JsonProperty("items")]
        public List<ConversationItem> Items { get; set; }
    }

    [Serializable]
    public sealed class ClearConversationsResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("conversations")]
        public int Conversations { get; set; }

        [JsonProperty("chat_responses")]
        public int ChatResponses { get; set; }

        [JsonProperty("memories")]
        public int Memories { get; set; }
    }

    public sealed class ChatSpeechResult
    {
        public ChatResponse Chat { get; set; }
        public TtsResponse Tts { get; set; }
        public UnityEngine.AudioClip AudioClip { get; set; }
    }
}
