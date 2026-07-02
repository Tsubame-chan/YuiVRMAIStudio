using System;
using System.IO;
using System.Text;
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

        public static AudioClip ToAudioClip(byte[] wavBytes, string clipName = "YuiAudio")
        {
            if (wavBytes == null || wavBytes.Length < 44)
            {
                return null;
            }

            using var stream = new MemoryStream(wavBytes);
            using var reader = new BinaryReader(stream);
            if (ReadFourCc(reader) != "RIFF")
            {
                return null;
            }

            reader.ReadInt32();
            if (ReadFourCc(reader) != "WAVE")
            {
                return null;
            }

            short audioFormat = 1;
            short channels = 1;
            int sampleRate = 24000;
            short bitsPerSample = 16;
            var dataOffset = -1;
            var dataSize = 0;

            while (stream.Position + 8 <= stream.Length)
            {
                var chunkId = ReadFourCc(reader);
                var chunkSize = reader.ReadInt32();
                var chunkEnd = stream.Position + chunkSize;
                if (chunkId == "fmt ")
                {
                    audioFormat = reader.ReadInt16();
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt16();
                    bitsPerSample = reader.ReadInt16();
                }
                else if (chunkId == "data")
                {
                    dataOffset = (int)stream.Position;
                    dataSize = Mathf.Max(0, (int)Math.Min(chunkSize, stream.Length - stream.Position));
                }

                stream.Position = Math.Min(chunkEnd, stream.Length);
                if ((chunkSize & 1) == 1 && stream.Position < stream.Length)
                {
                    stream.Position++;
                }
            }

            if (dataOffset < 0 || dataSize <= 0 || channels <= 0 || sampleRate <= 0)
            {
                return null;
            }

            float[] samples;
            if (audioFormat == 1 && bitsPerSample == 16)
            {
                var sampleCount = dataSize / 2;
                samples = new float[sampleCount];
                for (var index = 0; index < sampleCount; index++)
                {
                    var value = BitConverter.ToInt16(wavBytes, dataOffset + index * 2);
                    samples[index] = value / 32768f;
                }
            }
            else if (audioFormat == 3 && bitsPerSample == 32)
            {
                var sampleCount = dataSize / 4;
                samples = new float[sampleCount];
                for (var index = 0; index < sampleCount; index++)
                {
                    samples[index] = BitConverter.ToSingle(wavBytes, dataOffset + index * 4);
                }
            }
            else
            {
                Debug.LogWarning($"Unsupported WAV format: format={audioFormat}, bits={bitsPerSample}");
                return null;
            }

            var frames = samples.Length / channels;
            if (frames <= 0)
            {
                return null;
            }

            var clip = AudioClip.Create(
                string.IsNullOrWhiteSpace(clipName) ? "YuiAudio" : clipName,
                frames,
                channels,
                sampleRate,
                false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static string ReadFourCc(BinaryReader reader)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(4));
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
