using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace YuiPhysicalAI.LocalAI
{
    public interface IYuiLocalAiAssetHttpClient
    {
        Task<string> GetStringAsync(string url, CancellationToken cancellationToken);

        Task DownloadFileAsync(
            string url,
            string destinationPath,
            long expectedBytes,
            IProgress<YuiLocalAiAssetDownloadProgress> progress,
            CancellationToken cancellationToken);
    }

    public sealed class YuiLocalAiAssetDownloadProgress
    {
        public YuiLocalAiAssetDownloadProgress(
            string name,
            long downloadedBytes,
            long totalBytes,
            float percent,
            string stage)
        {
            Name = name ?? string.Empty;
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
            Percent = percent;
            Stage = stage ?? string.Empty;
        }

        public string Name { get; }
        public long DownloadedBytes { get; }
        public long TotalBytes { get; }
        public float Percent { get; }
        public string Stage { get; }
    }

    public sealed class YuiLocalAiAssetInstallResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public IReadOnlyList<YuiLocalAiReleaseAsset> InstalledAssets { get; set; } = Array.Empty<YuiLocalAiReleaseAsset>();
    }

    public sealed class YuiUnityAssetHttpClient : IYuiLocalAiAssetHttpClient
    {
        public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Get(url);
            await SendAsync(request, cancellationToken);
            return request.downloadHandler.text;
        }

        public async Task DownloadFileAsync(
            string url,
            string destinationPath,
            long expectedBytes,
            IProgress<YuiLocalAiAssetDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerFile(destinationPath)
            {
                removeFileOnAbort = true
            };
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var downloaded = (long)request.downloadedBytes;
                progress?.Report(new YuiLocalAiAssetDownloadProgress(
                    url,
                    downloaded,
                    expectedBytes,
                    request.downloadProgress < 0f ? 0f : request.downloadProgress,
                    "download"));
                await Task.Yield();
            }

            ThrowIfRequestFailed(request);
            var size = File.Exists(destinationPath) ? new FileInfo(destinationPath).Length : 0L;
            progress?.Report(new YuiLocalAiAssetDownloadProgress(url, size, expectedBytes > 0 ? expectedBytes : size, 1f, "download"));
        }

        private static async Task SendAsync(UnityWebRequest request, CancellationToken cancellationToken)
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            ThrowIfRequestFailed(request);
        }

        private static void ThrowIfRequestFailed(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                return;
            }

            throw new IOException($"HTTP download failed: {request.error}");
        }
    }

    public sealed class YuiLocalAiAssetDownloader
    {
        private readonly IYuiLocalAiAssetHttpClient httpClient;
        private readonly string assetStorageRoot;
        private readonly string cacheRoot;

        public YuiLocalAiAssetDownloader(
            IYuiLocalAiAssetHttpClient httpClient,
            string assetStorageRoot,
            string cacheRoot)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.assetStorageRoot = assetStorageRoot ?? string.Empty;
            this.cacheRoot = string.IsNullOrWhiteSpace(cacheRoot) ? this.assetStorageRoot : cacheRoot;
        }

        public string LedgerPath => Path.Combine(assetStorageRoot, YuiLocalAiInstalledAssetLedger.DefaultFileName);

        public async Task<YuiLocalAiAssetManifest> FetchManifestAsync(string manifestUrl, CancellationToken cancellationToken)
        {
            var json = await httpClient.GetStringAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
            return YuiLocalAiAssetManifest.FromJson(json);
        }

        public async Task<YuiLocalAiAssetInstallResult> InstallAssetsAsync(
            YuiLocalAiAssetManifest manifest,
            IEnumerable<YuiLocalAiReleaseAsset> assets,
            IProgress<YuiLocalAiAssetDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            var installed = new List<YuiLocalAiReleaseAsset>();
            try
            {
                Directory.CreateDirectory(assetStorageRoot);
                Directory.CreateDirectory(cacheRoot);
                var ledger = YuiLocalAiInstalledAssetLedger.Load(LedgerPath);
                foreach (var asset in assets ?? Enumerable.Empty<YuiLocalAiReleaseAsset>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await DownloadVerifyAndExtractAsync(asset, progress, cancellationToken).ConfigureAwait(false);
                    var verificationPlatform = asset.Platforms != null && asset.Platforms.Length > 0
                        ? asset.Platforms[0]
                        : YuiLocalAiModelRegistry.CurrentPlatformKey();
                    var status = YuiLocalAiAssetInstallProbe.Check(asset, assetStorageRoot, verificationPlatform);
                    if (!status.Installed)
                    {
                        throw new FileNotFoundException(status.Detail);
                    }

                    ledger.MarkInstalled(asset, manifest?.ReleaseVersion);
                    ledger.Save(LedgerPath);
                    installed.Add(asset);
                }

                return new YuiLocalAiAssetInstallResult
                {
                    Success = true,
                    InstalledAssets = installed
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new YuiLocalAiAssetInstallResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    InstalledAssets = installed
                };
            }
        }

        private async Task DownloadVerifyAndExtractAsync(
            YuiLocalAiReleaseAsset asset,
            IProgress<YuiLocalAiAssetDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            if (asset == null)
            {
                return;
            }

            progress?.Report(new YuiLocalAiAssetDownloadProgress(asset.DisplayName ?? asset.Id, 0, asset.SizeBytes, 0f, "download"));
            var downloadDirectory = Path.Combine(cacheRoot, "Downloads");
            Directory.CreateDirectory(downloadDirectory);
            var zipPath = Path.Combine(downloadDirectory, SafeFileName(asset.Filename ?? $"{asset.Id}.zip"));

            if (asset.Parts != null && asset.Parts.Count > 0)
            {
                await DownloadAndCombinePartsAsync(asset, zipPath, downloadDirectory, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(asset.Url))
                {
                    throw new InvalidDataException($"Asset {asset.Id} does not define a download URL.");
                }

                await httpClient.DownloadFileAsync(asset.Url, zipPath, asset.SizeBytes, progress, cancellationToken).ConfigureAwait(false);
            }

            VerifyFileSha256(asset.Sha256, zipPath, asset.Filename ?? asset.Id);
            var zipSize = File.Exists(zipPath) ? new FileInfo(zipPath).Length : 0L;
            progress?.Report(new YuiLocalAiAssetDownloadProgress(asset.DisplayName ?? asset.Id, zipSize, asset.SizeBytes, 1f, "verify"));
            var installRoot = Path.Combine(assetStorageRoot, NormalizeRelativePath(asset.InstallRoot));
            Directory.CreateDirectory(installRoot);
            ExtractZipSafely(zipPath, installRoot);
            ApplyPostInstallPermissions(asset, installRoot);
            progress?.Report(new YuiLocalAiAssetDownloadProgress(asset.DisplayName ?? asset.Id, zipSize, asset.SizeBytes, 1f, "install"));
        }

        private async Task DownloadAndCombinePartsAsync(
            YuiLocalAiReleaseAsset asset,
            string zipPath,
            string downloadDirectory,
            IProgress<YuiLocalAiAssetDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            var partPaths = new List<string>();
            foreach (var part in asset.Parts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (part == null || string.IsNullOrWhiteSpace(part.Url))
                {
                    throw new InvalidDataException($"Asset {asset.Id} contains an invalid release asset part.");
                }

                var partName = SafeFileName(part.Filename ?? Path.GetFileName(part.Url));
                var partPath = Path.Combine(downloadDirectory, partName);
                await httpClient.DownloadFileAsync(part.Url, partPath, part.SizeBytes, progress, cancellationToken).ConfigureAwait(false);
                VerifyFileSha256(part.Sha256, partPath, partName);
                partPaths.Add(partPath);
            }

            using var output = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            foreach (var partPath in partPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var input = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                await input.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
            }
        }

        private static void VerifyFileSha256(string expectedSha256, string path, string name)
        {
            if (string.IsNullOrWhiteSpace(expectedSha256))
            {
                return;
            }

            using var stream = File.OpenRead(path);
            using var sha256 = SHA256.Create();
            var actual = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            var expected = expectedSha256.Trim().ToLowerInvariant();
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"sha256 mismatch for {name}: expected {expected}, got {actual}");
            }
        }

        private static void ExtractZipSafely(string zipPath, string destinationDirectory)
        {
            var destinationRoot = Path.GetFullPath(destinationDirectory);
            if (!destinationRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                destinationRoot += Path.DirectorySeparatorChar;
            }
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.FullName) || entry.FullName.EndsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }

                var relativePath = NormalizeRelativePath(entry.FullName);
                var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));
                if (!destinationPath.StartsWith(destinationRoot, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Unsafe zip entry path: {entry.FullName}");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        }

        private static void ApplyPostInstallPermissions(YuiLocalAiReleaseAsset asset, string installRoot)
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if (asset == null
                || !string.Equals(asset.Kind, "desktop_backend_bundle", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(installRoot)
                || !Directory.Exists(installRoot))
            {
                return;
            }

            ChmodIfExists(Path.Combine(installRoot, "Start_Yui_Backend.command"), "+x");
            ChmodIfExists(Path.Combine(installRoot, "Stop_Yui_Backend.command"), "+x");
            ChmodIfExists(Path.Combine(installRoot, "scripts"), "-R", "+x");
            ChmodIfExists(Path.Combine(installRoot, "backend", ".venv", "bin"), "-R", "+x");
#endif
        }

        private static void ChmodIfExists(string path, params string[] modeArgs)
        {
            if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
            {
                return;
            }

            try
            {
                var arguments = string.Join(" ", modeArgs.Select(ShellQuote).Concat(new[] { ShellQuote(path) }));
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit(10000);
            }
            catch
            {
                // Permission repair is best-effort; the launch path reports a clearer error if it still cannot execute.
            }
        }

        private static string ShellQuote(string value)
        {
            return $"'{(value ?? string.Empty).Replace("'", "'\\''")}'";
        }

        private static string NormalizeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Trim()
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string SafeFileName(string value)
        {
            var name = string.IsNullOrWhiteSpace(value) ? "asset.zip" : Path.GetFileName(value.Trim());
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name;
        }
    }
}
