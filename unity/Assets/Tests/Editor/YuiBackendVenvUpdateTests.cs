using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiBackendVenvUpdateTests
    {
        [TestCase("windows", false)]
        [TestCase("macos", false)]
        [TestCase("windows", true)]
        [TestCase("macos", true)]
        public void BackendUpdateReplacesWholeRuntimeAndRollsBackOnFailure(string platform, bool fail)
        {
            WithRoot(root =>
            {
                var install = Path.Combine(root, "YuiBackend");
                var site = platform == "windows" ? "Lib/site-packages" : "lib/python3.12/site-packages";
                var python = platform == "windows" ? "Scripts/python.exe" : "bin/python";
                Write(Path.Combine(install, "backend/.venv", python), "old-python");
                Write(Path.Combine(install, "backend/.venv", site, "example-1.0.dist-info/METADATA"), "old-metadata");
                Write(Path.Combine(install, "backend/.venv", site, "removed.py"), "obsolete-module");
                Write(Path.Combine(install, "README.txt"), "old-readme");
                var protectedFiles = new[] { ".env", "backend/data/yui.db", "backend/data/voice-library.json",
                    "YuiIrodoriV4/model.safetensors", "../YuiLocalAI/Models/model.bin" };
                foreach (var file in protectedFiles) Write(Path.Combine(install, file), "user-data");
                if (fail) Write(Path.Combine(install, "blocked"), "keep-this-file");
                var files = RuntimeFiles(platform, python);
                files["backend/.venv/" + site + "/example-2.0.dist-info/METADATA"] = "new-metadata";
                files["README.txt"] = "new-readme";
                if (fail) files["blocked/file.txt"] = "cannot-create-parent";
                var result = Install(root, files, platform);
                Assert.AreEqual(!fail, result.Success, result.ErrorMessage);
                Assert.AreEqual(fail ? "old-python" : "new-python", File.ReadAllText(Path.Combine(install, "backend/.venv", python)));
                Assert.AreEqual(fail, File.Exists(Path.Combine(install, "backend/.venv", site, "example-1.0.dist-info/METADATA")));
                Assert.AreEqual(fail, File.Exists(Path.Combine(install, "backend/.venv", site, "removed.py")));
                Assert.AreEqual(!fail, File.Exists(Path.Combine(install, "backend/.venv", site, "example-2.0.dist-info/METADATA")));
                Assert.AreEqual(fail ? "old-readme" : "new-readme", File.ReadAllText(Path.Combine(install, "README.txt")));
                foreach (var file in protectedFiles) Assert.AreEqual("user-data", File.ReadAllText(Path.Combine(install, file)));
                Assert.AreEqual(0, Directory.GetDirectories(root, ".install-*").Length);
                Assert.AreEqual(!fail, File.Exists(Path.Combine(root, YuiLocalAiInstalledAssetLedger.DefaultFileName)));
            });
        }

        [TestCase("windows")]
        [TestCase("macos")]
        public void IncompleteRuntimeCannotReplaceExistingEnvironment(string platform)
        {
            WithRoot(root =>
            {
                var old = Path.Combine(root, "YuiBackend/backend/.venv/old.txt");
                Write(old, "known-good");
                var result = Install(root, new Dictionary<string, string> {
                    ["README.txt"] = "update", ["backend/.venv/partial.txt"] = "incomplete"
                }, platform);
                Assert.IsFalse(result.Success);
                Assert.AreEqual("known-good", File.ReadAllText(old));
            });
        }

        [Test]
        public void MissingWindowsNativeLibraryPreservesExistingRuntime()
        {
            WithRoot(root =>
            {
                var old = Path.Combine(root, "YuiBackend/backend/.venv/old.txt");
                Write(old, "known-good");
                var files = RuntimeFiles("windows", "Scripts/python.exe");
                files["README.txt"] = "update";
                files.Remove("backend/.venv/Lib/site-packages/litert_lm/dxil.dll");
                var result = Install(root, files, "windows");
                Assert.IsFalse(result.Success);
                Assert.AreEqual("known-good", File.ReadAllText(old));
                Assert.IsFalse(File.Exists(Path.Combine(root, "YuiBackend/README.txt")));
            });
        }

        [Test]
        public void SourceOnlyBundleKeepsExistingRuntime()
        {
            WithRoot(root =>
            {
                var old = Path.Combine(root, "YuiBackend/backend/.venv/old.txt");
                Write(old, "known-good");
                var result = Install(root, new Dictionary<string, string> { ["README.txt"] = "update" }, "macos");
                Assert.IsTrue(result.Success, result.ErrorMessage);
                Assert.AreEqual("known-good", File.ReadAllText(old));
            });
        }

        [Test]
        public void MissingRequiredFilePreservesExistingRuntime()
        {
            WithRoot(root =>
            {
                var old = Path.Combine(root, "YuiBackend/backend/.venv/old.txt");
                Write(old, "known-good");
                var result = Install(root, RuntimeFiles("macos", "bin/python"), "macos");
                Assert.IsFalse(result.Success);
                Assert.AreEqual("known-good", File.ReadAllText(old));
            });
        }

        [Test]
        public void NonBackendAssetKeepsUnlistedRuntimeFiles()
        {
            WithRoot(root =>
            {
                var old = Path.Combine(root, "YuiBackend/backend/.venv/old.txt");
                Write(old, "known-good");
                var files = RuntimeFiles("macos", "bin/python");
                files["README.txt"] = "update";
                var result = Install(root, files, "macos", "optional_tts_addon");
                Assert.IsTrue(result.Success, result.ErrorMessage);
                Assert.AreEqual("known-good", File.ReadAllText(old));
            });
        }

        [Test]
        public void RuntimeRollbackFailureKeepsBackupAndCanBeRetried()
        {
            WithRoot(root =>
            {
                var staged = Path.Combine(root, "new");
                var target = Path.Combine(root, "active");
                var backup = Path.Combine(root, "old");
                Write(Path.Combine(staged, "backend/.venv/bin/python"), "new-python");
                Write(Path.Combine(target, "backend/.venv/old.txt"), "known-good");
                var type = typeof(YuiLocalAiAssetDownloader).Assembly.GetType("YuiPhysicalAI.LocalAI.YuiBackendVenvInstallTransaction");
                var transaction = Activator.CreateInstance(type, new object[] { staged, target, backup });
                type.GetMethod("Install").Invoke(transaction, new object[] { CancellationToken.None });
                Directory.CreateDirectory(Path.Combine(staged, "backend/.venv"));
                Assert.Throws<TargetInvocationException>(() => type.GetMethod("Rollback").Invoke(transaction, null));
                Assert.AreEqual("known-good", File.ReadAllText(Path.Combine(backup, "backend/.venv/old.txt")));
                Directory.Delete(Path.Combine(staged, "backend/.venv"));
                type.GetMethod("Rollback").Invoke(transaction, null);
                Assert.AreEqual("known-good", File.ReadAllText(Path.Combine(target, "backend/.venv/old.txt")));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LinkedRuntimeDirectoryCannotMoveOrDeleteExternalFiles(bool nested)
        {
            WithRoot(root =>
            {
                var staged = Path.Combine(root, "new");
                var target = Path.Combine(root, "active");
                var backup = Path.Combine(root, "old");
                var external = Path.Combine(root, "external");
                Write(Path.Combine(external, "keep.txt"), "external-data");
                Write(Path.Combine(staged, "backend/.venv/bin/python"), "new-python");
                var link = Path.Combine(target, "backend/.venv", nested ? "linked" : "");
                Directory.CreateDirectory(Path.GetDirectoryName(link.TrimEnd(Path.DirectorySeparatorChar)));
                var windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                var start = new System.Diagnostics.ProcessStartInfo {
                    FileName = windows ? "powershell.exe" : "/bin/ln", UseShellExecute = false, CreateNoWindow = true,
                    Arguments = windows
                        ? "-NoProfile -NonInteractive -Command \"New-Item -ItemType Junction -Path $env:YUI_TEST_LINK -Target $env:YUI_TEST_LINK_TARGET | Out-Null\""
                        : "-s \"" + external + "\" \"" + link + "\""
                };
                start.EnvironmentVariables["YUI_TEST_LINK"] = link;
                start.EnvironmentVariables["YUI_TEST_LINK_TARGET"] = external;
                using var process = System.Diagnostics.Process.Start(start);
                Assert.IsTrue(process.WaitForExit(15000));
                Assert.AreEqual(0, process.ExitCode);
                try
                {
                    var type = typeof(YuiLocalAiAssetDownloader).Assembly.GetType("YuiPhysicalAI.LocalAI.YuiBackendVenvInstallTransaction");
                    var transaction = Activator.CreateInstance(type, new object[] { staged, target, backup });
                    Assert.Throws<TargetInvocationException>(() => type.GetMethod("Install").Invoke(transaction, new object[] { CancellationToken.None }));
                    type.GetMethod("Rollback").Invoke(transaction, null);
                    Assert.AreEqual("external-data", File.ReadAllText(Path.Combine(external, "keep.txt")));
                    Assert.IsTrue(File.Exists(Path.Combine(staged, "backend/.venv/bin/python")));
                    Assert.IsFalse(Directory.Exists(backup));
                }
                finally { Directory.Delete(link); }
            });
        }

        [Test]
        public void AlreadyCancelledUpdateDoesNotChangeRuntime()
        {
            WithRoot(root =>
            {
                var old = Path.Combine(root, "YuiBackend/backend/.venv/old.txt");
                Write(old, "known-good");
                using var cancel = new CancellationTokenSource();
                cancel.Cancel();
                Assert.Throws<OperationCanceledException>(() => Install(root,
                    new Dictionary<string, string> { ["README.txt"] = "update" }, "macos", token: cancel.Token));
                Assert.AreEqual("known-good", File.ReadAllText(old));
            });
        }

        private static Dictionary<string, string> RuntimeFiles(string platform, string python)
        {
            var files = new Dictionary<string, string> { ["backend/.venv/" + python] = "new-python" };
            if (platform == "windows")
            {
                foreach (var file in YuiDesktopInferenceProcess.WindowsChatFiles)
                    if (!files.ContainsKey(file)) files[file] = "runtime-file";
                files["runtime/voicevox/voicevox_core.dll"] = "voicevox";
                files["runtime/voicevox/voicevox_onnxruntime.dll"] = "onnx";
            }
            return files;
        }

        private static YuiLocalAiAssetInstallResult Install(string root, Dictionary<string, string> files,
            string platform, string kind = "desktop_backend_bundle", CancellationToken token = default)
        {
            using var zip = new MemoryStream();
            using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
                foreach (var file in files)
                    using (var writer = new StreamWriter(archive.CreateEntry(file.Key).Open())) writer.Write(file.Value);
            var bytes = zip.ToArray();
            var asset = new YuiLocalAiReleaseAsset { Id = "backend-update", Kind = kind, InstallRoot = "YuiBackend",
                Filename = "backend.zip", Url = "memory://backend", Platforms = new[] { platform },
                InstalledPaths = new[] { "README.txt" }, Sha256 = BitConverter.ToString(SHA256.Create().ComputeHash(bytes)).Replace("-", "") };
            return new YuiLocalAiAssetDownloader(new MemoryClient(bytes), root, Path.Combine(root, "cache"))
                .InstallAssetsAsync(new YuiLocalAiAssetManifest(), new[] { asset }, null, token).GetAwaiter().GetResult();
        }

        private static void WithRoot(Action<string> test)
        {
            var root = Path.Combine(Path.GetTempPath(), "yui-venv 日本語 空白-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try { test(root); }
            finally { Directory.Delete(root, true); }
        }

        private static void Write(string path, string value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, value);
        }

        private sealed class MemoryClient : IYuiLocalAiAssetHttpClient
        {
            private readonly byte[] bytes;
            public MemoryClient(byte[] bytes) { this.bytes = bytes; }
            public Task<string> GetStringAsync(string url, CancellationToken token) => Task.FromResult("{}");
            public Task DownloadFileAsync(string url, string path, long size,
                IProgress<YuiLocalAiAssetDownloadProgress> progress, CancellationToken token)
            {
                File.WriteAllBytes(path, bytes);
                return Task.CompletedTask;
            }
        }
    }
}
