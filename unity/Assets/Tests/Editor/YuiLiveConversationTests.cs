using System;
using System.Collections;
using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiLiveConversationTests
    {
        [UnityTest]
        public IEnumerator SyntheticVoiceThroughExistingCloudSttThenBackendAndDirectChat()
        {
            if (Environment.GetEnvironmentVariable("YUI_RUN_LIVE_API_TESTS") != "1")
                Assert.Ignore("External API integration requires explicit permission and synthetic input.");
            var key = Environment.GetEnvironmentVariable("YUI_TEST_OPENAI_KEY");
            Assert.IsFalse(string.IsNullOrEmpty(key), "Test API credential was not supplied.");
            var backend = new YuiBackendClient("http://127.0.0.1:18764");
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var started = System.Diagnostics.Stopwatch.StartNew();
            var stt = backend.TranscribeAudioAsync(File.ReadAllBytes(Environment.GetEnvironmentVariable("YUI_TEST_AUDIO")),
                "synthetic-test.wav", null, timeout.Token);
            while (!stt.IsCompleted) yield return null;
            var transcript = stt.GetAwaiter().GetResult();
            StringAssert.Contains("青い帽子", transcript.Text);
            StringAssert.Contains("散歩", transcript.Text);
            Debug.Log("Live cloud STT: " + transcript.Text + " / elapsed_ms=" + started.ElapsedMilliseconds);
            var request = new ChatRequest { RequestId = Guid.NewGuid().ToString("N"), Secret = true,
                Message = transcript.Text + " 一文で短く返答してください。", CharacterName = "ユイ" };
            started.Restart();
            var chat = backend.SendChatAsync(request, timeout.Token);
            while (!chat.IsCompleted) yield return null;
            var reply = chat.GetAwaiter().GetResult();
            Assert.IsNotEmpty(reply.Text);
            Debug.Log("Live backend OpenAI chat: " + reply.Text + " / elapsed_ms=" + started.ElapsedMilliseconds);
            var direct = new YuiDirectOpenAiClient(key, Environment.GetEnvironmentVariable("YUI_TEST_OPENAI_MODEL"));
            var directStt = direct.TranscribeAudioAsync(File.ReadAllBytes(Environment.GetEnvironmentVariable("YUI_TEST_AUDIO")), "synthetic-test.wav", timeout.Token);
            while (!directStt.IsCompleted) yield return null;
            var directTranscript = directStt.GetAwaiter().GetResult();
            StringAssert.Contains("青い帽子", directTranscript.Text);
            StringAssert.Contains("散歩", directTranscript.Text);
            Debug.Log("Live direct STT (no Backend): " + directTranscript.Text);
            request.RequestId = Guid.NewGuid().ToString("N"); started.Restart();
            var apiChat = direct.SendChatAsync(request, timeout.Token);
            while (!apiChat.IsCompleted) yield return null;
            var directReply = apiChat.GetAwaiter().GetResult();
            Assert.IsNotEmpty(directReply.Text);
            Debug.Log("Live direct OpenAI chat: " + directReply.Text + " / elapsed_ms=" + started.ElapsedMilliseconds);
        }
    }
}
