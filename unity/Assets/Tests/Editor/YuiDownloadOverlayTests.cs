using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiDownloadOverlayTests
    {
        [Test]
        public void DownloadDialogIsIndependentOfClippedDisabledChatPanelAndCanClose()
        {
            var host = new GameObject("ChatPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(RectMask2D));
            var group = host.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
            var overlay = host.AddComponent<YuiLocalAiDownloadOverlay>();
            GameObject root = null;
            try
            {
                Call(overlay, "EnsureUi");
                root = (GameObject)typeof(YuiLocalAiDownloadOverlay).GetField("root", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(overlay);
                Assert.IsNull(root.transform.parent, "Dialog must not inherit composer clipping/input state.");
                Assert.IsTrue(root.GetComponent<Canvas>().isRootCanvas);
                Call(overlay, "Show"); Canvas.ForceUpdateCanvases();
                var start = root.transform.Find("Panel/DownloadButton").GetComponent<RectTransform>();
                var close = root.transform.Find("Panel/CancelButton").GetComponent<RectTransform>();
                var a = new Vector3[4]; var b = new Vector3[4]; start.GetWorldCorners(a); close.GetWorldCorners(b);
                Assert.Less(b[3].x, a[0].x, "Close and download targets must not overlap.");
                close.GetComponent<Button>().onClick.Invoke();
                Assert.IsFalse(root.activeSelf, "User can dismiss the prompt before downloading.");
            }
            finally
            {
                // DestroyImmediate is used only by the edit-mode test (runtime owner uses Destroy).
                if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(host);
            }
        }
        private static void Call(object owner, string method) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    }
}
