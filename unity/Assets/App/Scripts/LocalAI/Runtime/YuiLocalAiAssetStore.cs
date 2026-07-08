using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace YuiPhysicalAI.LocalAI
{
    public enum YuiLocalAiAssetPlanState
    {
        UpToDate,
        NeedsDownload,
        NoRequiredAssets
    }

    public enum YuiLocalAiAssetNeedReason
    {
        None,
        MissingFiles,
        OutdatedVersion,
        HashChanged
    }

    public sealed class YuiLocalAiAssetPlanItem
    {
        public YuiLocalAiAssetPlanItem(
            YuiLocalAiReleaseAsset asset,
            YuiLocalAiAssetInstallStatus installStatus,
            YuiLocalAiAssetNeedReason needReason)
        {
            Asset = asset;
            InstallStatus = installStatus;
            NeedReason = needReason;
        }

        public YuiLocalAiReleaseAsset Asset { get; }
        public YuiLocalAiAssetInstallStatus InstallStatus { get; }
        public YuiLocalAiAssetNeedReason NeedReason { get; }
        public bool NeedsDownload => NeedReason != YuiLocalAiAssetNeedReason.None;
    }

    public sealed class YuiLocalAiAssetPlan
    {
        public YuiLocalAiAssetPlan(IReadOnlyList<YuiLocalAiAssetPlanItem> items)
        {
            Items = items ?? Array.Empty<YuiLocalAiAssetPlanItem>();
        }

        public IReadOnlyList<YuiLocalAiAssetPlanItem> Items { get; }
        public IReadOnlyList<YuiLocalAiReleaseAsset> AssetsToDownload => Items.Where(item => item.NeedsDownload).Select(item => item.Asset).ToArray();
        public YuiLocalAiAssetPlanState State => Items.Count == 0
            ? YuiLocalAiAssetPlanState.NoRequiredAssets
            : Items.Any(item => item.NeedsDownload)
                ? YuiLocalAiAssetPlanState.NeedsDownload
                : YuiLocalAiAssetPlanState.UpToDate;
    }

    [Serializable]
    public sealed class YuiLocalAiInstalledAssetLedger
    {
        public const string DefaultFileName = "local_ai_installed_assets.json";

        [JsonProperty("schema_version")]
        public int SchemaVersion { get; set; } = 1;

        [JsonProperty("release_version")]
        public string ReleaseVersion { get; set; }

        [JsonProperty("updated_at_utc")]
        public string UpdatedAtUtc { get; set; }

        [JsonProperty("assets")]
        public List<YuiLocalAiInstalledAssetRecord> Assets { get; set; } = new List<YuiLocalAiInstalledAssetRecord>();

        public static YuiLocalAiInstalledAssetLedger Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return new YuiLocalAiInstalledAssetLedger();
            }

            var ledger = JsonConvert.DeserializeObject<YuiLocalAiInstalledAssetLedger>(File.ReadAllText(path))
                ?? new YuiLocalAiInstalledAssetLedger();
            ledger.Assets ??= new List<YuiLocalAiInstalledAssetRecord>();
            return ledger;
        }

        public void Save(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            UpdatedAtUtc = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public YuiLocalAiInstalledAssetRecord Find(string id)
        {
            return Assets.FirstOrDefault(record => string.Equals(record.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public void MarkInstalled(YuiLocalAiReleaseAsset asset, string releaseVersion)
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.Id))
            {
                return;
            }

            ReleaseVersion = releaseVersion;
            var record = Find(asset.Id);
            if (record == null)
            {
                record = new YuiLocalAiInstalledAssetRecord { Id = asset.Id };
                Assets.Add(record);
            }

            record.Version = asset.Version;
            record.Sha256 = asset.Sha256;
            record.Filename = asset.Filename;
            record.InstalledAtUtc = DateTime.UtcNow.ToString("O");
        }
    }

    [Serializable]
    public sealed class YuiLocalAiInstalledAssetRecord
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("sha256")]
        public string Sha256 { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("installed_at_utc")]
        public string InstalledAtUtc { get; set; }
    }

    public static class YuiLocalAiAssetStore
    {
        public static YuiLocalAiAssetPlan PlanRequiredDownloads(
            YuiLocalAiAssetManifest manifest,
            YuiLocalAiInstalledAssetLedger ledger,
            string assetStorageRoot,
            string platform)
        {
            if (manifest == null)
            {
                return new YuiLocalAiAssetPlan(Array.Empty<YuiLocalAiAssetPlanItem>());
            }

            return PlanDownloads(manifest.RequiredAssetsFor(platform), ledger, assetStorageRoot, platform);
        }

        public static YuiLocalAiAssetPlan PlanOptionalDownloads(
            YuiLocalAiAssetManifest manifest,
            YuiLocalAiInstalledAssetLedger ledger,
            string assetStorageRoot,
            string platform,
            string kind = null)
        {
            if (manifest == null)
            {
                return new YuiLocalAiAssetPlan(Array.Empty<YuiLocalAiAssetPlanItem>());
            }

            return PlanDownloads(manifest.OptionalAssetsFor(platform, kind), ledger, assetStorageRoot, platform);
        }

        private static YuiLocalAiAssetPlan PlanDownloads(
            IEnumerable<YuiLocalAiReleaseAsset> assets,
            YuiLocalAiInstalledAssetLedger ledger,
            string assetStorageRoot,
            string platform)
        {
            ledger ??= new YuiLocalAiInstalledAssetLedger();
            var items = new List<YuiLocalAiAssetPlanItem>();
            foreach (var asset in assets ?? Array.Empty<YuiLocalAiReleaseAsset>())
            {
                var installStatus = YuiLocalAiAssetInstallProbe.Check(asset, assetStorageRoot, platform);
                if (installStatus.State == YuiLocalAiAssetInstallState.UnsupportedPlatform)
                {
                    continue;
                }

                var record = ledger.Find(asset.Id);
                var reason = NeedReason(asset, record, installStatus);
                items.Add(new YuiLocalAiAssetPlanItem(asset, installStatus, reason));
            }

            return new YuiLocalAiAssetPlan(items);
        }

        private static YuiLocalAiAssetNeedReason NeedReason(
            YuiLocalAiReleaseAsset asset,
            YuiLocalAiInstalledAssetRecord record,
            YuiLocalAiAssetInstallStatus installStatus)
        {
            if (record != null)
            {
                if (!string.Equals(Normalize(record.Version), Normalize(asset.Version), StringComparison.OrdinalIgnoreCase))
                {
                    return YuiLocalAiAssetNeedReason.OutdatedVersion;
                }

                if (!string.Equals(Normalize(record.Sha256), Normalize(asset.Sha256), StringComparison.OrdinalIgnoreCase))
                {
                    return YuiLocalAiAssetNeedReason.HashChanged;
                }
            }

            if (!installStatus.Installed)
            {
                return YuiLocalAiAssetNeedReason.MissingFiles;
            }

            return record == null ? YuiLocalAiAssetNeedReason.None : YuiLocalAiAssetNeedReason.None;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
