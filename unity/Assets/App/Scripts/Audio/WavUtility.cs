using System;
using System.IO;
using UnityEngine;

namespace YuiPhysicalAI.Audio
{
    public static class WavUtility
    {
        public static byte[] FromAudioClip(AudioClip clip, int sampleCount)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            sampleCount = Mathf.Clamp(sampleCount, 0, clip.samples);
            var sampleData = new float[sampleCount * clip.channels];
            clip.GetData(sampleData, 0);

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            WriteHeader(writer, clip.frequency, clip.channels, sampleData.Length);

            foreach (var sample in sampleData)
            {
                var clamped = Mathf.Clamp(sample, -1f, 1f);
                writer.Write((short)(clamped * short.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteHeader(
            BinaryWriter writer,
            int sampleRate,
            int channels,
            int sampleValueCount)
        {
            const short bitsPerSample = 16;
            var byteRate = sampleRate * channels * bitsPerSample / 8;
            var dataSize = sampleValueCount * bitsPerSample / 8;

            writer.Write(new byte[] { 0x52, 0x49, 0x46, 0x46 });
            writer.Write(36 + dataSize);
            writer.Write(new byte[] { 0x57, 0x41, 0x56, 0x45 });
            writer.Write(new byte[] { 0x66, 0x6d, 0x74, 0x20 });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write(bitsPerSample);
            writer.Write(new byte[] { 0x64, 0x61, 0x74, 0x61 });
            writer.Write(dataSize);
        }
    }
}
