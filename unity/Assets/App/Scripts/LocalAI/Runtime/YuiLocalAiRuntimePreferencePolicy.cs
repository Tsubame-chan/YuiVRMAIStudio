using System;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.LocalAI
{
    public readonly struct YuiLocalAiRuntimePreferences
    {
        public YuiLocalAiRuntimePreferences(
            bool preferLocalChat,
            bool preferLocalTranscription,
            bool preferLocalVision,
            bool fallbackToBackend,
            bool fallbackToBackendTranscription,
            bool fallbackToBackendVision)
        {
            PreferLocalChat = preferLocalChat;
            PreferLocalTranscription = preferLocalTranscription;
            PreferLocalVision = preferLocalVision;
            FallbackToBackend = fallbackToBackend;
            FallbackToBackendTranscription = fallbackToBackendTranscription;
            FallbackToBackendVision = fallbackToBackendVision;
        }

        public bool PreferLocalChat { get; }
        public bool PreferLocalTranscription { get; }
        public bool PreferLocalVision { get; }
        public bool FallbackToBackend { get; }
        public bool FallbackToBackendTranscription { get; }
        public bool FallbackToBackendVision { get; }
    }

    public static class YuiLocalAiRuntimePreferencePolicy
    {
        public static YuiLocalAiRuntimePreferences For(
            string conversationMode,
            string ttsMode,
            bool localVisionAvailable,
            bool localTranscriptionAvailable = false)
        {
            var localConversation = string.Equals(
                YuiConversationModes.Normalize(conversationMode),
                YuiConversationModes.LocalAi,
                StringComparison.OrdinalIgnoreCase);
            var localSpeechMode = string.Equals(ttsMode, "local-ai", StringComparison.OrdinalIgnoreCase);
            var requiresLocalTranscription = (localConversation || localSpeechMode) && localTranscriptionAvailable;

            return new YuiLocalAiRuntimePreferences(
                preferLocalChat: localConversation,
                preferLocalTranscription: requiresLocalTranscription,
                preferLocalVision: localConversation,
                fallbackToBackend: !localConversation && !localSpeechMode,
                fallbackToBackendTranscription: !requiresLocalTranscription,
                fallbackToBackendVision: !localConversation);
        }
    }
}
