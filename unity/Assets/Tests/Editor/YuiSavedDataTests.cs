using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.UI;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiSavedDataTests
    {
        [Test] public void SavedResultsRetainLegacyTextAndArchiveSidecarTogether()
        {
            var directory = Path.Combine(Path.GetTempPath(), "yui-results-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try {
                File.WriteAllText(Path.Combine(directory, "old.md"), "legacy 日本語");
                File.WriteAllText(Path.Combine(directory, "new.md"), "result");
                File.WriteAllText(Path.Combine(directory, "new.md.json"), "{\"task_id\":\"task-a\"}");
                var store = new YuiSavedResultStore(directory);
                Assert.AreEqual(2, store.List().Length);
                Assert.AreEqual("legacy 日本語", store.Read("old.md"));
                Assert.IsNull(store.Metadata("old.md"));
                store.Archive("new.md");
                Assert.AreEqual(new [] { "old.md" }, store.List());
                Assert.AreEqual(1, Directory.GetFiles(Path.Combine(directory, "Archived"), "*.json", SearchOption.AllDirectories).Length);
                Assert.Throws<ArgumentException>(() => store.Read("../private.md"));
                Assert.Throws<ArgumentException>(() => store.Read("C:\\private.md"));
            } finally { Directory.Delete(directory, true); }
        }

        [Test] public void AlreadyCancelledInferenceNeverInvokesPlatformBridge()
        {
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => YuiGoogleAiEdgeBridge.Invoke(new YuiGoogleAiEdgeBridgeRequest(), cancellation.Token));
        }

        [Test] public void RealtimeCloseCancelsConnectingSocketSynchronously()
        {
            var go = new GameObject("stop-regression"); go.SetActive(false);
            var panel = go.AddComponent<YuiChatPanel>();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            using var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            try {
                typeof(YuiChatPanel).GetField("realtimeSocket", flags).SetValue(panel, new ClientWebSocket());
                typeof(YuiChatPanel).GetField("realtimeCancellationTokenSource", flags).SetValue(panel, cancellation);
                var task = (Task)typeof(YuiChatPanel).GetMethod("CloseRealtimeStreamAsync", flags).Invoke(panel, null);
                Assert.IsTrue(task.IsCompleted);
                Assert.IsTrue(token.IsCancellationRequested);
                Assert.IsNull(typeof(YuiChatPanel).GetField("realtimeSocket", flags).GetValue(panel));
            } finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
