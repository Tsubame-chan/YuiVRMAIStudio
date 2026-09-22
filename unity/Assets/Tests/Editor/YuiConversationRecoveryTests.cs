using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiConversationRecoveryTests
    {
        [UnityTest]
        public IEnumerator NativeMediaKeepsUnityPathsOnMainThreadAndCleansFiles()
        {
            var mainThread = Thread.CurrentThread.ManagedThreadId;
            var cache = Application.temporaryCachePath;
            foreach (var extension in new[] { ".wav", ".jpg" })
            {
                string savedPath = null;
                var pending = YuiNativeMediaFile.RunAsync(new byte[] { 1, 2, 3 }, extension, path =>
                {
                    savedPath = path;
                    Assert.AreNotEqual(mainThread, Thread.CurrentThread.ManagedThreadId);
                    Assert.AreEqual(cache, Path.GetDirectoryName(path));
                    CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
                    return "recognized";
                }, CancellationToken.None);
                while (!pending.IsCompleted) yield return null;
                Assert.AreEqual("recognized", pending.GetAwaiter().GetResult());
                Assert.AreEqual(mainThread, Thread.CurrentThread.ManagedThreadId);
                Assert.IsFalse(File.Exists(savedPath));
            }
        }

        [UnityTest]
        public IEnumerator CancelledNativeMediaDiscardsLateResultAndStillCleansFile()
        {
            using var cancellation = new CancellationTokenSource();
            var entered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();
            var pending = YuiNativeMediaFile.RunAsync(new byte[] { 1 }, ".wav", path =>
            {
                entered.TrySetResult(path);
                if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
                return "late transcript";
            }, cancellation.Token);
            try
            {
                while (!entered.Task.IsCompleted && !pending.IsCompleted) yield return null;
                Assert.IsTrue(entered.Task.IsCompleted);
                cancellation.Cancel(); release.Set();
                while (!pending.IsCompleted) yield return null;
                Assert.Throws<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
                Assert.IsFalse(File.Exists(entered.Task.Result));
            }
            finally { release.Set(); }
        }

        [UnityTest]
        public IEnumerator NativeFailureCleansInputAndNextRequestSucceeds()
        {
            string savedPath = null;
            var failed = YuiNativeMediaFile.RunAsync<string>(new byte[] { 1 }, ".jpg", path =>
            { savedPath = path; throw new InvalidOperationException("fixture recognition failed"); }, CancellationToken.None);
            while (!failed.IsCompleted) yield return null;
            Assert.Throws<InvalidOperationException>(() => failed.GetAwaiter().GetResult());
            Assert.IsFalse(File.Exists(savedPath));
            var retry = YuiNativeMediaFile.RunAsync(new byte[] { 2 }, ".jpg", File.ReadAllBytes, CancellationToken.None);
            while (!retry.IsCompleted) yield return null;
            CollectionAssert.AreEqual(new byte[] { 2 }, retry.GetAwaiter().GetResult());
        }

        [TestCase(YuiConversationModes.LocalAi, "local-stt", "local-chat")]
        [TestCase(YuiConversationModes.DirectOpenAi, "api-stt", "api-chat")]
        [TestCase(YuiConversationModes.Stable, "backend-stt", "backend-chat")]
        public void VoiceToReplyPreservesLocalAndCloudRecognitionRoutes(string mode, string stt, string chat)
        {
            var calls = new List<string>(); var runtime = new ProbeRuntime(calls);
            var service = new YuiLocalAiService(runtime);
            var prefs = YuiLocalAiRuntimePreferencePolicy.For(mode, "voicevox-native", false);
            var router = new YuiAiRuntimeRouter(service,
                (request, token) => { calls.Add(mode == YuiConversationModes.DirectOpenAi ? "api-chat" : "backend-chat"); Assert.AreEqual("recognized words", request.Message); return Task.FromResult(new ChatResponse { Text = "reply" }); },
                (bytes, file, duration, token) => { calls.Add("backend-stt"); return Task.FromResult(new SttResponse { Text = "recognized words" }); },
                (bytes, file, prompt, mime, token) => throw new AssertionException("Unexpected image request")) {
                PreferLocalChat = prefs.PreferLocalChat, PreferLocalTranscription = prefs.PreferLocalTranscription,
                FallbackToBackend = prefs.FallbackToBackend, FallbackToBackendTranscription = prefs.FallbackToBackendTranscription,
                SelectEndpoint = (capability, token) => Task.FromResult(YuiAiEndpointPolicy.Resolve(mode,
                    new HealthResponse { Status = "ok", Providers = new Dictionary<string, object> { ["openai_configured"] = true } }, true, capability)),
                DirectChat = (request, token) => { calls.Add("api-chat"); return Task.FromResult(new ChatResponse { Text = "reply" }); },
                DirectTranscribe = (bytes, file, token) => { calls.Add("api-stt"); return Task.FromResult(new SttResponse { Text = "recognized words" }); }
            };
            var transcript = router.TranscribeAsync(new byte[64], "mic.wav", 1000, CancellationToken.None).GetAwaiter().GetResult();
            var response = router.SendChatAsync(new ChatRequest { Message = transcript.Text }, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual("reply", response.Text);
            var audio = service.SynthesizeSpeechAsync(new YuiLocalAiSpeechRequest { Text = response.Text }, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(audio.Success);
            CollectionAssert.AreEqual(new[] { stt, chat, "local-tts" }, calls);
        }

        [TestCase("stt")][TestCase("chat")][TestCase("tts")][TestCase("vision")]
        public void CancelledRuntimeDoesNotReturnLateSuccessOrTryAnotherRuntime(string stage)
        {
            using var stop = new CancellationTokenSource();
            var calls = new List<string>(); var runtime = new ProbeRuntime(calls) { OnCall = stop.Cancel };
            var service = new YuiLocalAiService(new YuiCompositeLocalAiRuntime(new[] { runtime, new ProbeRuntime(calls) }));
            Task pending = stage == "stt" ? (Task)service.TranscribeAsync(new YuiLocalAiAudioRequest(), stop.Token)
                : stage == "chat" ? service.ChatAsync(new YuiLocalAiChatRequest(), stop.Token)
                : stage == "tts" ? service.SynthesizeSpeechAsync(new YuiLocalAiSpeechRequest(), stop.Token)
                : service.AnalyzeImageAsync(new YuiLocalAiVisionRequest(), stop.Token);
            Assert.Throws<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            Assert.AreEqual(1, calls.Count);
        }

        private sealed class ProbeRuntime : IYuiLocalAiRuntime
        {
            private readonly List<string> calls;
            public Action OnCall;
            public ProbeRuntime(List<string> calls) { this.calls = calls; }
            public string RuntimeName => "fixture";
            public YuiLocalAiStatus GetStatus() => new YuiLocalAiStatus { Available = true };
            public bool Supports(YuiLocalAiCapability capability) => true;
            public Task WarmAsync(YuiLocalAiCapability capability, CancellationToken token) => Task.CompletedTask;
            public Task ReleaseAsync(YuiLocalAiCapability capability, CancellationToken token) => Task.CompletedTask;
            private Task<T> Reply<T>(string stage, T value) { calls.Add(stage); OnCall?.Invoke(); return Task.FromResult(value); }
            public Task<YuiLocalAiChatResponse> ChatAsync(YuiLocalAiChatRequest request, CancellationToken token)
                => Reply("local-chat", new YuiLocalAiChatResponse { Success = true, Text = "reply" });
            public Task<YuiLocalAiTranscriptionResponse> TranscribeAsync(YuiLocalAiAudioRequest request, CancellationToken token)
                => Reply("local-stt", new YuiLocalAiTranscriptionResponse { Success = true, Text = "recognized words" });
            public Task<YuiLocalAiSpeechResponse> SynthesizeSpeechAsync(YuiLocalAiSpeechRequest request, CancellationToken token)
                => Reply("local-tts", new YuiLocalAiSpeechResponse { Success = true, AudioBytes = new byte[64] });
            public Task<YuiLocalAiVisionResponse> AnalyzeImageAsync(YuiLocalAiVisionRequest request, CancellationToken token)
                => Reply("local-vision", new YuiLocalAiVisionResponse { Success = true, Summary = "image" });
        }
    }
}
