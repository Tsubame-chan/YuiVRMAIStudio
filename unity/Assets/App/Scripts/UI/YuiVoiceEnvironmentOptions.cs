using System;
using System.Collections.Generic;
using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.UI
{
    // Voice capabilities belong to the execution environment, never to the LLM choice.
    public static class YuiVoiceEnvironmentOptions
    {
        public static List<KeyValuePair<string, string>> Build(ProviderStatusResponse knownBackend,
            bool backendReachable, bool localBackendInstalled, bool nativeVoicevox, bool nativeAivis,
            bool deviceSpeech, string selected)
        {
            var options = new List<KeyValuePair<string, string>>();
            bool BackendHas(string provider)
            {
                return (backendReachable || localBackendInstalled)
                    && knownBackend?.Providers != null && knownBackend.Providers.TryGetValue(provider, out var item)
                    && (item.Selectable || item.Status == "ok"
                        || (provider == "http_tts" && !string.IsNullOrWhiteSpace(item.BaseUrl)));
            }
            void Add(string mode, string label, bool available)
            {
                if (available || string.Equals(selected, mode, StringComparison.OrdinalIgnoreCase))
                    options.Add(new KeyValuePair<string, string>(mode, available ? label : label + " · Unavailable"));
            }
            Add("server", "VOICEVOX", nativeVoicevox || BackendHas("voicevox"));
            Add("aivis-native", "AivisSpeech · This device", nativeAivis);
            Add("aivis", "AivisSpeech · Backend", BackendHas("aivis"));
            var httpLabel = knownBackend?.Providers != null && knownBackend.Providers.TryGetValue("http_tts", out var http)
                && (http.Engine ?? "").StartsWith("irodori", StringComparison.OrdinalIgnoreCase) ? "Irodori" : "External voice";
            Add("server-http", httpLabel + " · Backend", BackendHas("http_tts"));
            Add("local-ai", "Device voice", deviceSpeech);
            options.Add(new KeyValuePair<string, string>("silent", "Silent"));
            // Preserve explicit native VOICEVOX from older versions without silently using a backend.
            if (selected == "voicevox-native") Add("voicevox-native", "VOICEVOX · This device", nativeVoicevox);
            return options;
        }
    }
}
