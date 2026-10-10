using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiDesktopInferenceTests
    {
        [TestCase("a b", "\"a b\"")]
        [TestCase("C:\\Program Files\\Yui\\", "\"C:\\Program Files\\Yui\\\\\"")]
        [TestCase("a\"b", "\"a\\\"b\"")]
        public void WorkerPathQuotingPreservesSpacesQuotesAndTrailingSlashes(string value, string expected)
            => Assert.AreEqual(expected, YuiDesktopInferenceProcess.QuoteArgument(value));

        [UnityTest]
        public IEnumerator WorkerDoesNotInheritAnotherPythonHome()
        {
            if (!YuiDesktopInferenceProcess.IsAvailable) Assert.Ignore("Bundled desktop Python is not installed.");
            var home = Environment.GetEnvironmentVariable("PYTHONHOME");
            var path = Environment.GetEnvironmentVariable("PYTHONPATH");
            try
            {
                Environment.SetEnvironmentVariable("PYTHONHOME", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
                Environment.SetEnvironmentVariable("PYTHONPATH", "not-the-worker-environment");
                var task = Task.Run(() => YuiDesktopInferenceProcess.Invoke("{\"capability\":\"Chat\",\"model_path\":\"missing-model-for-environment-test\"}", CancellationToken.None));
                while (!task.IsCompleted) yield return null;
                var response = Newtonsoft.Json.Linq.JObject.Parse(task.GetAwaiter().GetResult());
                // The worker can start and report a controlled error. A foreign
                // PYTHONHOME would kill Python before it can emit this response.
                Assert.IsFalse(response.Value<bool>("ok"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("PYTHONHOME", home);
                Environment.SetEnvironmentVariable("PYTHONPATH", path);
            }
        }

        [UnityTest]
        public IEnumerator ActualDesktopWorkerTranscribesAndChatsWithoutBackend()
        {
            if (Environment.GetEnvironmentVariable("YUI_RUN_NATIVE_AI_TESTS") != "1") Assert.Ignore("Native model integration is opt-in.");
            Assert.IsTrue(YuiDesktopInferenceProcess.IsAvailable);
            var runtime = YuiLocalAiRuntimeFactory.Create(YuiLocalAiModelRegistry.CreateDefaultLocalAi());
            Assert.IsTrue(runtime.Supports(YuiLocalAiCapability.Chat));
            Assert.IsTrue(runtime.Supports(YuiLocalAiCapability.Transcription));
            var wav = File.ReadAllBytes(Environment.GetEnvironmentVariable("YUI_NATIVE_STT_WAV"));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var stt = runtime.TranscribeAsync(new YuiLocalAiAudioRequest { AudioBytes = wav }, cancellation.Token);
            while (!stt.IsCompleted) yield return null;
            var transcript = stt.GetAwaiter().GetResult(); Assert.IsTrue(transcript.Success, transcript.ErrorMessage);
            StringAssert.Contains("帽子", transcript.Text);
            var chat = runtime.ChatAsync(new YuiLocalAiChatRequest { CharacterName = "ユイ", Message = transcript.Text,
                CustomInstruction = "青い帽子にひとこと感想を返してください。" }, cancellation.Token);
            while (!chat.IsCompleted) yield return null;
            var response = chat.GetAwaiter().GetResult(); Assert.IsTrue(response.Success, response.ErrorMessage);
            Assert.IsNotEmpty(response.Text);
            Debug.Log("Native local STT: " + transcript.Text + " / Native local chat: " + response.Text);
            using var stop = new CancellationTokenSource();
            var pending = Task.Run(() => YuiDesktopInferenceProcess.Invoke("{\"capability\":\"Chat\",\"model_path\":\"missing\"}", stop.Token));
            stop.Cancel(); while (!pending.IsCompleted) yield return null;
            Assert.Throws<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
        }
    }
}
