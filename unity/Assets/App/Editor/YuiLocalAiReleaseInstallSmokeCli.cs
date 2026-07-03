using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using YuiPhysicalAI.LocalAI;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.EditorTools
{
    public static class YuiLocalAiReleaseInstallSmokeCli
    {
        private const string DefaultInstallRoot = "/private/tmp/yui-local-ai-release-install-smoke";
        private const string DefaultReportPath = "/private/tmp/yui-local-ai-release-install-smoke-report.txt";

        public static void Run()
        {
            try
            {
                var report = RunAsync().GetAwaiter().GetResult();
                File.WriteAllText(ReportPath(), report);
                Debug.Log(report);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath()));
                File.WriteAllText(ReportPath(), ex.ToString());
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static async Task<string> RunAsync()
        {
            var installRoot = Environment.GetEnvironmentVariable("YUI_LOCAL_AI_SMOKE_ROOT");
            if (string.IsNullOrWhiteSpace(installRoot))
            {
                installRoot = DefaultInstallRoot;
            }

            var manifestUrl = Environment.GetEnvironmentVariable("YUI_LOCAL_AI_SMOKE_MANIFEST_URL");
            if (string.IsNullOrWhiteSpace(manifestUrl))
            {
                manifestUrl = YuiLocalAiDownloadOverlay.DefaultManifestUrl;
            }

            var cacheRoot = Path.Combine(installRoot, "_cache");
            Directory.CreateDirectory(installRoot);
            Directory.CreateDirectory(cacheRoot);

            Debug.Log($"Yui Local AI release install smoke: start manifest={manifestUrl}, root={installRoot}");
            var downloader = new YuiLocalAiAssetDownloader(
                new YuiEditorAssetHttpClient(),
                installRoot,
                cacheRoot);

            var started = DateTimeOffset.UtcNow;
            var progressLines = new StringBuilder();
            long lastDownloadBucket = -1;
            var progress = new DirectProgress(item =>
            {
                if (item == null)
                {
                    return;
                }

                var mb = item.DownloadedBytes / 1024f / 1024f;
                var totalMb = item.TotalBytes > 0 ? item.TotalBytes / 1024f / 1024f : 0f;
                var line = totalMb > 0
                    ? $"{item.Stage}: {mb:0.0} MB / {totalMb:0.0} MB"
                    : $"{item.Stage}: {mb:0.0} MB";
                var bucket = item.DownloadedBytes / (128L * 1024L * 1024L);
                var shouldLog = !string.Equals(item.Stage, "download", StringComparison.OrdinalIgnoreCase)
                    || item.DownloadedBytes >= item.TotalBytes
                    || bucket != lastDownloadBucket;
                if (!shouldLog)
                {
                    return;
                }

                if (string.Equals(item.Stage, "download", StringComparison.OrdinalIgnoreCase))
                {
                    lastDownloadBucket = bucket;
                }

                Debug.Log($"Yui Local AI release install smoke: {line}");
                progressLines.AppendLine(line);
            });

            Debug.Log("Yui Local AI release install smoke: fetching manifest");
            var manifest = await downloader.FetchManifestAsync(manifestUrl, CancellationToken.None).ConfigureAwait(false);
            var platform = Environment.GetEnvironmentVariable("YUI_LOCAL_AI_SMOKE_PLATFORM");
            if (string.IsNullOrWhiteSpace(platform))
            {
                platform = YuiLocalAiModelRegistry.CurrentPlatformKey();
            }

            Debug.Log($"Yui Local AI release install smoke: planning for {platform}");
            var beforePlan = YuiLocalAiAssetStore.PlanRequiredDownloads(
                manifest,
                YuiLocalAiInstalledAssetLedger.Load(downloader.LedgerPath),
                installRoot,
                platform);
            Require(beforePlan.State == YuiLocalAiAssetPlanState.NeedsDownload, $"Expected fresh install to need downloads, got {beforePlan.State}.");

            Debug.Log($"Yui Local AI release install smoke: installing {beforePlan.AssetsToDownload.Count} asset(s)");
            var result = await downloader.InstallAssetsAsync(
                manifest,
                beforePlan.AssetsToDownload,
                progress,
                CancellationToken.None).ConfigureAwait(false);
            Require(result.Success, $"Install failed: {result.ErrorMessage}");

            Debug.Log("Yui Local AI release install smoke: validating installed plan");
            var afterPlan = YuiLocalAiAssetStore.PlanRequiredDownloads(
                manifest,
                YuiLocalAiInstalledAssetLedger.Load(downloader.LedgerPath),
                installRoot,
                platform);
            Require(afterPlan.State == YuiLocalAiAssetPlanState.UpToDate, $"Expected installed assets to be up to date, got {afterPlan.State}.");

            var ledger = YuiLocalAiInstalledAssetLedger.Load(downloader.LedgerPath);
            Require(ledger.Assets.Count > 0, "Install ledger did not record any assets.");
            foreach (var asset in manifest.RequiredAssetsFor(platform))
            {
                var status = YuiLocalAiAssetInstallProbe.Check(asset, installRoot, platform);
                Require(status.Installed, status.Detail);
                Require(ledger.Find(asset.Id) != null, $"Ledger missing asset record: {asset.Id}");
            }

            Debug.Log("Yui Local AI release install smoke: creating report");
            var elapsed = DateTimeOffset.UtcNow - started;
            return string.Join(
                Environment.NewLine,
                "Yui Local AI release install smoke: PASS",
                $"Manifest: {manifest.ReleaseVersion}",
                $"Platform: {platform}",
                $"InstalledAssets: {string.Join(", ", result.InstalledAssets.Select(asset => asset.Id))}",
                $"InstallRoot: {installRoot}",
                $"Ledger: {downloader.LedgerPath}",
                $"ElapsedSeconds: {elapsed.TotalSeconds:0.0}",
                "Progress:",
                progressLines.ToString().TrimEnd());
        }

        private static string ReportPath()
        {
            var path = Environment.GetEnvironmentVariable("YUI_LOCAL_AI_SMOKE_REPORT");
            return string.IsNullOrWhiteSpace(path) ? DefaultReportPath : path;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class DirectProgress : IProgress<YuiLocalAiAssetDownloadProgress>
        {
            private readonly Action<YuiLocalAiAssetDownloadProgress> onReport;

            public DirectProgress(Action<YuiLocalAiAssetDownloadProgress> onReport)
            {
                this.onReport = onReport;
            }

            public void Report(YuiLocalAiAssetDownloadProgress value)
            {
                onReport?.Invoke(value);
            }
        }

        private sealed class YuiEditorAssetHttpClient : IYuiLocalAiAssetHttpClient
        {
            private static readonly HttpClient HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(45)
            };

            public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
            {
                if (TryGetLocalPath(url, out var localPath))
                {
                    return File.ReadAllText(localPath);
                }

                using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            public async Task DownloadFileAsync(
                string url,
                string destinationPath,
                long expectedBytes,
                IProgress<YuiLocalAiAssetDownloadProgress> progress,
                CancellationToken cancellationToken)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                if (TryGetLocalPath(url, out var localPath))
                {
                    await CopyLocalFileAsync(localPath, destinationPath, expectedBytes, progress, cancellationToken).ConfigureAwait(false);
                    return;
                }

                using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var totalBytes = expectedBytes > 0 ? expectedBytes : response.Content.Headers.ContentLength.GetValueOrDefault();
                using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[1024 * 1024];
                long downloaded = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    downloaded += read;
                    var percent = totalBytes > 0 ? Mathf.Clamp01(downloaded / (float)totalBytes) : 0f;
                    progress?.Report(new YuiLocalAiAssetDownloadProgress(url, downloaded, totalBytes, percent, "download"));
                }

                progress?.Report(new YuiLocalAiAssetDownloadProgress(url, downloaded, totalBytes > 0 ? totalBytes : downloaded, 1f, "download"));
            }

            private static bool TryGetLocalPath(string url, out string path)
            {
                path = null;
                if (string.IsNullOrWhiteSpace(url))
                {
                    return false;
                }

                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile)
                {
                    path = uri.LocalPath;
                    return true;
                }

                if (File.Exists(url))
                {
                    path = url;
                    return true;
                }

                return false;
            }

            private static async Task CopyLocalFileAsync(
                string sourcePath,
                string destinationPath,
                long expectedBytes,
                IProgress<YuiLocalAiAssetDownloadProgress> progress,
                CancellationToken cancellationToken)
            {
                var totalBytes = expectedBytes > 0 ? expectedBytes : new FileInfo(sourcePath).Length;
                var buffer = new byte[1024 * 1024];
                long copied = 0;
                using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                int read;
                while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    copied += read;
                    var percent = totalBytes > 0 ? Mathf.Clamp01(copied / (float)totalBytes) : 0f;
                    progress?.Report(new YuiLocalAiAssetDownloadProgress(sourcePath, copied, totalBytes, percent, "download"));
                }

                progress?.Report(new YuiLocalAiAssetDownloadProgress(sourcePath, copied, totalBytes > 0 ? totalBytes : copied, 1f, "download"));
            }
        }
    }
}
