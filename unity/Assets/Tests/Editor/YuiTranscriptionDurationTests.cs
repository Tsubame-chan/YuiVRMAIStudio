using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiTranscriptionDurationTests
    {
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
                (bytes, mime, prompt, token) => Task.FromResult(new VisionResponse()));
            router.SelectEndpoint = (capability, token) => Task.FromResult(endpoint);
            router.DirectTranscribe = (bytes, filename, token) => { calls++; return Task.FromResult(response); };
            Assert.AreSame(response, await router.TranscribeAsync(new byte[0], "test.wav", 60000, CancellationToken.None));
            Assert.AreEqual(1, calls);
        }
    }
}
