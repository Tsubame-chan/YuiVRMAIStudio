using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public readonly struct YuiSavedTtsTuning
    {
        public YuiSavedTtsTuning(
            int speakerId,
            float speedScale,
            float pitchScale,
            float intonationScale,
            float synthesisVolumeScale,
            float prePhonemeLength,
            float postPhonemeLength)
        {
            SpeakerId = speakerId;
            SpeedScale = speedScale;
            PitchScale = pitchScale;
            IntonationScale = intonationScale;
            SynthesisVolumeScale = synthesisVolumeScale;
            PrePhonemeLength = prePhonemeLength;
            PostPhonemeLength = postPhonemeLength;
        }

        public int SpeakerId { get; }
        public float SpeedScale { get; }
        public float PitchScale { get; }
        public float IntonationScale { get; }
        public float SynthesisVolumeScale { get; }
        public float PrePhonemeLength { get; }
        public float PostPhonemeLength { get; }
    }

    public static class YuiTtsTuningPrefs
    {
        private const string Prefix = "Yui.Settings.TtsProfile.";

        public static string NormalizeMode(string ttsMode)
        {
            if (string.Equals(ttsMode, "aivis-native", System.StringComparison.OrdinalIgnoreCase))
            {
                return "aivis-native";
            }

            if (string.Equals(ttsMode, "aivis", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(ttsMode, "server-http", System.StringComparison.OrdinalIgnoreCase))
            {
                return "aivis";
            }

            if (string.Equals(ttsMode, "silent", System.StringComparison.OrdinalIgnoreCase))
            {
                return "silent";
            }

            return "server";
        }

        public static int DefaultSpeakerForMode(string ttsMode)
        {
            return NormalizeMode(ttsMode) == "aivis" ? 1431611904 : 14;
        }

        public static YuiSavedTtsTuning DefaultForMode(string ttsMode)
        {
            return new YuiSavedTtsTuning(
                DefaultSpeakerForMode(ttsMode),
                1.0f,
                0.0f,
                1.0f,
                1.0f,
                0.1f,
                0.1f);
        }

        public static YuiSavedTtsTuning LoadForMode(string ttsMode, YuiSavedTtsTuning fallback)
        {
            var mode = NormalizeMode(ttsMode);
            var defaultTuning = DefaultForMode(mode);
            var speakerId = PlayerPrefs.GetInt(Key(mode, "SpeakerId"), defaultTuning.SpeakerId);
            var speedScale = PlayerPrefs.GetFloat(Key(mode, "Speed"), fallback.SpeedScale > 0f ? fallback.SpeedScale : defaultTuning.SpeedScale);
            var pitchScale = PlayerPrefs.GetFloat(Key(mode, "Pitch"), fallback.PitchScale);
            var intonationScale = PlayerPrefs.GetFloat(Key(mode, "Intonation"), fallback.IntonationScale > 0f ? fallback.IntonationScale : defaultTuning.IntonationScale);
            var synthesisVolumeScale = PlayerPrefs.GetFloat(Key(mode, "SynthesisVolume"), fallback.SynthesisVolumeScale > 0f ? fallback.SynthesisVolumeScale : defaultTuning.SynthesisVolumeScale);
            var prePhonemeLength = PlayerPrefs.GetFloat(Key(mode, "PrePhonemeLength"), fallback.PrePhonemeLength > 0f ? fallback.PrePhonemeLength : defaultTuning.PrePhonemeLength);
            var postPhonemeLength = PlayerPrefs.GetFloat(Key(mode, "PostPhonemeLength"), fallback.PostPhonemeLength > 0f ? fallback.PostPhonemeLength : defaultTuning.PostPhonemeLength);
            return Sanitize(mode, new YuiSavedTtsTuning(
                speakerId,
                speedScale,
                pitchScale,
                intonationScale,
                synthesisVolumeScale,
                prePhonemeLength,
                postPhonemeLength));
        }

        public static void SaveForMode(string ttsMode, YuiSavedTtsTuning tuning)
        {
            var mode = NormalizeMode(ttsMode);
            tuning = Sanitize(mode, tuning);
            PlayerPrefs.SetInt(Key(mode, "SpeakerId"), tuning.SpeakerId);
            PlayerPrefs.SetFloat(Key(mode, "Speed"), tuning.SpeedScale);
            PlayerPrefs.SetFloat(Key(mode, "Pitch"), tuning.PitchScale);
            PlayerPrefs.SetFloat(Key(mode, "Intonation"), tuning.IntonationScale);
            PlayerPrefs.SetFloat(Key(mode, "SynthesisVolume"), tuning.SynthesisVolumeScale);
            PlayerPrefs.SetFloat(Key(mode, "PrePhonemeLength"), tuning.PrePhonemeLength);
            PlayerPrefs.SetFloat(Key(mode, "PostPhonemeLength"), tuning.PostPhonemeLength);
        }

        public static YuiSavedTtsTuning Sanitize(string ttsMode, YuiSavedTtsTuning tuning)
        {
            var mode = NormalizeMode(ttsMode);
            var defaultTuning = DefaultForMode(mode);
            return new YuiSavedTtsTuning(
                CompatibleSpeakerForMode(mode, tuning.SpeakerId),
                YuiTtsTuning.SafeSpeedForMode(mode, tuning.SpeedScale > 0f ? tuning.SpeedScale : defaultTuning.SpeedScale),
                YuiTtsTuning.SafePitchForMode(mode, tuning.PitchScale),
                Mathf.Clamp(tuning.IntonationScale > 0f ? tuning.IntonationScale : defaultTuning.IntonationScale, 0.5f, 1.5f),
                Mathf.Clamp(tuning.SynthesisVolumeScale > 0f ? tuning.SynthesisVolumeScale : defaultTuning.SynthesisVolumeScale, 0.5f, 1.5f),
                Mathf.Clamp(tuning.PrePhonemeLength, 0.0f, 0.5f),
                Mathf.Clamp(tuning.PostPhonemeLength, 0.0f, 0.5f));
        }

        private static string Key(string mode, string name)
        {
            return Prefix + NormalizeMode(mode) + "." + name;
        }

        private static int CompatibleSpeakerForMode(string mode, int speakerId)
        {
            if (speakerId <= 0)
            {
                return DefaultSpeakerForMode(mode);
            }

            var normalizedMode = NormalizeMode(mode);
            if (normalizedMode == "aivis" || normalizedMode == "aivis-native")
            {
                return speakerId >= 100000 ? speakerId : DefaultSpeakerForMode(mode);
            }

            return speakerId < 100000 ? speakerId : DefaultSpeakerForMode(mode);
        }
    }
}
