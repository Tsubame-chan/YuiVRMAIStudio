using System;

namespace YuiPhysicalAI.UI
{
    public enum YuiTtsExecutionRoute
    {
        None,
        Backend,
        NativeVoicevox
    }

    public static class YuiTtsRuntimeRouting
    {
        public static bool UsesNativeSpeech(string ttsMode)
        {
            return string.Equals(ttsMode, "aivis-native", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ttsMode, "voicevox-native", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ttsMode, "local-ai", StringComparison.OrdinalIgnoreCase);
        }

        public static string BackendProviderForMode(string ttsMode)
        {
            if (string.Equals(ttsMode, "aivis", StringComparison.OrdinalIgnoreCase))
            {
                return "aivis";
            }

            if (string.Equals(ttsMode, "server-http", StringComparison.OrdinalIgnoreCase))
            {
                return "http";
            }

            if (string.Equals(ttsMode, "server", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ttsMode, "local", StringComparison.OrdinalIgnoreCase))
            {
                return "voicevox";
            }

            return null;
        }

        public static bool IsVoicevoxIntent(string ttsMode)
        {
            return string.Equals(ttsMode, "server", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ttsMode, "local", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ttsMode, "voicevox-native", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldTryChatdollKitVoicevoxFallback(string ttsMode)
        {
            return string.Equals(ttsMode, "local", StringComparison.OrdinalIgnoreCase);
        }

        public static YuiTtsExecutionRoute ResolveVoicevoxRoute(
            bool backendVoicevoxAvailable,
            bool nativeVoicevoxAvailable,
            bool backendIsRemote)
        {
            if (backendVoicevoxAvailable && !backendIsRemote)
            {
                return YuiTtsExecutionRoute.Backend;
            }

            if (nativeVoicevoxAvailable)
            {
                return YuiTtsExecutionRoute.NativeVoicevox;
            }

            return backendVoicevoxAvailable
                ? YuiTtsExecutionRoute.Backend
                : YuiTtsExecutionRoute.None;
        }
    }
}
