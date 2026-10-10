using System;
using System.Reflection;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiTranscriptionDurationTests
    {
        [TestCase(30)]
        [TestCase(31)]
        public void WavDurationComesFromAudioFrames(int seconds)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            int dataSize = seconds * 16000 * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(16000); writer.Write(32000);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(dataSize); writer.Write(new byte[dataSize]);
            var method = typeof(YuiAiRuntimeRouter).GetMethod("TryReadWavInfo", BindingFlags.NonPublic | BindingFlags.Static);
            object[] args = { stream.ToArray(), 0, 0d };
            Assert.IsTrue((bool)method.Invoke(null, args));
            Assert.AreEqual(16000, args[1]); Assert.AreEqual(seconds * 1000d, args[2]);
            var truncated = stream.ToArray(); Array.Resize(ref truncated, truncated.Length - 2);
            args[0] = truncated;
            Assert.IsFalse((bool)method.Invoke(null, args));
        }

        [TestCase(0xfffffff8u)]
        [TestCase(0xffffffffu)]
        public void InvalidChunkCannotLoopOrReadOutsideWav(uint chunkSize)
        {
            var bytes = new byte[28];
            Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
            Encoding.ASCII.GetBytes("WAVEJUNK").CopyTo(bytes, 8);
            BitConverter.GetBytes(chunkSize).CopyTo(bytes, 16);
            var method = typeof(YuiAiRuntimeRouter).GetMethod("TryReadWavSampleRate", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(method.Invoke(null, new object[] { bytes }));
        }

        [TestCase(30000, false)]
        [TestCase(30001, true)]
        [TestCase(60000, true)]
        public void LocalDurationBoundary(int durationMs, bool rejected)
        {
            var validator = typeof(YuiAiRuntimeRouter).GetMethod("ValidateLocalTranscriptionDuration",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(validator);
            if (rejected)
                Assert.IsInstanceOf<InvalidOperationException>(Assert.Throws<TargetInvocationException>(
                    () => validator.Invoke(null, new object[] { (int?)durationMs })).InnerException);
            else
                Assert.DoesNotThrow(() => validator.Invoke(null, new object[] { (int?)durationMs }));
        }

        [TestCase(YuiAiEndpoint.Backend)]
        [TestCase(YuiAiEndpoint.DirectOpenAi)]
        public async Task ApiSpeechCanStillReceiveSixtySeconds(YuiAiEndpoint endpoint)
        {
            var calls = 0;
            var response = new SttResponse();
            var router = new YuiAiRuntimeRouter(null,
                (request, token) => Task.FromResult(new ChatResponse()),
                (bytes, filename, duration, token) => { calls++; return Task.FromResult(response); },
                (bytes, filename, prompt, mime, token) => Task.FromResult(new VisionResponse()));
            router.SelectEndpoint = (capability, token) => Task.FromResult(endpoint);
            router.DirectTranscribe = (bytes, filename, token) => { calls++; return Task.FromResult(response); };
            Assert.AreSame(response, await router.TranscribeAsync(new byte[0], "test.wav", 60000, CancellationToken.None));
            Assert.AreEqual(1, calls);
        }
    }
}
