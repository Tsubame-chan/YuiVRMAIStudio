using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public static class YuiTtsTuning
    {
        public const float VoicevoxPitchMin = -0.5f;
        public const float VoicevoxPitchMax = 0.5f;
        public const float IrodoriPitchMin = -0.5f;
        public const float IrodoriPitchMax = 0.5f;

        public static float PitchMinForMode(string ttsMode)
        {
            return IsIrodoriMode(ttsMode) ? IrodoriPitchMin : VoicevoxPitchMin;
        }

        public static float PitchMaxForMode(string ttsMode)
        {
            return IsIrodoriMode(ttsMode) ? IrodoriPitchMax : VoicevoxPitchMax;
        }

        public static float SafePitchForMode(string ttsMode, float pitch)
        {
            return Mathf.Clamp(pitch, PitchMinForMode(ttsMode), PitchMaxForMode(ttsMode));
        }

        private static bool IsIrodoriMode(string ttsMode)
        {
            return string.Equals(ttsMode, "server-http", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
