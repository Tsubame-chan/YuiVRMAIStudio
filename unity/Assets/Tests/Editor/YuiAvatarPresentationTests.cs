using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.Avatar;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiAvatarPresentationTests
    {
        private static readonly MethodInfo Tick = typeof(YuiAvatarPresentation).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void RevealWaitsForPhysicsAndDoesNotEnableHiddenOutfits()
        {
            var root = new GameObject("Avatar");
            var visible = new GameObject("Visible").AddComponent<MeshRenderer>();
            var outfit = new GameObject("Hidden outfit").AddComponent<MeshRenderer>();
            visible.transform.SetParent(root.transform); outfit.transform.SetParent(root.transform);
            outfit.enabled = false; outfit.forceRenderingOff = true;
            try
            {
                var presentation = root.AddComponent<YuiAvatarPresentation>();
                // EditMode does not dispatch this runtime component's lifecycle.
                Invoke(presentation, "OnEnable");
                Assert.IsTrue(visible.forceRenderingOff);
                Tick.Invoke(presentation, null); Tick.Invoke(presentation, null);
                Assert.IsFalse(presentation.IsReady);
                Assert.IsTrue(visible.forceRenderingOff);
                Tick.Invoke(presentation, null);
                Assert.IsTrue(presentation.IsReady);
                Assert.IsFalse(visible.forceRenderingOff);
                Assert.IsFalse(outfit.enabled);
                Assert.IsTrue(outfit.forceRenderingOff);
                Invoke(presentation, "OnDisable"); Invoke(presentation, "OnEnable");
                Assert.IsFalse(presentation.IsReady);
                Assert.IsTrue(visible.forceRenderingOff);
                // Cancellation before reveal must not permanently hide the avatar.
                Invoke(presentation, "OnDisable");
                Assert.IsFalse(visible.forceRenderingOff);
                Assert.IsTrue(outfit.forceRenderingOff);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void Invoke(YuiAvatarPresentation target, string method) =>
            typeof(YuiAvatarPresentation).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        [Test]
        public void PreparedAvatarStaysHiddenUntilSelectionCommits()
        {
            var root = new GameObject("Prepared avatar", typeof(MeshRenderer));
            try
            {
                var presentation = root.AddComponent<YuiAvatarPresentation>();
                presentation.HoldForSelection(); Invoke(presentation, "OnEnable");
                Tick.Invoke(presentation, null); Tick.Invoke(presentation, null); Tick.Invoke(presentation, null);
                Assert.IsTrue(presentation.IsReady);
                Assert.IsTrue(root.GetComponent<Renderer>().forceRenderingOff);
                presentation.ReleaseForDisplay();
                Assert.IsFalse(root.GetComponent<Renderer>().forceRenderingOff);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
