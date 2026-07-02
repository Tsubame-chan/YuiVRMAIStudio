namespace YuiPhysicalAI.UI
{
    public static class YuiTtsModeOptions
    {
        public const string LocalAiVoiceLabel = "Local Voice/STT (Offline)";
        public const string OfflineAivisLabel = "AivisSpeech HD (Offline)";
        public const string BackendVoicevoxLabel = "VOICEVOX";
        public const string OfflineVoicevoxLabel = "VOICEVOX";
        public const string AivisLabel = "AivisSpeech HD";
        public const string IrodoriLabel = "Irodori TTS";
        public const string IrodoriUnavailableLabel = "Irodori TTS (Backend setup required)";
        public const string SilentLabel = "Silent";

        public static string[] Labels(bool includeHttpTts, bool httpTtsAvailable)
        {
            return Labels(includeLocalAi: false, includeHttpTts, httpTtsAvailable);
        }

        public static string[] Labels(bool includeLocalAi, bool includeHttpTts, bool httpTtsAvailable)
        {
            return Labels(includeLocalAi, includeNativeAivis: false, includeHttpTts, httpTtsAvailable);
        }

        public static string[] Labels(bool includeLocalAi, bool includeNativeAivis, bool includeHttpTts, bool httpTtsAvailable)
        {
            return Labels(includeLocalAi, includeNativeAivis, includeNativeVoicevox: false, includeHttpTts, httpTtsAvailable);
        }

        public static string[] Labels(
            bool includeLocalAi,
            bool includeNativeAivis,
            bool includeNativeVoicevox,
            bool includeHttpTts,
            bool httpTtsAvailable)
        {
            var count = (includeLocalAi ? 1 : 0)
                + 3
                + (includeHttpTts ? 1 : 0);
            var labels = new string[count];
            var index = 0;
            if (includeLocalAi)
            {
                labels[index++] = LocalAiVoiceLabel;
            }

            labels[index++] = BackendVoicevoxLabel;

            labels[index++] = AivisLabel;
            if (includeHttpTts)
            {
                labels[index++] = httpTtsAvailable ? IrodoriLabel : IrodoriUnavailableLabel;
            }

            labels[index] = SilentLabel;
            return labels;
        }

        public static string ModeFromIndex(int index, bool includeHttpTts)
        {
            return ModeFromIndex(index, includeLocalAi: false, includeHttpTts);
        }

        public static string ModeFromIndex(int index, bool includeLocalAi, bool includeHttpTts)
        {
            return ModeFromIndex(index, includeLocalAi, includeNativeAivis: false, includeHttpTts);
        }

        public static string ModeFromIndex(int index, bool includeLocalAi, bool includeNativeAivis, bool includeHttpTts)
        {
            return ModeFromIndex(index, includeLocalAi, includeNativeAivis, includeNativeVoicevox: false, includeHttpTts);
        }

        public static string ModeFromIndex(
            int index,
            bool includeLocalAi,
            bool includeNativeAivis,
            bool includeNativeVoicevox,
            bool includeHttpTts)
        {
            if (includeLocalAi && index == 0)
            {
                return "local-ai";
            }

            var backendVoicevoxIndex = includeLocalAi ? 1 : 0;
            if (index == backendVoicevoxIndex)
            {
                return "server";
            }

            var nativeAivisIndex = backendVoicevoxIndex + 1;
            var backendAivisIndex = nativeAivisIndex;
            if (index == backendAivisIndex)
            {
                return "aivis";
            }

            var irodoriIndex = backendAivisIndex + 1;
            if (includeHttpTts && index == irodoriIndex)
            {
                return "server-http";
            }

            var silentIndex = irodoriIndex + (includeHttpTts ? 1 : 0);
            if (index == silentIndex)
            {
                return "silent";
            }

            return "server";
        }

        public static int IndexFromMode(string mode, bool includeHttpTts)
        {
            return IndexFromMode(mode, includeLocalAi: false, includeHttpTts);
        }

        public static int IndexFromMode(string mode, bool includeLocalAi, bool includeHttpTts)
        {
            return IndexFromMode(mode, includeLocalAi, includeNativeAivis: false, includeHttpTts);
        }

        public static int IndexFromMode(string mode, bool includeLocalAi, bool includeNativeAivis, bool includeHttpTts)
        {
            return IndexFromMode(mode, includeLocalAi, includeNativeAivis, includeNativeVoicevox: false, includeHttpTts);
        }

        public static int IndexFromMode(
            string mode,
            bool includeLocalAi,
            bool includeNativeAivis,
            bool includeNativeVoicevox,
            bool includeHttpTts)
        {
            if (string.Equals(mode, "local-ai", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "liquid-audio", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "on-device-audio", System.StringComparison.OrdinalIgnoreCase))
            {
                return includeLocalAi ? 0 : 0;
            }

            var backendVoicevoxIndex = includeLocalAi ? 1 : 0;
            if (string.Equals(mode, "server", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "local", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "voicevox-native", System.StringComparison.OrdinalIgnoreCase))
            {
                return backendVoicevoxIndex;
            }

            var nativeAivisIndex = backendVoicevoxIndex + 1;
            if (string.Equals(mode, "aivis-native", System.StringComparison.OrdinalIgnoreCase))
            {
                return nativeAivisIndex;
            }

            var aivisIndex = nativeAivisIndex;
            if (string.Equals(mode, "aivis", System.StringComparison.OrdinalIgnoreCase))
            {
                return aivisIndex;
            }

            var irodoriIndex = aivisIndex + 1;
            if (string.Equals(mode, "server-http", System.StringComparison.OrdinalIgnoreCase))
            {
                return includeHttpTts ? irodoriIndex : aivisIndex;
            }

            if (string.Equals(mode, "silent", System.StringComparison.OrdinalIgnoreCase))
            {
                return irodoriIndex + (includeHttpTts ? 1 : 0);
            }

            return backendVoicevoxIndex;
        }
    }
}
