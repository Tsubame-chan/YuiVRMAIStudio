namespace YuiPhysicalAI.Core
{
    public readonly struct YuiRealtimeVadSettings
    {
        public YuiRealtimeVadSettings(
            float speechRms,
            float silenceSeconds,
            int startChunks,
            int minTurnChunks,
            int prespeechChunks)
        {
            SpeechRms = speechRms;
            SilenceSeconds = silenceSeconds;
            StartChunks = startChunks;
            MinTurnChunks = minTurnChunks;
            PrespeechChunks = prespeechChunks;
        }

        public float SpeechRms { get; }
        public float SilenceSeconds { get; }
        public int StartChunks { get; }
        public int MinTurnChunks { get; }
        public int PrespeechChunks { get; }
    }

    public static class YuiRealtimeTuning
    {
        public const int SessionResetTurns = 0;
        public const float AudioMinPlayablePeak = 0.003f;
        public const float AudioTargetPeak = 0.62f;
        public const float AudioMaxAutoGain = 3f;

        private static readonly YuiRealtimeVadSettings VoiceVad = new YuiRealtimeVadSettings(
            speechRms: 0.008f,
            silenceSeconds: 0.9f,
            startChunks: 5,
            minTurnChunks: 5,
            prespeechChunks: 5);

        private static readonly YuiRealtimeVadSettings TranslateVad = new YuiRealtimeVadSettings(
            speechRms: 0.008f,
            silenceSeconds: 0.75f,
            startChunks: 2,
            minTurnChunks: 10,
            prespeechChunks: 10);

        public static YuiRealtimeVadSettings ClientVadFor(string mode)
        {
            return YuiConversationModes.IsRealtimeTranslate(mode)
                ? TranslateVad
                : VoiceVad;
        }

        public static YuiRealtimeVadSettings ClientVadFor(bool translateMode)
        {
            return translateMode ? TranslateVad : VoiceVad;
        }
    }
}
