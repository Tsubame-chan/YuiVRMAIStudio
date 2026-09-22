using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Threading;
using NUnit.Framework;
using UnityEngine.TestTools;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiAssetHttpThreadTests
    {
        [UnityTest]
        public IEnumerator RealUnityDownloadsSurviveTheThreadSwitchBetweenTwoArchives()
        {
            var root = Path.Combine(Path.GetTempPath(), "Yui download " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var assets = new YuiLocalAiReleaseAsset[2];
                for (var i = 0; i < 2; i++)
                {
                    var name = "data" + i + ".zip"; var zip = Path.Combine(root, name);
                    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                    using (var writer = new StreamWriter(archive.CreateEntry("payload.txt").Open())) writer.Write("archive " + i);
                    assets[i] = new YuiLocalAiReleaseAsset { Id = "fixture" + i, Version = "1", Filename = name,
                        Url = new Uri(zip).AbsoluteUri, InstallRoot = "installed" + i,
                        Platforms = new[] { "macos-arm64" }, InstalledPaths = new[] { "payload.txt" } };
                }
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var downloader = new YuiLocalAiAssetDownloader(new YuiUnityAssetHttpClient(), root, Path.Combine(root, "cache"));
                var pending = downloader.InstallAssetsAsync(new YuiLocalAiAssetManifest(), assets, null, cancel.Token);
                while (!pending.IsCompleted) yield return null;
                var result = pending.GetAwaiter().GetResult();
                Assert.IsTrue(result.Success, result.ErrorMessage);
                Assert.AreEqual(2, result.InstalledAssets.Count);
                for (var i = 0; i < 2; i++) Assert.AreEqual("archive " + i, File.ReadAllText(Path.Combine(root, "installed" + i, "payload.txt")));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
