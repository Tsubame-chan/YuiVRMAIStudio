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
            bool deviceSpeech, string selected, bool english = false, bool englishReady = false, bool irodoriSupported = false, bool irodoriReady = false)
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
            if (english)
            {
                var label = "Kokoro · On this device" + (englishReady ? "" : " · Download needed");
                Add("voicevox-native", label, true);
                if (selected == "server" || selected == "aivis-native" || selected == "local-ai" || selected == "irodori-native") Add(selected, label, true);
            }
            else if (string.Equals(selected, "voicevox-native", StringComparison.OrdinalIgnoreCase))
            {
                Add("voicevox-native", "VOICEVOX · Recommended", nativeVoicevox);
                Add("server", "VOICEVOX · Backend", BackendHas("voicevox"));
            }
            else Add("server", "VOICEVOX · Recommended", nativeVoicevox || BackendHas("voicevox"));
            if (!english) Add("irodori-native", YuiSimpleDialog.L("Irodori · この端末", "Irodori · On this device") + (irodoriReady ? "" : YuiSimpleDialog.L(" · ダウンロードが必要", " · Download needed")), irodoriSupported);
            if (!english) Add("aivis-native", "AivisSpeech · Experimental", nativeAivis);
            Add("aivis", "AivisSpeech · Backend", BackendHas("aivis"));
            var httpLabel = knownBackend?.Providers != null && knownBackend.Providers.TryGetValue("http_tts", out var http)
                && (http.Engine ?? "").StartsWith("irodori", StringComparison.OrdinalIgnoreCase) ? "Irodori" : "External voice";
            Add("server-http", httpLabel + " · Backend", BackendHas("http_tts"));
            Add("backend-profile", "Saved voice · Backend", string.Equals(selected,"backend-profile",StringComparison.OrdinalIgnoreCase));
            if (!english) Add("local-ai", "Device voice", deviceSpeech);
            options.Add(new KeyValuePair<string, string>("silent", "Silent"));
            return options;
        }
    }
}
