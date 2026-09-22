using System;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.LocalAI
{
    public enum YuiAiEndpoint { Backend, DirectOpenAi, Local }

    public static class YuiAiEndpointPolicy
    {
        public static YuiAiEndpoint Resolve(string mode, HealthResponse backend, bool appKeyConfigured,
            YuiLocalAiCapability capability)
        {
            mode = YuiConversationModes.Normalize(mode);
            if (mode == YuiConversationModes.LocalAi) return YuiAiEndpoint.Local;
            if (mode == YuiConversationModes.DirectOpenAi) return YuiAiEndpoint.DirectOpenAi;
            if (mode != YuiConversationModes.Stable) return YuiAiEndpoint.Backend;
            if (BackendConfigured(backend, capability)) return YuiAiEndpoint.Backend;
            return appKeyConfigured ? YuiAiEndpoint.DirectOpenAi : YuiAiEndpoint.Local;
        }

        public static bool BackendConfigured(HealthResponse health, YuiLocalAiCapability capability)
        {
            if (health == null || health.Status != "ok" || health.Providers == null) return false;
            if (capability == YuiLocalAiCapability.Transcription) return Flag(health, "openai_configured");
            var providerField = capability == YuiLocalAiCapability.Vision ? "vision_provider" : "chat_provider";
            var provider = health.Providers.TryGetValue(providerField, out var value) ? Convert.ToString(value) : "openai";
            switch (provider)
            {
                case "openai": return Flag(health, "openai_configured");
                case "xai": return Flag(health, "xai_configured");
                case "gemini": return Flag(health, "gemini_configured");
                case "lmstudio": case "litert_lm": return true;
                default: return false;
            }
        }

        private static bool Flag(HealthResponse health, string name)
        {
            return health.Providers.TryGetValue(name, out var value) && value is bool flag && flag;
        }
    }
}
