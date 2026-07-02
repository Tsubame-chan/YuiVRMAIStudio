using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace YuiPhysicalAI.LocalAI
{
    [Serializable]
    public sealed class YuiLocalAiAssetManifest
    {
        [JsonProperty("schema_version")]
        public int SchemaVersion { get; set; } = 1;

        [JsonProperty("release_version")]
        public string ReleaseVersion { get; set; }

        [JsonProperty("minimum_app_version")]
        public string MinimumAppVersion { get; set; }

        [JsonProperty("assets")]
        public List<YuiLocalAiReleaseAsset> Assets { get; set; } = new List<YuiLocalAiReleaseAsset>();

        public static YuiLocalAiAssetManifest FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new YuiLocalAiAssetManifest();
            }

            var manifest = JsonConvert.DeserializeObject<YuiLocalAiAssetManifest>(json)
                ?? new YuiLocalAiAssetManifest();
            manifest.Assets = (manifest.Assets ?? new List<YuiLocalAiReleaseAsset>())
                .Where(asset => asset != null && !string.IsNullOrWhiteSpace(asset.Id))
                .ToList();
            return manifest;
        }

        public IEnumerable<YuiLocalAiReleaseAsset> RequiredAssetsFor(string platform)
        {
            return Assets.Where(asset => !asset.Optional && asset.SupportsPlatform(platform));
        }
    }

    [Serializable]
    public sealed class YuiLocalAiReleaseAsset
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("platforms")]
        public string[] Platforms { get; set; } = Array.Empty<string>();

        [JsonProperty("required_for")]
        public string[] RequiredFor { get; set; } = Array.Empty<string>();

        [JsonProperty("optional")]
        public bool Optional { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("parts")]
        public List<YuiLocalAiReleaseAssetPart> Parts { get; set; } = new List<YuiLocalAiReleaseAssetPart>();

        [JsonProperty("sha256")]
        public string Sha256 { get; set; }

        [JsonProperty("size_bytes")]
        public long SizeBytes { get; set; }

        [JsonProperty("install_root")]
        public string InstallRoot { get; set; }

        [JsonProperty("installed_paths")]
        public string[] InstalledPaths { get; set; } = Array.Empty<string>();

        public bool SupportsPlatform(string platform)
        {
            if (Platforms == null || Platforms.Length == 0)
            {
                return true;
            }

            var normalizedPlatform = NormalizePlatform(platform);
            return Platforms.Any(value =>
            {
                var normalizedValue = NormalizePlatform(value);
                return string.Equals(normalizedValue, "all", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedValue, normalizedPlatform, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static string NormalizePlatform(string platform)
        {
            if (string.IsNullOrWhiteSpace(platform))
            {
                return "all";
            }

            var value = platform.Trim().Replace('-', '_').ToLowerInvariant();
            if (value.StartsWith("macos", StringComparison.Ordinal)
                || value.StartsWith("osx", StringComparison.Ordinal)
                || value.StartsWith("darwin", StringComparison.Ordinal))
            {
                return "macos";
            }

            if (value.StartsWith("windows", StringComparison.Ordinal)
                || value.StartsWith("win", StringComparison.Ordinal)
                || value.StartsWith("standalonewindows", StringComparison.Ordinal))
            {
                return "windows";
            }

            if (value.StartsWith("ios", StringComparison.Ordinal)
                || value.StartsWith("iphone", StringComparison.Ordinal))
            {
                return "ios";
            }

            if (value.StartsWith("android", StringComparison.Ordinal))
            {
                return "android";
            }

            return value;
        }
    }

    [Serializable]
    public sealed class YuiLocalAiReleaseAssetPart
    {
        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("sha256")]
        public string Sha256 { get; set; }

        [JsonProperty("size_bytes")]
        public long SizeBytes { get; set; }
    }

    public enum YuiLocalAiAssetInstallState
    {
        Installed,
        Missing,
        UnsupportedPlatform
    }

    public sealed class YuiLocalAiAssetInstallStatus
    {
        public YuiLocalAiAssetInstallStatus(
            YuiLocalAiReleaseAsset asset,
            YuiLocalAiAssetInstallState state,
            IReadOnlyList<string> checkedPaths,
            IReadOnlyList<string> missingPaths,
            string detail)
        {
            Asset = asset;
            State = state;
            CheckedPaths = checkedPaths ?? Array.Empty<string>();
            MissingPaths = missingPaths ?? Array.Empty<string>();
            Detail = detail ?? string.Empty;
        }

        public YuiLocalAiReleaseAsset Asset { get; }
        public YuiLocalAiAssetInstallState State { get; }
        public IReadOnlyList<string> CheckedPaths { get; }
        public IReadOnlyList<string> MissingPaths { get; }
        public string Detail { get; }
        public bool Installed => State == YuiLocalAiAssetInstallState.Installed;
    }

    public static class YuiLocalAiAssetInstallProbe
    {
        public static YuiLocalAiAssetInstallStatus Check(
            YuiLocalAiReleaseAsset asset,
            string assetStorageRoot,
            string platform)
        {
            if (asset == null)
            {
                return new YuiLocalAiAssetInstallStatus(
                    null,
                    YuiLocalAiAssetInstallState.Missing,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    "Local AI asset metadata is missing.");
            }

            if (!asset.SupportsPlatform(platform))
            {
                return new YuiLocalAiAssetInstallStatus(
                    asset,
                    YuiLocalAiAssetInstallState.UnsupportedPlatform,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    $"{asset.Id} does not target {platform}.");
            }

            var root = Path.Combine(assetStorageRoot ?? string.Empty, NormalizeRelativePath(asset.InstallRoot));
            var installedPaths = asset.InstalledPaths ?? Array.Empty<string>();
            var checkedPaths = installedPaths
                .Select(path => Path.Combine(root, NormalizeRelativePath(path)))
                .ToArray();
            var missingPaths = checkedPaths
                .Where(path => !File.Exists(path) && !Directory.Exists(path))
                .ToArray();

            if (missingPaths.Length == 0)
            {
                return new YuiLocalAiAssetInstallStatus(
                    asset,
                    YuiLocalAiAssetInstallState.Installed,
                    checkedPaths,
                    Array.Empty<string>(),
                    $"{asset.DisplayName ?? asset.Id} is installed.");
            }

            return new YuiLocalAiAssetInstallStatus(
                asset,
                YuiLocalAiAssetInstallState.Missing,
                checkedPaths,
                missingPaths,
                $"{asset.DisplayName ?? asset.Id} is missing: {string.Join(", ", missingPaths.Select(Path.GetFileName))}");
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
    }
}
