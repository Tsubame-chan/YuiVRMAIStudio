using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YuiPhysicalAI.UI;

public class YuiFirstLaunchConsentTests
{
    [UnityTest]
    public IEnumerator DownloadGate_WaitsThroughLater_AndOnlyCompletesOnExplicitStart()
    {
        var host = new GameObject("ConsentTest");
        var overlay = host.AddComponent<YuiLocalAiDownloadOverlay>();
        var view = YuiFirstLaunchView.Create();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(YuiLocalAiDownloadOverlay).GetField("firstLaunchView", flags).SetValue(overlay, view);
        using var cancellation = new CancellationTokenSource();
        try
        {
            var task = (Task)typeof(YuiLocalAiDownloadOverlay).GetMethod("WaitForAppleDownloadApprovalAsync", flags)
                .Invoke(overlay, new object[] { cancellation.Token });
            yield return null;
            Assert.That(task.IsCompleted, Is.False, "Opening setup must not authorize network access.");
            view.transform.Find("SafeArea/DownloadConsent/Dialog/Later").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(task.IsCompleted, Is.False, "Later must leave the download gate closed.");
            Assert.That(view.transform.Find("SafeArea/DownloadConsent"), Is.Null);
            view.transform.Find("SafeArea/Continue").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(task.IsCompleted, Is.False, "Returning to setup must ask again.");
            view.transform.Find("SafeArea/DownloadConsent/Dialog/StartDownload").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));

            var next = (Task)typeof(YuiLocalAiDownloadOverlay).GetMethod("WaitForAppleDownloadApprovalAsync", flags)
                .Invoke(overlay, new object[] { cancellation.Token });
            cancellation.Cancel(); yield return null;
            Assert.That(next.IsCanceled, Is.True, "Closing setup must not authorize a download.");
        }
        finally
        {
            Object.DestroyImmediate(view.gameObject);
            Object.DestroyImmediate(host);
        }
    }
}
