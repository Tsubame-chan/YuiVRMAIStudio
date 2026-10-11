using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine.TestTools;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests
{
    public sealed class YuiSpeechCancellationTests
    {
        // EditMode test frames can run without pumping Unity's Player context.
        // Exercise the actual marshalled continuations with a controlled caller queue.
        private sealed class CallerContext : SynchronizationContext
        {
            private readonly Queue<Action> queue = new Queue<Action>();
            public override void Post(SendOrPostCallback callback, object state)
            { lock (queue) queue.Enqueue(() => callback(state)); }
            public void Pump()
            {
                for (var count = 0; count < 100; count++)
                {
                    Action action;
                    lock (queue) { if (queue.Count == 0) return; action = queue.Dequeue(); }
                    action();
                }
            }
            public Task<YuiPlatformSpeechTranscriptionResult> Start(Func<CancellationToken, Task<bool>> permission,
                Action<string> start, Action<string> cancel, CancellationToken token)
            {
                var previous = Current;
                try { SetSynchronizationContext(this); return YuiAndroidSpeechRecognizer.RunRecognitionAsync(permission, start, cancel, token); }
                finally { SetSynchronizationContext(previous); }
            }
        }
        private static void Result(string id, string text) => YuiAndroidSpeechRecognizer.CompleteNativeResult(
            JsonConvert.SerializeObject(new { request_id = id, message = text }));

        [UnityTest]
        public IEnumerator AndroidStopWithoutNativeCallbackAllowsNextRequestAndRejectsLateResult()
        {
            using var stop = new CancellationTokenSource();
            var caller = new CallerContext();
            string firstId = null, secondId = null;
            var first = caller.Start(_ => Task.FromResult(true),
                id => firstId = id, _ => { }, stop.Token);
            Assert.IsNotNull(firstId);
            stop.Cancel();
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (!first.IsCompleted && DateTime.UtcNow < deadline) { caller.Pump(); yield return null; }
            Assert.IsTrue(first.IsCanceled, "Stop must settle without a Java callback. Status: " + first.Status + "; exception: " + first.Exception);
            var second = caller.Start(_ => Task.FromResult(true),
                id => secondId = id, _ => { }, CancellationToken.None);
            Result(firstId, "stale transcript");
            Assert.IsFalse(second.IsCompleted, "An old callback must not complete the new recording.");
            Result(secondId, "new transcript");
            deadline = DateTime.UtcNow.AddSeconds(3);
            while (!second.IsCompleted && DateTime.UtcNow < deadline) { caller.Pump(); yield return null; }
            Assert.IsTrue(second.IsCompleted, "New result must settle.");
            Assert.AreEqual("new transcript", second.GetAwaiter().GetResult().Text);
        }

        [UnityTest]
        public IEnumerator AndroidReservesSlotWhilePermissionPending()
        {
            var permission = new TaskCompletionSource<bool>();
            var caller = new CallerContext();
            string id = null;
            var first = caller.Start(_ => permission.Task,
                value => id = value, _ => { }, CancellationToken.None);
            var second = caller.Start(_ => Task.FromResult(true),
                _ => Assert.Fail("Concurrent start must not reach JNI."), _ => { }, CancellationToken.None);
            Assert.AreEqual("busy", second.GetAwaiter().GetResult().ErrorCode);
            permission.SetResult(true);
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (id == null && DateTime.UtcNow < deadline) { caller.Pump(); yield return null; }
            Assert.IsNotNull(id);
            Result(id, "accepted");
            deadline = DateTime.UtcNow.AddSeconds(3);
            while (!first.IsCompleted && DateTime.UtcNow < deadline) { caller.Pump(); yield return null; }
            Assert.IsTrue(first.IsCompleted);
            Assert.IsTrue(first.GetAwaiter().GetResult().Ok);
        }

        [Test]
        public void AndroidExplicitStopSettlesWhilePermissionIsPending()
        {
            var caller = new CallerContext();
            var permission = new TaskCompletionSource<bool>();
            var request = caller.Start(_ => permission.Task,
                _ => Assert.Fail("Stopped permission request must not start recording."), _ => { }, CancellationToken.None);
            YuiAndroidSpeechRecognizer.Cancel();
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (!request.IsCompleted && DateTime.UtcNow < deadline) { caller.Pump(); Thread.Sleep(1); }
            Assert.IsTrue(request.IsCompleted);
            Assert.AreEqual("cancelled", request.GetAwaiter().GetResult().ErrorCode);
            permission.SetResult(true);
            caller.Pump();
        }

        [Test]
        public void NativeCancelWakesActiveWorkerAndDoesNotReturnLateAudio()
        {
            using var stop = new CancellationTokenSource();
            using var started = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            int ended = 0;
            var task = Task.Run(() => YuiNativeSpeechRequest.Run("owned", () =>
            {
                started.Set();
                Assert.IsTrue(release.Wait(2000), "Native cancel must wake the worker.");
                return "late audio";
            }, stop.Token, _ => { }, id => { Assert.AreEqual("owned", id); release.Set(); }, _ => Interlocked.Increment(ref ended)));
            Assert.IsTrue(started.Wait(2000));
            stop.Cancel();
            Assert.Catch<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            Assert.AreEqual(1, ended);
        }

        [Test]
        public void CancellationBetweenNativeBeginAndInvokeStillCleansUp()
        {
            using var stop = new CancellationTokenSource();
            bool ended = false, cancelled = false;
            Assert.Catch<OperationCanceledException>(() => YuiNativeSpeechRequest.Run("early", () =>
            {
                Assert.Fail("Already cancelled native request must not start."); return 0;
            }, stop.Token, _ => stop.Cancel(), _ => cancelled = true, _ => ended = true));
            Assert.IsTrue(cancelled); Assert.IsTrue(ended);
        }
    }
}
