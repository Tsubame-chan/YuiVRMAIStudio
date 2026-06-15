using System;
using UnityEngine;

namespace YuiPhysicalAI.Audio
{
    public sealed class YuiUnityMicrophoneRecorder
    {
        public readonly struct StopResult
        {
            public StopResult(AudioClip clip, int samplePosition, bool wasStillRecording)
            {
                Clip = clip;
                SamplePosition = samplePosition;
                WasStillRecording = wasStillRecording;
            }

            public AudioClip Clip { get; }
            public int SamplePosition { get; }
            public bool WasStillRecording { get; }
        }

        public AudioClip Clip { get; private set; }
        public string ActiveDevice { get; private set; }
        public int ActiveFrequency { get; private set; }

        public bool HasClip => Clip != null && !string.IsNullOrEmpty(ActiveDevice);

        public bool Start(string device, int frequency, int clipLengthSeconds, bool loop)
        {
            ActiveDevice = device;
            ActiveFrequency = frequency;
            Clip = null;

            try
            {
                Clip = Microphone.Start(device, loop, clipLengthSeconds, frequency);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Starting microphone failed for '{device}': {ex.Message}");
                Clip = null;
            }

            if (Clip != null)
            {
                return true;
            }

            Debug.LogWarning($"Starting microphone returned null for '{device}'.");
            return false;
        }

        public bool IsRecording()
        {
            return HasClip && Microphone.IsRecording(ActiveDevice);
        }

        public int GetPosition()
        {
            return HasClip ? Microphone.GetPosition(ActiveDevice) : 0;
        }

        public StopResult Stop()
        {
            var clip = Clip;
            var device = ActiveDevice;
            var samplePosition = HasClip ? Microphone.GetPosition(device) : 0;
            var wasStillRecording = HasClip && Microphone.IsRecording(device);
            if (!string.IsNullOrEmpty(device))
            {
                Microphone.End(device);
            }

            if (samplePosition <= 0 && !wasStillRecording && clip != null)
            {
                samplePosition = clip.samples;
            }

            Clip = null;
            ActiveDevice = null;
            ActiveFrequency = 0;
            return new StopResult(clip, samplePosition, wasStillRecording);
        }

        public float RecentLevel(float[] sampleBuffer, float fallbackLevel = 0f)
        {
            if (!HasClip || sampleBuffer == null || sampleBuffer.Length == 0)
            {
                return fallbackLevel;
            }

            var position = GetPosition();
            if (position <= sampleBuffer.Length)
            {
                return fallbackLevel;
            }

            Clip.GetData(sampleBuffer, position - sampleBuffer.Length);
            var sum = 0f;
            for (var index = 0; index < sampleBuffer.Length; index++)
            {
                sum += sampleBuffer[index] * sampleBuffer[index];
            }

            var rms = Mathf.Sqrt(sum / sampleBuffer.Length);
            return rms > 0.005f ? Mathf.Max(0.06f, rms * 32f) : fallbackLevel;
        }

        public float[] ReadSamplesBetween(int fromPosition, int currentPosition, out int nextPosition)
        {
            nextPosition = fromPosition;
            if (!HasClip || currentPosition == fromPosition)
            {
                return Array.Empty<float>();
            }

            var sampleCount = currentPosition > fromPosition
                ? currentPosition - fromPosition
                : Clip.samples - fromPosition + currentPosition;
            if (sampleCount <= 0)
            {
                return Array.Empty<float>();
            }

            var data = new float[sampleCount * Clip.channels];
            if (currentPosition > fromPosition)
            {
                Clip.GetData(data, fromPosition);
            }
            else
            {
                var tailSamples = Clip.samples - fromPosition;
                var tailData = new float[tailSamples * Clip.channels];
                var headData = new float[currentPosition * Clip.channels];
                Clip.GetData(tailData, fromPosition);
                if (currentPosition > 0)
                {
                    Clip.GetData(headData, 0);
                }
                Array.Copy(tailData, 0, data, 0, tailData.Length);
                Array.Copy(headData, 0, data, tailData.Length, headData.Length);
            }

            nextPosition = currentPosition;
            return data;
        }

        public static (float rms, float peak) CalculateAudioStats(AudioClip clip, int sampleCount)
        {
            if (clip == null || sampleCount <= 0)
            {
                return (0f, 0f);
            }

            sampleCount = Mathf.Clamp(sampleCount, 0, clip.samples);
            var data = new float[sampleCount * clip.channels];
            clip.GetData(data, 0);
            var sum = 0f;
            var peak = 0f;
            for (var i = 0; i < data.Length; i++)
            {
                var value = Mathf.Abs(data[i]);
                sum += data[i] * data[i];
                if (value > peak)
                {
                    peak = value;
                }
            }

            var rms = data.Length > 0 ? Mathf.Sqrt(sum / data.Length) : 0f;
            return (rms, peak);
        }
    }
}
