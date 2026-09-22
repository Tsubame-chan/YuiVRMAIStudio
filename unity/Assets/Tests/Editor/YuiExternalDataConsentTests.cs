using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public class YuiExternalDataConsentTests
    {
        private const string Ledger = "Yui.Privacy.ExternalDataConsent.v1";
        private string previous;
        private bool existed;
        [SetUp] public void SetUp() { existed = PlayerPrefs.HasKey(Ledger); previous = PlayerPrefs.GetString(Ledger); YuiExternalDataConsent.RevokeAll(); }
        [TearDown] public void TearDown() { if (existed) PlayerPrefs.SetString(Ledger, previous); else PlayerPrefs.DeleteKey(Ledger); PlayerPrefs.Save(); }

        [Test] public void PermissionIsExplicitAndScopedToTheExactServer()
        {
            Assert.IsFalse(YuiExternalDataConsent.HasPermission("https://api.openai.com"));
            YuiExternalDataConsent.Grant("https://api.openai.com/");
            Assert.IsTrue(YuiExternalDataConsent.HasPermission("https://api.openai.com"));
            Assert.IsFalse(YuiExternalDataConsent.HasPermission("https://api.openai.com.evil.example"));
            Assert.IsFalse(YuiExternalDataConsent.HasPermission("http://api.openai.com"));
            YuiExternalDataConsent.Grant("http://localhost:8000/path");
            Assert.IsFalse(YuiExternalDataConsent.HasPermission("http://localhost:8001/path"));
            Assert.IsFalse(YuiExternalDataConsent.HasPermission("http://localhost:8000/other"));
            YuiExternalDataConsent.RevokeAll();
            Assert.IsFalse(YuiExternalDataConsent.HasPermission("https://api.openai.com"));
        }

        [TestCase("file:///tmp/test")]
        [TestCase("https://user:secret@example.org")]
        [TestCase("not a url")]
        public void InvalidOrCredentialBearingDestinationsAreRejected(string url)
        { Assert.Throws<ArgumentException>(() => YuiExternalDataConsent.Destination(url)); }

        [Test] public void WaitingForAnAnswerDoesNotStartTheTransport()
        {
            var permission = new TaskCompletionSource<bool>(); var sent = false;
            var request = YuiExternalDataConsent.SendAsync(t => permission.Task, () => { sent = true; return Task.FromResult(42); }, CancellationToken.None);
            Assert.IsFalse(sent); Assert.IsFalse(request.IsCompleted);
            permission.SetResult(true);
            Assert.AreEqual(42, request.GetAwaiter().GetResult()); Assert.IsTrue(sent);
        }

        [Test] public void DenialAndCancellationNeverSendData()
        {
            var sent = false;
            Assert.Throws<TaskCanceledException>(() => YuiExternalDataConsent.SendAsync(
                t => Task.FromCanceled(new CancellationToken(true)), () => { sent = true; return Task.FromResult(0); }, CancellationToken.None).GetAwaiter().GetResult());
            using var source = new CancellationTokenSource();
            Assert.Throws<OperationCanceledException>(() => YuiExternalDataConsent.SendAsync(
                t => { source.Cancel(); return Task.CompletedTask; }, () => { sent = true; return Task.FromResult(0); }, source.Token).GetAwaiter().GetResult());
            Assert.IsFalse(sent);
        }

        [Test] public void DialogDisableCallbackCancelsWithoutGrantingPermission()
        {
            var host = new GameObject("consent"); var completion = new TaskCompletionSource<bool>();
            host.AddComponent<YuiConsentDialogLifetime>().Completion = completion;
            // EditMode does not dispatch runtime MonoBehaviour lifecycle callbacks.
            typeof(YuiConsentDialogLifetime).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(host.GetComponent<YuiConsentDialogLifetime>(), null);
            Assert.IsTrue(completion.Task.IsCanceled);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
