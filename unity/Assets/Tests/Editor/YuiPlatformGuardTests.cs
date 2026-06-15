using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiPlatformGuardTests
    {
        [Test]
        public void WindowsForegroundAppMonitor_IsDisabledInMacEditor()
        {
#if UNITY_EDITOR_OSX
            var gameObject = new GameObject("foreground-monitor-test");
            try
            {
                var monitor = gameObject.AddComponent<YuiWindowsForegroundAppMonitor>();

                Assert.IsFalse(monitor.IsSupported);
                Assert.IsFalse(monitor.GetForegroundApp().IsAvailable);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
#else
            Assert.Pass("This guard is only meaningful in the macOS Unity Editor build.");
#endif
        }
    }
}
