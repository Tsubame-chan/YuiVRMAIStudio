namespace YuiPhysicalAI.UI
{
    public static class YuiTtsModeOptions
    {
        public const string DirectVoicevoxLabel = "Direct VOICEVOX (this device)";
        public const string BackendVoicevoxLabel = "Backend VOICEVOX";
        public const string IrodoriLabel = "Irodori TTS";
        public const string IrodoriUnavailableLabel = "Irodori TTS (unavailable)";
        public const string SilentLabel = "Silent";

        public static string[] Labels(bool includeHttpTts, bool httpTtsAvailable)
        {
            if (!includeHttpTts)
            {
                return new[]
                {
                    DirectVoicevoxLabel,
                    BackendVoicevoxLabel,
                    SilentLabel,
                };
            }

            return new[]
            {
                DirectVoicevoxLabel,
                BackendVoicevoxLabel,
                httpTtsAvailable ? IrodoriLabel : IrodoriUnavailableLabel,
                SilentLabel,
            };
        }

        public static string ModeFromIndex(int index, bool includeHttpTts)
        {
            switch (index)
            {
                case 1:
                    return "server";
                case 2:
                    return includeHttpTts ? "server-http" : "silent";
                case 3:
                    return "silent";
                default:
                    return "local";
            }
        }

        public static int IndexFromMode(string mode, bool includeHttpTts)
        {
            if (string.Equals(mode, "server", System.StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(mode, "server-http", System.StringComparison.OrdinalIgnoreCase))
            {
                return includeHttpTts ? 2 : 1;
            }

            if (string.Equals(mode, "silent", System.StringComparison.OrdinalIgnoreCase))
            {
                return includeHttpTts ? 3 : 2;
            }

            return 0;
        }
    }
}
