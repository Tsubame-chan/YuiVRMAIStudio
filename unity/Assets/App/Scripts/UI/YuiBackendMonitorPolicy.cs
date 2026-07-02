using System;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public static class YuiBackendMonitorPolicy
    {
        public static bool ShouldMonitorBackend(string conversationMode, string ttsMode)
        {
            return ShouldMonitorBackend(conversationMode, ttsMode, nativeVoicevoxAvailable: false);
        }

        public static bool ShouldMonitorBackend(string conversationMode, string ttsMode, bool nativeVoicevoxAvailable)
        {
            var backendIndependentConversation =
                string.Equals(
                    YuiConversationModes.Normalize(conversationMode),
                    YuiConversationModes.LocalAi,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    YuiConversationModes.Normalize(conversationMode),
                    YuiConversationModes.DirectOpenAi,
                    StringComparison.OrdinalIgnoreCase);
            var tts = YuiCapabilityMatrix.FromProviderStatus(
                providerStatus: null,
                backendReachable: false,
                nativeVoicevoxAvailable,
                localChatAvailable: false,
                directOpenAiConfigured: false).Tts(ttsMode);
            var backendIndependentTts = tts.Route != YuiCapabilityRoute.Backend
                || YuiTtsRuntimeRouting.UsesNativeSpeech(ttsMode)
                || string.Equals(ttsMode, "silent", StringComparison.OrdinalIgnoreCase);

            return !(backendIndependentConversation && backendIndependentTts);
        }
    }
}
