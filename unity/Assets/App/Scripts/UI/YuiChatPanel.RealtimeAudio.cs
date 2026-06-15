using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YuiPhysicalAI.Audio;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private void PlayNextRealtimeQueuedClip()
        {
            if (audioSource == null || audioSource.isPlaying)
            {
                return;
            }

            byte[] pcmBytes = null;
            lock (realtimeAudioLock)
            {
                if (realtimeAudioPcmQueue.Count > 0)
                {
                    pcmBytes = realtimeAudioPcmQueue.Dequeue();
                }
            }
            if (pcmBytes == null || pcmBytes.Length == 0)
            {
                return;
            }

            var previousClip = audioSource.clip;
            var clip = Pcm16BytesToAudioClip(
                pcmBytes,
                24000,
                "YuiRealtimeResponse",
                out var sourcePeak,
                out var appliedGain);
            if (clip == null)
            {
                return;
            }
            audioSource.clip = clip;
            DestroyOwnedAudioClip(previousClip, clip);
            SetStatus("Speaking...");
            Debug.Log(
                $"Yui realtime API audio playback start: bytes={pcmBytes.Length}, source_peak={sourcePeak:F4}, gain={appliedGain:F2}, audio_volume={audioSource.volume:F2}");
            audioSource.Play();
        }

        private static AudioClip Pcm16BytesToAudioClip(
            byte[] pcm,
            int sampleRate,
            string clipName,
            out float sourcePeak,
            out float appliedGain)
        {
            sourcePeak = 0f;
            appliedGain = 1f;
            if (pcm == null || pcm.Length < 2)
            {
                return null;
            }

            var sampleCount = pcm.Length / 2;
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var value = BitConverter.ToInt16(pcm, i * 2);
                var sample = Mathf.Clamp(value / 32768f, -1f, 1f);
                samples[i] = sample;
                sourcePeak = Mathf.Max(sourcePeak, Mathf.Abs(sample));
            }

            if (sourcePeak < YuiRealtimeTuning.AudioMinPlayablePeak)
            {
                return null;
            }

            if (sourcePeak > 0.0001f && sourcePeak < YuiRealtimeTuning.AudioTargetPeak)
            {
                appliedGain = Mathf.Min(
                    YuiRealtimeTuning.AudioMaxAutoGain,
                    YuiRealtimeTuning.AudioTargetPeak / sourcePeak);
                for (var i = 0; i < samples.Length; i++)
                {
                    samples[i] = Mathf.Clamp(samples[i] * appliedGain, -1f, 1f);
                }
            }

            var clip = AudioClip.Create(
                string.IsNullOrWhiteSpace(clipName) ? "YuiRealtimeAudio" : clipName,
                sampleCount,
                1,
                sampleRate > 0 ? sampleRate : 24000,
                false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static byte[] Pcm16BytesToWav(byte[] pcm, int sampleRate)
        {
            if (pcm == null || pcm.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var dataSize = pcm.Length - (pcm.Length % 2);
            using var stream = new MemoryStream(44 + dataSize);
            using var writer = new BinaryWriter(stream);
            const short channels = 1;
            const short bitsPerSample = 16;
            var safeSampleRate = sampleRate > 0 ? sampleRate : 24000;
            var byteRate = safeSampleRate * channels * bitsPerSample / 8;

            writer.Write(new byte[] { 0x52, 0x49, 0x46, 0x46 });
            writer.Write(36 + dataSize);
            writer.Write(new byte[] { 0x57, 0x41, 0x56, 0x45 });
            writer.Write(new byte[] { 0x66, 0x6d, 0x74, 0x20 });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(safeSampleRate);
            writer.Write(byteRate);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write(bitsPerSample);
            writer.Write(new byte[] { 0x64, 0x61, 0x74, 0x61 });
            writer.Write(dataSize);
            writer.Write(pcm, 0, dataSize);
            writer.Flush();
            return stream.ToArray();
        }

    }
}
