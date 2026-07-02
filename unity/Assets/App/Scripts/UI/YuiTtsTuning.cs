using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public static class YuiTtsTuning
    {
        public const float VoicevoxSpeedMin = 0.85f;
        public const float VoicevoxSpeedMax = 1.25f;
        public const float AivisSpeedMin = 0.85f;
        public const float AivisSpeedMax = 1.20f;
        public const float VoicevoxPitchMin = -0.15f;
        public const float VoicevoxPitchMax = 0.18f;
        public const float AivisPitchMin = -0.12f;
        public const float AivisPitchMax = 0.12f;

        public static float SpeedMinForMode(string ttsMode)
        {
            return IsAivisMode(ttsMode) ? AivisSpeedMin : VoicevoxSpeedMin;
        }

        public static float SpeedMaxForMode(string ttsMode)
        {
            return IsAivisMode(ttsMode) ? AivisSpeedMax : VoicevoxSpeedMax;
        }

        public static float SafeSpeedForMode(string ttsMode, float speed)
        {
            return Mathf.Clamp(speed, SpeedMinForMode(ttsMode), SpeedMaxForMode(ttsMode));
        }

        public static float PitchMinForMode(string ttsMode)
        {
            return IsAivisMode(ttsMode) ? AivisPitchMin : VoicevoxPitchMin;
        }

        public static float PitchMaxForMode(string ttsMode)
        {
            return IsAivisMode(ttsMode) ? AivisPitchMax : VoicevoxPitchMax;
        }

        public static float SafePitchForMode(string ttsMode, float pitch)
        {
            return Mathf.Clamp(pitch, PitchMinForMode(ttsMode), PitchMaxForMode(ttsMode));
        }

        private static bool IsAivisMode(string ttsMode)
        {
            return string.Equals(ttsMode, "aivis", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(ttsMode, "server-http", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(ttsMode, "aivis-native", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
