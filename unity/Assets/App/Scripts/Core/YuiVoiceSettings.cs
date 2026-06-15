using UnityEngine;

namespace YuiPhysicalAI.Core
{
    public readonly struct YuiVoiceSettings
    {
        public YuiVoiceSettings(
            int speakerId,
            float speedScale,
            float pitchScale,
            float intonationScale,
            float synthesisVolumeScale,
            float prePhonemeLength,
            float postPhonemeLength)
        {
            SpeakerId = Mathf.Max(0, speakerId);
            SpeedScale = Mathf.Clamp(speedScale, 0.5f, 2.0f);
            PitchScale = Mathf.Clamp(pitchScale, -0.15f, 0.15f);
            IntonationScale = Mathf.Clamp(intonationScale, 0.0f, 2.0f);
            SynthesisVolumeScale = Mathf.Clamp(synthesisVolumeScale, 0.0f, 2.0f);
            PrePhonemeLength = Mathf.Clamp(prePhonemeLength, 0.0f, 1.5f);
            PostPhonemeLength = Mathf.Clamp(postPhonemeLength, 0.0f, 1.5f);
        }

        public int SpeakerId { get; }
        public float SpeedScale { get; }
        public float PitchScale { get; }
        public float IntonationScale { get; }
        public float SynthesisVolumeScale { get; }
        public float PrePhonemeLength { get; }
        public float PostPhonemeLength { get; }
    }
}
