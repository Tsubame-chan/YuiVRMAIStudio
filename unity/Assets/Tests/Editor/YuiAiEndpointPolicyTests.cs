using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.LocalAI;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiAiEndpointPolicyTests
    {
        private static HealthResponse Backend(bool key) => new HealthResponse { Status = "ok",
            Providers = new Dictionary<string, object> { ["chat_provider"] = "openai", ["openai_configured"] = key } };

        [TestCase(false, false, YuiAiEndpoint.Local)]
        [TestCase(false, true, YuiAiEndpoint.DirectOpenAi)]
        [TestCase(true, false, YuiAiEndpoint.Backend)]
        [TestCase(true, true, YuiAiEndpoint.Backend)]
        public void AutomaticUsesConfiguredEndpointForBothVoiceAndText(bool backendKey, bool appKey, YuiAiEndpoint expected)
        {
            foreach (var capability in new[] { YuiLocalAiCapability.Chat, YuiLocalAiCapability.Transcription })
                Assert.AreEqual(expected, YuiAiEndpointPolicy.Resolve(YuiConversationModes.Stable, Backend(backendKey), appKey, capability));
        }

        [Test]
        public void ExplicitModesNeverBorrowCredentialsOrChangeWithPlaybackVoice()
        {
            foreach (var tts in new[] { "server", "aivis", "server-http", "silent", "local-ai", "voicevox-native", "aivis-native" })
            foreach (var appKey in new[] { false, true })
            foreach (var backendKey in new[] { false, true })
            foreach (var entry in new[] { (YuiConversationModes.LocalAi, YuiAiEndpoint.Local),
                (YuiConversationModes.DirectOpenAi, YuiAiEndpoint.DirectOpenAi),
                (YuiConversationModes.BackendAi, YuiAiEndpoint.Backend),
                (YuiConversationModes.RealtimeVoice, YuiAiEndpoint.Backend),
                (YuiConversationModes.RealtimeVoicevox, YuiAiEndpoint.Backend),
                (YuiConversationModes.RealtimeAivis, YuiAiEndpoint.Backend),
                (YuiConversationModes.RealtimeTranslate, YuiAiEndpoint.Backend) })
            {
                Assert.AreEqual(entry.Item2, YuiAiEndpointPolicy.Resolve(entry.Item1, Backend(backendKey), appKey, YuiLocalAiCapability.Chat));
                var policy = YuiLocalAiRuntimePreferencePolicy.For(entry.Item1, tts, true, true);
                Assert.AreEqual(entry.Item1 == YuiConversationModes.LocalAi, policy.PreferLocalTranscription, entry.Item1 + "/" + tts);
            }
        }

        [Test]
        public void DirectVoiceAndChatNeverCallBackendEvenWhenBackendExists()
        {
            var calls = new List<string>();
            var router = new YuiAiRuntimeRouter(null,
                (_, __) => throw new AssertionException("Backend chat must not be called"),
                (_, __, ___, ____) => throw new AssertionException("Backend STT must not be called"),
                (_, __, ___, ____, _____) => throw new AssertionException("Backend vision must not be called"))
            {
                SelectEndpoint = (capability, token) => Task.FromResult(YuiAiEndpoint.DirectOpenAi),
                DirectTranscribe = (bytes, file, token) => { calls.Add("direct-stt"); return Task.FromResult(new SttResponse { Text = "test" }); },
                DirectChat = (request, token) => { calls.Add("direct-chat"); return Task.FromResult(new ChatResponse { Text = "reply" }); },
                DirectVision = (bytes, mime, token) => { calls.Add("direct-vision"); return Task.FromResult(new VisionResponse { Summary = "image" }); }
            };
            var transcript = router.TranscribeAsync(new byte[] { 1 }, "test.wav", 10, CancellationToken.None).GetAwaiter().GetResult();
            router.SendChatAsync(new ChatRequest { Message = transcript.Text }, CancellationToken.None).GetAwaiter().GetResult();
            router.AnalyzeImageAsync(new byte[] { 1 }, "test.png", "general", "image/png", CancellationToken.None).GetAwaiter().GetResult();
            CollectionAssert.AreEqual(new[] { "direct-stt", "direct-chat", "direct-vision" }, calls);
        }

        [Test]
        public void ReachableBackendWithoutKeyDoesNotAdvertiseRealtimeOrBackendChatReady()
        {
            var snapshot = YuiCapabilityMatrix.FromHealth(Backend(false), true, true, true, true);
            Assert.AreEqual(YuiCapabilityRoute.DirectApi, snapshot.Conversation(YuiConversationModes.Stable).Route);
            Assert.AreEqual(YuiCapabilityState.SetupRequired, snapshot.Conversation(YuiConversationModes.BackendAi).State);
            foreach (var mode in new[] { YuiConversationModes.RealtimeVoice, YuiConversationModes.RealtimeVoicevox,
                YuiConversationModes.RealtimeAivis, YuiConversationModes.RealtimeTranslate })
                Assert.AreEqual(YuiCapabilityState.SetupRequired, snapshot.Conversation(mode).State);
            Assert.IsTrue(snapshot.Conversation(YuiConversationModes.DirectOpenAi).Ready);
        }
    }
}
