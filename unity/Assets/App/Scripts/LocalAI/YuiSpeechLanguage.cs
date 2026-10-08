using System;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiSpeechLanguage
    {
        public static bool IsEnglish(string language) => language != null
            && (language.Equals("en", StringComparison.OrdinalIgnoreCase)
                || language.StartsWith("en-", StringComparison.OrdinalIgnoreCase)
                || language.StartsWith("en_", StringComparison.OrdinalIgnoreCase));
        public static string Locale(string language) => IsEnglish(language) ? "en-US" : "ja-JP";

        public static bool UsesKokoro(string language, string mode) => IsEnglish(language)
            && (string.Equals(mode, "voicevox-native", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "server", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "local", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "local-ai", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "irodori-native", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "aivis-native", StringComparison.OrdinalIgnoreCase));
    }
}
