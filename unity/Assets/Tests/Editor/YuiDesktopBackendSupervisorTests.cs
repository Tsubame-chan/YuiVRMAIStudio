using System.IO;
using NUnit.Framework;
using YuiPhysicalAI.Backend;

namespace YuiPhysicalAI.Tests
{
    public sealed class YuiDesktopBackendSupervisorTests
    {
        [Test]
        public void ResolveMacBackendRoot_ReturnsSiblingOfAppBundle()
        {
            var root = Path.Combine(Path.GetTempPath(), "YuiBackendPathTest");
            var contents = Path.Combine(root, "Yui VRM AI Studio.app", "Contents");

            var backendRoot = YuiDesktopBackendPaths.ResolveMacBackendRoot(contents);

            Assert.AreEqual(Path.Combine(root, "YuiBackend"), backendRoot);
        }

        [Test]
        public void ResolveMacBackendRoot_PrefersPersistentInstalledBackend()
        {
            var root = Path.Combine(Path.GetTempPath(), "YuiBackendPathTestPersistent");
            var contents = Path.Combine(root, "Yui VRM AI Studio.app", "Contents");
            var persistent = Path.Combine(root, "ApplicationSupport");
            var scripts = Path.Combine(persistent, "YuiBackend", "scripts");
            Directory.CreateDirectory(scripts);
            File.WriteAllText(Path.Combine(scripts, "start_local_services_detached_macos.sh"), "#!/usr/bin/env bash\n");

            var backendRoot = YuiDesktopBackendPaths.ResolveMacBackendRoot(contents, persistent);

            Assert.AreEqual(Path.Combine(persistent, "YuiBackend"), backendRoot);
        }

        [Test]
        public void ResolveWindowsBackendRoot_ReturnsSiblingOfDataFolder()
        {
            var root = Path.Combine(Path.GetTempPath(), "YuiBackendWindowsPathTest");
            var dataPath = Path.Combine(root, "Yui VRM AI Studio_Data");

            var backendRoot = YuiDesktopBackendPaths.ResolveWindowsBackendRoot(dataPath);

            Assert.AreEqual(Path.Combine(root, "YuiBackend"), backendRoot);
        }

        [Test]
        public void ResolveWindowsBackendRoot_PrefersPersistentInstalledBackend()
        {
            var root = Path.Combine(Path.GetTempPath(), "YuiBackendWindowsPathTestPersistent");
            var dataPath = Path.Combine(root, "Yui VRM AI Studio_Data");
            var persistent = Path.Combine(root, "ApplicationData");
            var scripts = Path.Combine(persistent, "YuiBackend", "scripts");
            Directory.CreateDirectory(scripts);
            File.WriteAllText(Path.Combine(scripts, "start_local_services.ps1"), "Write-Host Yui\n");

            var backendRoot = YuiDesktopBackendPaths.ResolveWindowsBackendRoot(dataPath, persistent);

            Assert.AreEqual(Path.Combine(persistent, "YuiBackend"), backendRoot);
        }

        [Test]
        public void ShouldAutoStart_RequiresLocalBackendUrlAndStartScript()
        {
            var root = Path.Combine(Path.GetTempPath(), "YuiBackendStartDecision");
            var scripts = Path.Combine(root, "scripts");
            Directory.CreateDirectory(scripts);
            File.WriteAllText(Path.Combine(scripts, "start_local_services_detached_macos.sh"), "#!/usr/bin/env bash\n");

            Assert.IsTrue(YuiDesktopBackendSupervisor.ShouldAutoStart("http://127.0.0.1:8000", true, root));
            Assert.IsTrue(YuiDesktopBackendSupervisor.ShouldAutoStart("http://localhost:8000", true, root));
            Assert.IsFalse(YuiDesktopBackendSupervisor.ShouldAutoStart("http://192.168.1.10:8000", true, root));
            Assert.IsFalse(YuiDesktopBackendSupervisor.ShouldAutoStart("http://127.0.0.1:8000", false, root));
            Assert.IsFalse(YuiDesktopBackendSupervisor.ShouldAutoStart("http://127.0.0.1:8000", true, Path.Combine(root, "missing")));
        }
    }
}
