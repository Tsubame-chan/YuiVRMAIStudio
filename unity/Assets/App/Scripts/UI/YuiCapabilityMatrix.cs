using System;
using System.Collections.Generic;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public enum YuiCapabilityState
    {
        Ready,
        Degraded,
        NeedsBackend,
        SetupRequired,
        Unavailable
    }

    public enum YuiCapabilityRoute
    {
        None,
        Backend,
        Native,
        DirectApi,
        Local
    }

    public sealed class YuiCapabilityItem
    {
        public YuiCapabilityItem(
            string id,
            string label,
            YuiCapabilityState state,
            YuiCapabilityRoute route,
            string detail,
            bool visible = true)
        {
            Id = id;
            Label = label;
            State = state;
            Route = route;
            Detail = detail ?? string.Empty;
            Visible = visible;
        }

        public string Id { get; }
        public string Label { get; }
        public YuiCapabilityState State { get; }
        public YuiCapabilityRoute Route { get; }
        public string Detail { get; }
        public bool Visible { get; }

        public bool Ready => State == YuiCapabilityState.Ready || State == YuiCapabilityState.Degraded;
        public bool RequiresBackend => State == YuiCapabilityState.NeedsBackend || Route == YuiCapabilityRoute.Backend;
    }

    public sealed class YuiCapabilitySnapshot
    {
        private readonly Dictionary<string, YuiCapabilityItem> conversations;
        private readonly Dictionary<string, YuiCapabilityItem> tts;

        public YuiCapabilitySnapshot(
            bool backendReachable,
            YuiCapabilityItem backend,
            YuiCapabilityItem database,
            YuiCapabilityItem openAi,
            Dictionary<string, YuiCapabilityItem> conversations,
            Dictionary<string, YuiCapabilityItem> tts)
        {
            BackendReachable = backendReachable;
            Backend = backend;
            Database = database;
            OpenAi = openAi;
            this.conversations = conversations ?? new Dictionary<string, YuiCapabilityItem>(StringComparer.OrdinalIgnoreCase);
            this.tts = tts ?? new Dictionary<string, YuiCapabilityItem>(StringComparer.OrdinalIgnoreCase);
        }

        public bool BackendReachable { get; }
        public YuiCapabilityItem Backend { get; }
        public YuiCapabilityItem Database { get; }
        public YuiCapabilityItem OpenAi { get; }

        public YuiCapabilityItem Conversation(string mode)
        {
            var normalized = YuiConversationModes.Normalize(mode);
            return conversations.TryGetValue(normalized, out var item)
                ? item
                : conversations[YuiConversationModes.Stable];
        }

        public YuiCapabilityItem Tts(string mode)
        {
            var normalized = NormalizeTtsMode(mode);
            return tts.TryGetValue(normalized, out var item)
                ? item
                : tts["server"];
        }

        public static string NormalizeTtsMode(string mode)
        {
            if (string.Equals(mode, "local", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "voicevox-native", StringComparison.OrdinalIgnoreCase))
            {
                return "server";
            }

            if (string.Equals(mode, "aivis-native", StringComparison.OrdinalIgnoreCase))
            {
                return "aivis";
            }

            if (string.Equals(mode, "server-http", StringComparison.OrdinalIgnoreCase))
            {
                return "server-http";
            }

            if (string.Equals(mode, "silent", StringComparison.OrdinalIgnoreCase))
            {
                return "silent";
            }

            if (string.Equals(mode, "local-ai", StringComparison.OrdinalIgnoreCase))
            {
                return "local-ai";
            }

            if (string.Equals(mode, "aivis", StringComparison.OrdinalIgnoreCase))
            {
                return "aivis";
            }

            return "server";
        }
    }

    public static class YuiCapabilityMatrix
    {
        public static YuiCapabilitySnapshot FromProviderStatus(
            ProviderStatusResponse providerStatus,
            bool backendReachable,
            bool nativeVoicevoxAvailable,
            bool localChatAvailable,
            bool directOpenAiConfigured,
            bool backendIsRemote = false)
        {
            var backendStatus = providerStatus?.Backend?.Status;
            var databaseStatus = providerStatus?.Database?.Status;
            var backendOk = backendReachable || IsReadyStatus(backendStatus);
            var openAiStatus = ProviderStatus(providerStatus, "openai");
            var voicevoxStatus = ProviderStatus(providerStatus, "voicevox");
            var aivisStatus = ProviderStatus(providerStatus, "aivis");
            var httpTtsStatus = ProviderStatus(providerStatus, "http_tts");

            return Build(
                backendOk,
                StateForSystem(backendOk ? "ok" : backendStatus, backendOk ? "Backend is reachable." : "Backend is offline."),
                StateForSystem(databaseStatus, "Backend database status."),
                StateForOpenAi(openAiStatus, directOpenAiConfigured),
                voicevoxStatus,
                aivisStatus,
                httpTtsStatus,
                nativeVoicevoxAvailable,
                localChatAvailable,
                directOpenAiConfigured,
                backendIsRemote);
        }

        public static YuiCapabilitySnapshot FromHealth(
            HealthResponse health,
            bool backendReachable,
            bool nativeVoicevoxAvailable,
            bool localChatAvailable,
            bool directOpenAiConfigured,
            bool backendIsRemote = false)
        {
            var backendOk = backendReachable || IsReadyStatus(health?.Status);
            var openAiConfigured = directOpenAiConfigured || HealthBool(health, "openai_configured");
            var voicevoxStatus = HealthFeature(health, "local_voicevox_tts") ? "ok" : "offline";
            var httpTtsStatus = HealthFeature(health, "external_http_tts") ? "configured" : "not_configured";

            return Build(
                backendOk,
                StateForSystem(backendOk ? "ok" : health?.Status, backendOk ? "Backend is reachable." : "Backend is offline."),
                StateForSystem(health?.Database, "Backend database status."),
                openAiConfigured
                    ? new YuiCapabilityItem("openai", "OpenAI", YuiCapabilityState.Ready, YuiCapabilityRoute.DirectApi, "OpenAI key is configured.")
                    : new YuiCapabilityItem("openai", "OpenAI", YuiCapabilityState.SetupRequired, YuiCapabilityRoute.DirectApi, "OpenAI API key is not configured."),
                voicevoxStatus,
                "unknown",
                httpTtsStatus,
                nativeVoicevoxAvailable,
                localChatAvailable,
                openAiConfigured,
                backendIsRemote);
        }

        private static YuiCapabilitySnapshot Build(
            bool backendReachable,
            YuiCapabilityItem backend,
            YuiCapabilityItem database,
            YuiCapabilityItem openAi,
            string backendVoicevoxStatus,
            string aivisStatus,
            string httpTtsStatus,
            bool nativeVoicevoxAvailable,
            bool localChatAvailable,
            bool directOpenAiConfigured,
            bool backendIsRemote)
        {
            var conversations = new Dictionary<string, YuiCapabilityItem>(StringComparer.OrdinalIgnoreCase)
            {
                [YuiConversationModes.Stable] = backendReachable
                    ? new YuiCapabilityItem(YuiConversationModes.Stable, "Auto Select", YuiCapabilityState.Ready, YuiCapabilityRoute.Backend, "Backend is preferred; local fallback remains available.")
                    : localChatAvailable
                        ? new YuiCapabilityItem(YuiConversationModes.Stable, "Auto Select", YuiCapabilityState.Ready, YuiCapabilityRoute.Local, "Backend is offline; local AI fallback is ready.")
                        : new YuiCapabilityItem(YuiConversationModes.Stable, "Auto Select", YuiCapabilityState.NeedsBackend, YuiCapabilityRoute.Backend, "Backend is required until local AI is available."),
                [YuiConversationModes.BackendAi] = backendReachable
                    ? new YuiCapabilityItem(YuiConversationModes.BackendAi, "Backend Talk", YuiCapabilityState.Ready, YuiCapabilityRoute.Backend, "Backend standard talk is reachable.")
                    : new YuiCapabilityItem(YuiConversationModes.BackendAi, "Backend Talk", YuiCapabilityState.NeedsBackend, YuiCapabilityRoute.Backend, "Start or reconnect the backend to use this mode."),
                [YuiConversationModes.RealtimeVoice] = RealtimeItem(YuiConversationModes.RealtimeVoice, "Realtime Talk (OpenAI Voice)", backendReachable),
                [YuiConversationModes.RealtimeVoicevox] = RealtimeItem(YuiConversationModes.RealtimeVoicevox, "Realtime Talk (VOICEVOX)", backendReachable),
                [YuiConversationModes.RealtimeAivis] = RealtimeItem(YuiConversationModes.RealtimeAivis, "Realtime Talk (AivisSpeech HD)", backendReachable),
                [YuiConversationModes.RealtimeTranslate] = RealtimeItem(YuiConversationModes.RealtimeTranslate, "Realtime Translation", backendReachable),
                [YuiConversationModes.DirectOpenAi] = directOpenAiConfigured
                    ? new YuiCapabilityItem(YuiConversationModes.DirectOpenAi, "Direct OpenAI", YuiCapabilityState.Ready, YuiCapabilityRoute.DirectApi, "OpenAI API key is configured.")
                    : new YuiCapabilityItem(YuiConversationModes.DirectOpenAi, "Direct OpenAI", YuiCapabilityState.SetupRequired, YuiCapabilityRoute.DirectApi, "OpenAI API key is required."),
                [YuiConversationModes.LocalAi] = localChatAvailable
                    ? new YuiCapabilityItem(YuiConversationModes.LocalAi, "Local Gemma", YuiCapabilityState.Ready, YuiCapabilityRoute.Local, "Local chat runtime is available.")
                    : new YuiCapabilityItem(YuiConversationModes.LocalAi, "Local Gemma", YuiCapabilityState.Unavailable, YuiCapabilityRoute.Local, "Local chat runtime is not available in this build.")
            };

            var voicevoxRoute = YuiTtsRuntimeRouting.ResolveVoicevoxRoute(
                IsReadyStatus(backendVoicevoxStatus) && backendReachable,
                nativeVoicevoxAvailable,
                backendIsRemote);
            var voicevoxItem = voicevoxRoute == YuiTtsExecutionRoute.Backend
                ? new YuiCapabilityItem("server", "VOICEVOX", YuiCapabilityState.Ready, YuiCapabilityRoute.Backend, "Backend VOICEVOX is ready.")
                : voicevoxRoute == YuiTtsExecutionRoute.NativeVoicevox
                    ? new YuiCapabilityItem("server", "VOICEVOX", YuiCapabilityState.Ready, YuiCapabilityRoute.Native, "Local VOICEVOX Core is ready.")
                    : new YuiCapabilityItem("server", "VOICEVOX", backendReachable ? YuiCapabilityState.SetupRequired : YuiCapabilityState.NeedsBackend, YuiCapabilityRoute.Backend, "VOICEVOX needs backend setup or local VOICEVOX Core.");

            var tts = new Dictionary<string, YuiCapabilityItem>(StringComparer.OrdinalIgnoreCase)
            {
                ["server"] = voicevoxItem,
                ["aivis"] = BackendProviderItem("aivis", "AivisSpeech HD", aivisStatus, backendReachable, advertiseWhenMissing: true),
                ["server-http"] = BackendProviderItem("server-http", "Irodori TTS", httpTtsStatus, backendReachable, advertiseWhenMissing: true),
                ["local-ai"] = new YuiCapabilityItem("local-ai", "Local Voice/STT", YuiCapabilityState.Unavailable, YuiCapabilityRoute.Local, "Local generic TTS is hidden on desktop unless a platform runtime provides it."),
                ["silent"] = new YuiCapabilityItem("silent", "Silent", YuiCapabilityState.Ready, YuiCapabilityRoute.None, "Voice playback is disabled.")
            };

            return new YuiCapabilitySnapshot(backendReachable, backend, database, openAi, conversations, tts);
        }

        private static YuiCapabilityItem RealtimeItem(string id, string label, bool backendReachable)
        {
            return backendReachable
                ? new YuiCapabilityItem(id, label, YuiCapabilityState.Ready, YuiCapabilityRoute.Backend, "Backend realtime endpoint is reachable.")
                : new YuiCapabilityItem(id, label, YuiCapabilityState.NeedsBackend, YuiCapabilityRoute.Backend, "Realtime modes require the backend.");
        }

        private static YuiCapabilityItem BackendProviderItem(
            string id,
            string label,
            string status,
            bool backendReachable,
            bool advertiseWhenMissing)
        {
            if (backendReachable && IsReadyStatus(status))
            {
                return new YuiCapabilityItem(id, label, YuiCapabilityState.Ready, YuiCapabilityRoute.Backend, $"{label} backend provider is ready.");
            }

            if (!backendReachable)
            {
                return new YuiCapabilityItem(id, label, YuiCapabilityState.NeedsBackend, YuiCapabilityRoute.Backend, $"{label} requires the backend.", advertiseWhenMissing);
            }

            return new YuiCapabilityItem(id, label, YuiCapabilityState.SetupRequired, YuiCapabilityRoute.Backend, $"{label} is visible but needs backend configuration.", advertiseWhenMissing);
        }

        private static YuiCapabilityItem StateForSystem(string status, string detail)
        {
            var state = IsReadyStatus(status)
                ? YuiCapabilityState.Ready
                : string.Equals(status, "degraded", StringComparison.OrdinalIgnoreCase)
                    ? YuiCapabilityState.Degraded
                    : YuiCapabilityState.Unavailable;
            return new YuiCapabilityItem("system", "System", state, YuiCapabilityRoute.Backend, detail);
        }

        private static YuiCapabilityItem StateForOpenAi(string status, bool directOpenAiConfigured)
        {
            if (directOpenAiConfigured || IsReadyStatus(status))
            {
                return new YuiCapabilityItem("openai", "OpenAI", YuiCapabilityState.Ready, YuiCapabilityRoute.DirectApi, "OpenAI is configured.");
            }

            return new YuiCapabilityItem("openai", "OpenAI", YuiCapabilityState.SetupRequired, YuiCapabilityRoute.DirectApi, "OpenAI API key is not configured.");
        }

        private static string ProviderStatus(ProviderStatusResponse status, string key)
        {
            if (status?.Providers == null || !status.Providers.TryGetValue(key, out var item) || item == null)
            {
                return "unknown";
            }

            return item.Status ?? "unknown";
        }

        private static bool HealthBool(HealthResponse health, string key)
        {
            return health?.Providers != null
                && health.Providers.TryGetValue(key, out var value)
                && value is bool configured
                && configured;
        }

        private static bool HealthFeature(HealthResponse health, string key)
        {
            return health?.Features != null
                && health.Features.TryGetValue(key, out var enabled)
                && enabled;
        }

        private static bool IsReadyStatus(string status)
        {
            return string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "configured", StringComparison.OrdinalIgnoreCase);
        }
    }
}
