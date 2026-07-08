using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.LocalAI;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiLocalAiFoundationTests
    {
        [Test]
        public void Registry_ReturnsEnabledPacksForCapabilityByPriority()
        {
            var registry = new YuiLocalAiModelRegistry(
                new[]
                {
                    new YuiLocalAiModelPack
                    {
                        Id = "google_vision_high",
                        DisplayName = "Google Vision High",
                        Provider = "google-litert-lm",
                        ModelId = "gemma-vision-full",
                        Capabilities = new[] { YuiLocalAiCapability.Vision },
                        EnabledByDefault = false,
                        Priority = 20
                    },
                    new YuiLocalAiModelPack
                    {
                        Id = "google_vision_mobile",
                        DisplayName = "Google Vision Mobile",
                        Provider = "google-litert-lm",
                        ModelId = "gemma-vision-light",
                        Capabilities = new[] { YuiLocalAiCapability.Vision },
                        EnabledByDefault = true,
                        Priority = 10
                    }
                });

            var packs = registry.EnabledFor(YuiLocalAiCapability.Vision).ToArray();

            Assert.AreEqual(1, packs.Length);
            Assert.AreEqual("google_vision_mobile", packs[0].Id);
        }

        [Test]
        public void Registry_DefaultLocalAiManifestIncludesTextVisionAndTtsPacks()
        {
            var registry = YuiLocalAiModelRegistry.CreateDefaultLocalAi();

            Assert.AreEqual("core_text", registry.BestFor(YuiLocalAiCapability.Chat, "macos").Id);
            Assert.AreEqual("core_text", registry.BestFor(YuiLocalAiCapability.Chat, "windows").Id);
            Assert.AreEqual("core_text_e2b", registry.BestFor(YuiLocalAiCapability.Chat, "ios").Id);
            Assert.AreEqual("core_text_e2b", registry.BestFor(YuiLocalAiCapability.Chat, "android").Id);
            Assert.IsTrue(YuiLocalAiModelRegistry.SupportsPlatform(
                registry.Packs.First(pack => pack.Id == "core_text_e2b"),
                "macos"));
            Assert.AreEqual("vision_gemma4_e2b", registry.BestFor(YuiLocalAiCapability.Vision, "ios").Id);
            Assert.AreEqual(
                "gemma-4-E2B-it.litertlm",
                registry.Packs.First(pack => pack.Id == "vision_gemma4_e2b").RuntimeModelRef);
            Assert.AreEqual(0, registry.Packs.First(pack => pack.Id == "vision_gemma4_e2b").DiskBudgetMb);
            Assert.IsNull(registry.Packs.FirstOrDefault(pack => string.Equals(pack.Provider, "liquid", StringComparison.OrdinalIgnoreCase)));
            Assert.IsNotNull(registry.Packs.FirstOrDefault(pack => pack.Id == "tts_kokoro_sherpa_onnx"));
            Assert.IsNotNull(registry.Packs.FirstOrDefault(pack => pack.Id == "tts_aivis_desktop_audition"));
            Assert.IsFalse(registry.Packs.First(pack => pack.Id == "core_text_12b_experimental").EnabledByDefault);
        }

        [Test]
        public void Registry_DefaultOfflinePacksDoNotDependOnLocalServers()
        {
            var registry = YuiLocalAiModelRegistry.CreateDefaultLocalAi();

            foreach (var pack in registry.Packs.Where(pack => pack.EnabledByDefault))
            {
                Assert.AreEqual(
                    YuiLocalAiDeploymentKind.OnDeviceEmbedded,
                    pack.DeploymentKind,
                    $"{pack.Id} must stay embeddable for the mobile offline default.");
                Assert.IsTrue(
                    string.IsNullOrWhiteSpace(pack.LocalServerBaseUrl),
                    $"{pack.Id} must not require a local HTTP server for the mobile offline default.");
            }

            Assert.AreEqual(
                YuiLocalAiDeploymentKind.DesktopAudition,
                registry.Packs.First(pack => pack.Id == "tts_aivis_desktop_audition").DeploymentKind);
        }

        [Test]
        public void Registry_BestForCanFilterProviderSpecificRuntimePacks()
        {
            var registry = YuiLocalAiModelRegistry.CreateDefaultLocalAi();

            var googleText = registry.BestFor(
                YuiLocalAiCapability.Chat,
                pack => pack.Provider == "google-litert-lm");
            var googleMobileText = registry.BestFor(
                YuiLocalAiCapability.Chat,
                "ios",
                pack => pack.Provider == "google-litert-lm");
            var googleVision = registry.BestFor(
                YuiLocalAiCapability.Vision,
                "ios",
                pack => pack.Provider == "google-litert-lm");

            Assert.AreEqual("core_text", googleText.Id);
            Assert.AreEqual("core_text_e2b", googleMobileText.Id);
            Assert.AreEqual("vision_gemma4_e2b", googleVision.Id);
        }

        [Test]
        public void ModelPathResolver_RuntimeCacheIsNotInsideStreamingAssets()
        {
            var pack = new YuiLocalAiModelPack
            {
                Id = "core_text",
                RuntimeModelRef = "gemma-4-E4B-it.litertlm"
            };

            var cacheDirectory = YuiLocalAiModelPathResolver.RuntimeCacheDirectory(pack);

            StringAssert.Contains("RuntimeCache", cacheDirectory);
            StringAssert.DoesNotContain("StreamingAssets", cacheDirectory);
        }

        [Test]
        public void RuntimeFactory_ComposesGoogleTextWithLightweightVision()
        {
            var runtime = YuiLocalAiRuntimeFactory.Create(YuiLocalAiModelRegistry.CreateDefaultLocalAi());
            var status = runtime.GetStatus();

            Assert.AreEqual("composite-local-ai", runtime.RuntimeName);
            Assert.IsTrue(runtime.Supports(YuiLocalAiCapability.Chat));
            Assert.IsFalse(runtime.Supports(YuiLocalAiCapability.SpeechSynthesis));
            Assert.IsTrue(runtime.Supports(YuiLocalAiCapability.Vision));
            StringAssert.Contains("litert-lm", status.Detail);
            StringAssert.Contains("lightweight-image-vision", status.Detail);
        }

        [Test]
        public void RuntimeFactory_CanReportEmbeddedChatPackBeforeRuntimeIsCreated()
        {
            var registry = YuiLocalAiModelRegistry.CreateDefaultLocalAi();

            Assert.IsTrue(YuiLocalAiRuntimeFactory.HasOnDeviceEmbeddedPack(
                registry,
                YuiLocalAiCapability.Chat,
                _ => true));
            Assert.IsFalse(YuiLocalAiRuntimeFactory.HasOnDeviceEmbeddedPack(
                registry,
                YuiLocalAiCapability.SpeechSynthesis,
                _ => true));
            Assert.IsFalse(YuiLocalAiRuntimeFactory.HasOnDeviceEmbeddedPack(
                registry,
                YuiLocalAiCapability.Chat,
                _ => false));
        }

        [Test]
        public void AssetManifest_FromJsonReadsReleaseAssetMetadata()
        {
            var json = @"{
  ""schema_version"": 1,
  ""release_version"": ""v0.2.0-beta.2"",
  ""minimum_app_version"": ""0.2.0-beta.2"",
  ""assets"": [
    {
      ""id"": ""desktop-local-gemma-e4b"",
      ""display_name"": ""Local Gemma SLM"",
      ""kind"": ""local_ai_model"",
      ""platforms"": [""macos-arm64"", ""windows-x64""],
      ""required_for"": [""local_chat""],
      ""optional"": false,
      ""version"": ""2026.07.02"",
	      ""filename"": ""YuiVRMAIStudio_LocalGemma_E4B_v20260702.zip"",
	      ""url"": ""https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.0-beta.2/YuiVRMAIStudio_LocalGemma_E4B_v20260702.zip"",
	      ""parts"": [
	        {
	          ""filename"": ""YuiVRMAIStudio_LocalGemma_E4B_v20260702.zip.part-000"",
	          ""url"": ""https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.0-beta.2/YuiVRMAIStudio_LocalGemma_E4B_v20260702.zip.part-000"",
	          ""sha256"": ""partsha"",
	          ""size_bytes"": 100
	        }
	      ],
	      ""sha256"": ""0123456789abcdef"",
	      ""size_bytes"": 1234567890,
      ""install_root"": ""YuiLocalAI"",
      ""installed_paths"": [""Models/gemma-4-E4B-it.litertlm""]
    }
  ]
}";

            var manifest = YuiLocalAiAssetManifest.FromJson(json);
            var asset = manifest.RequiredAssetsFor("macos").Single();

            Assert.AreEqual(1, manifest.SchemaVersion);
            Assert.AreEqual("v0.2.0-beta.2", manifest.ReleaseVersion);
            Assert.AreEqual("0.2.0-beta.2", manifest.MinimumAppVersion);
            Assert.AreEqual("desktop-local-gemma-e4b", asset.Id);
            Assert.AreEqual("Local Gemma SLM", asset.DisplayName);
            Assert.AreEqual("local_ai_model", asset.Kind);
            Assert.AreEqual("local_chat", asset.RequiredFor.Single());
            Assert.AreEqual("YuiVRMAIStudio_LocalGemma_E4B_v20260702.zip", asset.Filename);
            Assert.AreEqual(1234567890L, asset.SizeBytes);
            Assert.AreEqual("YuiVRMAIStudio_LocalGemma_E4B_v20260702.zip.part-000", asset.Parts.Single().Filename);
            Assert.AreEqual("Models/gemma-4-E4B-it.litertlm", asset.InstalledPaths.Single());
        }

        [Test]
        public void AssetManifest_ReturnsOptionalTtsAddonsForPlatform()
        {
            var manifest = YuiLocalAiAssetManifest.FromJson(@"{
  ""schema_version"": 1,
  ""assets"": [
    {
      ""id"": ""desktop-local-ai-minimum"",
      ""kind"": ""desktop_local_ai_minimum"",
      ""platforms"": [""windows""],
      ""optional"": false
    },
    {
      ""id"": ""tts-addon-aivis-macos"",
      ""kind"": ""optional_tts_addon"",
      ""platforms"": [""macos-arm64""],
      ""required_for"": [""backend_tts"", ""aivis""],
      ""optional"": true
    },
    {
      ""id"": ""tts-addon-irodori-linux"",
      ""kind"": ""optional_tts_addon"",
      ""platforms"": [""linux""],
      ""required_for"": [""backend_tts"", ""irodori""],
      ""optional"": true
    }
  ]
}");

            var addons = manifest.OptionalAssetsFor("macos", "optional_tts_addon").ToArray();

            Assert.AreEqual(1, addons.Length);
            Assert.AreEqual("tts-addon-aivis-macos", addons[0].Id);
            Assert.IsFalse(manifest.RequiredAssetsFor("macos").Any(asset => asset.Optional));
        }

        [Test]
        public void AssetInstallProbe_ReportsMissingAndInstalledRequiredPaths()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "yui-local-ai-assets-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var asset = new YuiLocalAiReleaseAsset
                {
                    Id = "desktop-local-gemma-e4b",
                    DisplayName = "Local Gemma SLM",
                    Platforms = new[] { "macos-arm64" },
                    Optional = false,
                    InstallRoot = "YuiLocalAI",
                    InstalledPaths = new[] { "Models/gemma-4-E4B-it.litertlm" }
                };

                var missing = YuiLocalAiAssetInstallProbe.Check(asset, tempRoot, "macos");

                Assert.AreEqual(YuiLocalAiAssetInstallState.Missing, missing.State);
                Assert.IsFalse(missing.Installed);
                Assert.AreEqual(1, missing.MissingPaths.Count);
                StringAssert.Contains("gemma-4-E4B-it.litertlm", missing.Detail);

                var installedPath = Path.Combine(tempRoot, "YuiLocalAI", "Models", "gemma-4-E4B-it.litertlm");
                Directory.CreateDirectory(Path.GetDirectoryName(installedPath));
                File.WriteAllText(installedPath, "model-placeholder");

                var installed = YuiLocalAiAssetInstallProbe.Check(asset, tempRoot, "macos");

                Assert.AreEqual(YuiLocalAiAssetInstallState.Installed, installed.State);
                Assert.IsTrue(installed.Installed);
                Assert.AreEqual(0, installed.MissingPaths.Count);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        [Test]
        public void AssetStore_PlansRequiredDownloadsForMissingOrOutdatedAssets()
        {
            var manifest = YuiLocalAiAssetManifest.FromJson(@"{
  ""schema_version"": 1,
  ""release_version"": ""v0.2.0-beta.2"",
  ""assets"": [
    {
      ""id"": ""desktop-local-gemma-e4b"",
      ""display_name"": ""Local Gemma SLM"",
      ""platforms"": [""macos-arm64""],
      ""required_for"": [""local_chat""],
      ""optional"": false,
      ""version"": ""2026.07.02"",
      ""filename"": ""gemma.zip"",
      ""sha256"": ""abc"",
      ""install_root"": ""YuiLocalAI"",
      ""installed_paths"": [""Models/gemma-4-E4B-it.litertlm""]
    },
    {
      ""id"": ""desktop-local-voicevox-core"",
      ""display_name"": ""Local VOICEVOX"",
      ""platforms"": [""macos-arm64""],
      ""required_for"": [""local_tts""],
      ""optional"": false,
      ""version"": ""2026.07.02"",
      ""filename"": ""voicevox.zip"",
      ""sha256"": ""def"",
      ""install_root"": ""YuiLocalAI/Voicevox"",
      ""installed_paths"": [""Models/meimei_himari_1.vvm""]
    }
  ]
}");
            var installed = new YuiLocalAiInstalledAssetLedger
            {
                Assets =
                {
                    new YuiLocalAiInstalledAssetRecord
                    {
                        Id = "desktop-local-voicevox-core",
                        Version = "2026.07.01",
                        Sha256 = "def"
                    }
                }
            };

            var plan = YuiLocalAiAssetStore.PlanRequiredDownloads(
                manifest,
                installed,
                assetStorageRoot: Path.Combine(Path.GetTempPath(), "missing-yui-assets"),
                platform: "macos");

            Assert.AreEqual(2, plan.AssetsToDownload.Count);
            Assert.AreEqual("desktop-local-gemma-e4b", plan.AssetsToDownload[0].Id);
            Assert.AreEqual(YuiLocalAiAssetNeedReason.MissingFiles, plan.Items[0].NeedReason);
            Assert.AreEqual("desktop-local-voicevox-core", plan.AssetsToDownload[1].Id);
            Assert.AreEqual(YuiLocalAiAssetNeedReason.OutdatedVersion, plan.Items[1].NeedReason);
            Assert.AreEqual(YuiLocalAiAssetPlanState.NeedsDownload, plan.State);
        }

        [Test]
        public void AssetStore_PlansOptionalTtsDownloadsForMissingAssets()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "yui-optional-tts-assets-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manifest = YuiLocalAiAssetManifest.FromJson(@"{
  ""schema_version"": 1,
  ""release_version"": ""v0.2.0-beta.3"",
  ""assets"": [
    {
      ""id"": ""desktop-local-ai-minimum"",
      ""display_name"": ""Required"",
      ""kind"": ""desktop_local_ai_minimum"",
      ""platforms"": [""macos""],
      ""optional"": false,
      ""version"": ""2026.07.02"",
      ""install_root"": ""YuiLocalAI"",
      ""installed_paths"": [""local_ai_model_packs.json""]
    },
    {
      ""id"": ""tts-addon-aivis-macos"",
      ""display_name"": ""AivisSpeech HD add-on"",
      ""kind"": ""optional_tts_addon"",
      ""platforms"": [""macos""],
      ""required_for"": [""backend_tts"", ""aivis""],
      ""optional"": true,
      ""version"": ""2026.07.08"",
      ""filename"": ""aivis.zip"",
      ""sha256"": ""aivis-sha"",
      ""install_root"": ""YuiBackend"",
      ""installed_paths"": [""tools/tts/aivis-engine/extracted/macOS-arm64/run""]
    }
  ]
}");

                var ledger = new YuiLocalAiInstalledAssetLedger();
                var plan = YuiLocalAiAssetStore.PlanOptionalDownloads(
                    manifest,
                    ledger,
                    tempRoot,
                    "macos",
                    "optional_tts_addon");

                Assert.AreEqual(YuiLocalAiAssetPlanState.NeedsDownload, plan.State);
                Assert.AreEqual(1, plan.AssetsToDownload.Count);
                Assert.AreEqual("tts-addon-aivis-macos", plan.AssetsToDownload[0].Id);
                Assert.AreEqual(YuiLocalAiAssetNeedReason.MissingFiles, plan.Items[0].NeedReason);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        [Test]
        public void SettingsUi_ExposesAdditionalTtsDownloadAction()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var runtimeUiPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiSettingsOverlay.RuntimeUi.cs");
            var localAiPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiChatPanel.LocalAI.cs");
            var overlayPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiLocalAiDownloadOverlay.cs");

            var runtimeUi = File.ReadAllText(runtimeUiPath);
            var localAi = File.ReadAllText(localAiPath);
            var overlay = File.ReadAllText(overlayPath);

            StringAssert.Contains("OptionalTtsDownloadButton", runtimeUi);
            StringAssert.Contains("Additional Voices", runtimeUi);
            StringAssert.Contains("RequestOptionalTtsAssetDownload", localAi);
            StringAssert.Contains("ShowOptionalTtsDownload", overlay);
            StringAssert.Contains("optional_tts_addon", overlay);
        }

        [Test]
        public void AssetDownloader_VerifiesSha256ExtractsZipAndRecordsInstall()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "yui-asset-downloader-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempRoot);
                var zipPath = Path.Combine(tempRoot, "gemma.zip");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("Models/gemma-4-E4B-it.litertlm");
                    using var stream = entry.Open();
                    using var writer = new StreamWriter(stream);
                    writer.Write("model-placeholder");
                }

                var zipBytes = File.ReadAllBytes(zipPath);
                var sha256 = Sha256(zipBytes);
                var asset = new YuiLocalAiReleaseAsset
                {
                    Id = "desktop-local-gemma-e4b",
                    DisplayName = "Local Gemma SLM",
                    Version = "2026.07.02",
                    Filename = "gemma.zip",
                    Url = "memory://gemma.zip",
                    Sha256 = sha256,
                    InstallRoot = "YuiLocalAI",
                    InstalledPaths = new[] { "Models/gemma-4-E4B-it.litertlm" },
                    Platforms = new[] { "macos-arm64" }
                };
                var manifest = new YuiLocalAiAssetManifest
                {
                    ReleaseVersion = "v0.2.0-beta.2",
                    Assets = { asset }
                };
                var httpClient = new InMemoryAssetHttpClient(
                    "{\"schema_version\":1,\"assets\":[]}",
                    new Dictionary<string, byte[]> { ["memory://gemma.zip"] = zipBytes });
                var downloader = new YuiLocalAiAssetDownloader(httpClient, tempRoot, tempRoot);

                var result = downloader.InstallAssetsAsync(
                    manifest,
                    new[] { asset },
                    progress: null,
                    CancellationToken.None).GetAwaiter().GetResult();

                Assert.IsTrue(result.Success, result.ErrorMessage);
                Assert.IsTrue(File.Exists(Path.Combine(tempRoot, "YuiLocalAI", "Models", "gemma-4-E4B-it.litertlm")));
                var ledger = YuiLocalAiInstalledAssetLedger.Load(Path.Combine(tempRoot, YuiLocalAiInstalledAssetLedger.DefaultFileName));
                Assert.AreEqual("desktop-local-gemma-e4b", ledger.Assets.Single().Id);
                Assert.AreEqual("2026.07.02", ledger.Assets.Single().Version);
                Assert.AreEqual(sha256, ledger.Assets.Single().Sha256);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        [Test]
        public void AssetDownloader_DownloadsSplitReleasePartsAndRecordsInstall()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "yui-asset-downloader-parts-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempRoot);
                var zipPath = Path.Combine(tempRoot, "local-ai.zip");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("Models/gemma-4-E4B-it.litertlm");
                    using var stream = entry.Open();
                    using var writer = new StreamWriter(stream);
                    writer.Write("model-placeholder-from-parts");
                }

                var zipBytes = File.ReadAllBytes(zipPath);
                var splitIndex = zipBytes.Length / 2;
                var part0 = zipBytes.Take(splitIndex).ToArray();
                var part1 = zipBytes.Skip(splitIndex).ToArray();
                var asset = new YuiLocalAiReleaseAsset
                {
                    Id = "desktop-local-gemma-e4b",
                    DisplayName = "Local Gemma SLM",
                    Version = "2026.07.02",
                    Filename = "local-ai.zip",
                    Sha256 = Sha256(zipBytes),
                    SizeBytes = zipBytes.Length,
                    InstallRoot = "YuiLocalAI",
                    InstalledPaths = new[] { "Models/gemma-4-E4B-it.litertlm" },
                    Platforms = new[] { "macos-arm64" },
                    Parts =
                    {
                        new YuiLocalAiReleaseAssetPart
                        {
                            Filename = "local-ai.zip.part-000",
                            Url = "memory://local-ai.zip.part-000",
                            Sha256 = Sha256(part0),
                            SizeBytes = part0.Length
                        },
                        new YuiLocalAiReleaseAssetPart
                        {
                            Filename = "local-ai.zip.part-001",
                            Url = "memory://local-ai.zip.part-001",
                            Sha256 = Sha256(part1),
                            SizeBytes = part1.Length
                        }
                    }
                };
                var manifest = new YuiLocalAiAssetManifest
                {
                    ReleaseVersion = "v0.2.0-beta.2",
                    Assets = { asset }
                };
                var httpClient = new InMemoryAssetHttpClient(
                    "{\"schema_version\":1,\"assets\":[]}",
                    new Dictionary<string, byte[]>
                    {
                        ["memory://local-ai.zip.part-000"] = part0,
                        ["memory://local-ai.zip.part-001"] = part1
                    });
                var downloader = new YuiLocalAiAssetDownloader(httpClient, tempRoot, tempRoot);

                var result = downloader.InstallAssetsAsync(
                    manifest,
                    new[] { asset },
                    progress: null,
                    CancellationToken.None).GetAwaiter().GetResult();

                Assert.IsTrue(result.Success, result.ErrorMessage);
                Assert.IsTrue(File.Exists(Path.Combine(tempRoot, "YuiLocalAI", "Models", "gemma-4-E4B-it.litertlm")));
                var ledger = YuiLocalAiInstalledAssetLedger.Load(Path.Combine(tempRoot, YuiLocalAiInstalledAssetLedger.DefaultFileName));
                Assert.AreEqual("desktop-local-gemma-e4b", ledger.Assets.Single().Id);
                Assert.AreEqual(asset.Sha256, ledger.Assets.Single().Sha256);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        [Test]
        public void RoutingPolicy_UsesLocalVisionOnlyForLocalConversationMode()
        {
            Assert.IsTrue(YuiLocalAiRoutingPolicy.ShouldPreferLocalVision(YuiConversationModes.LocalAi, true));
            Assert.IsFalse(YuiLocalAiRoutingPolicy.ShouldPreferLocalVision(YuiConversationModes.Stable, true));
            Assert.IsFalse(YuiLocalAiRoutingPolicy.ShouldPreferLocalVision(YuiConversationModes.DirectOpenAi, true));
            Assert.IsFalse(YuiLocalAiRoutingPolicy.ShouldPreferLocalVision(YuiConversationModes.RealtimeVoice, true));
            Assert.IsFalse(YuiLocalAiRoutingPolicy.ShouldPreferLocalVision(YuiConversationModes.LocalAi, false));
        }

        [Test]
        public void RuntimePreferences_LocalConversationUsesLocalTranscriptionWhenAvailable()
        {
            var preferences = YuiLocalAiRuntimePreferencePolicy.For(
                YuiConversationModes.LocalAi,
                ttsMode: "aivis",
                localVisionAvailable: true,
                localTranscriptionAvailable: true);

            Assert.IsTrue(preferences.PreferLocalChat);
            Assert.IsTrue(preferences.PreferLocalTranscription);
            Assert.IsTrue(preferences.PreferLocalVision);
            Assert.IsFalse(preferences.FallbackToBackend);
            Assert.IsFalse(preferences.FallbackToBackendTranscription);
            Assert.IsFalse(preferences.FallbackToBackendVision);
        }

        [Test]
        public void RuntimePreferences_LocalConversationFallsBackForTranscriptionWhenUnavailable()
        {
            var preferences = YuiLocalAiRuntimePreferencePolicy.For(
                YuiConversationModes.LocalAi,
                ttsMode: "aivis",
                localVisionAvailable: true,
                localTranscriptionAvailable: false);

            Assert.IsTrue(preferences.PreferLocalChat);
            Assert.IsFalse(preferences.PreferLocalTranscription);
            Assert.IsTrue(preferences.PreferLocalVision);
            Assert.IsFalse(preferences.FallbackToBackend);
            Assert.IsTrue(preferences.FallbackToBackendTranscription);
            Assert.IsFalse(preferences.FallbackToBackendVision);
        }

        [Test]
        public void RuntimePreferences_ApiConversationDoesNotStealVisionWhenLocalVisionExists()
        {
            var preferences = YuiLocalAiRuntimePreferencePolicy.For(
                YuiConversationModes.Stable,
                ttsMode: "server",
                localVisionAvailable: true);

            Assert.IsFalse(preferences.PreferLocalChat);
            Assert.IsFalse(preferences.PreferLocalTranscription);
            Assert.IsFalse(preferences.PreferLocalVision);
            Assert.IsTrue(preferences.FallbackToBackend);
            Assert.IsTrue(preferences.FallbackToBackendTranscription);
            Assert.IsTrue(preferences.FallbackToBackendVision);
        }

        [Test]
        public void RuntimePreferences_DirectApiConversationDoesNotBecomeLocalAi()
        {
            var preferences = YuiLocalAiRuntimePreferencePolicy.For(
                YuiConversationModes.DirectOpenAi,
                ttsMode: "voicevox-native",
                localVisionAvailable: true);

            Assert.IsFalse(preferences.PreferLocalChat);
            Assert.IsFalse(preferences.PreferLocalTranscription);
            Assert.IsFalse(preferences.PreferLocalVision);
            Assert.IsTrue(preferences.FallbackToBackend);
            Assert.IsTrue(preferences.FallbackToBackendTranscription);
            Assert.IsTrue(preferences.FallbackToBackendVision);
        }

        [Test]
        public void Router_FallsBackToLocalChatWhenBackendIsOfflineAndLocalFallbackIsEnabled()
        {
            var router = new YuiAiRuntimeRouter(
                new YuiLocalAiService(new YuiMockLocalAiRuntime(YuiLocalAiCapability.Chat)),
                (_, __) => throw new YuiPhysicalAI.Api.YuiBackendException(0, "connection refused", string.Empty, "http://127.0.0.1:8000/chat"),
                (_, __, ___, ____) => Task.FromResult(new SttResponse()),
                (_, __, ___, ____, _____) => Task.FromResult(new VisionResponse()))
            {
                FallbackToLocalChat = true
            };

            var response = router.SendChatAsync(
                new ChatRequest { Message = "こんにちは" },
                CancellationToken.None).GetAwaiter().GetResult();

            StringAssert.StartsWith("[local mock]", response.Text);
        }

        [Test]
        public void Router_RetriesBackendOnNextChatAfterLocalFallback()
        {
            var backendAttempts = 0;
            var router = new YuiAiRuntimeRouter(
                new YuiLocalAiService(new YuiMockLocalAiRuntime(YuiLocalAiCapability.Chat)),
                (_, __) =>
                {
                    backendAttempts++;
                    if (backendAttempts == 1)
                    {
                        throw new YuiPhysicalAI.Api.YuiBackendException(0, "connection refused", string.Empty, "http://127.0.0.1:8000/chat");
                    }

                    return Task.FromResult(new ChatResponse { Text = "backend restored" });
                },
                (_, __, ___, ____) => Task.FromResult(new SttResponse()),
                (_, __, ___, ____, _____) => Task.FromResult(new VisionResponse()))
            {
                FallbackToLocalChat = true
            };

            var first = router.SendChatAsync(
                new ChatRequest { Message = "first" },
                CancellationToken.None).GetAwaiter().GetResult();
            var second = router.SendChatAsync(
                new ChatRequest { Message = "second" },
                CancellationToken.None).GetAwaiter().GetResult();

            StringAssert.StartsWith("[local mock]", first.Text);
            Assert.AreEqual("backend restored", second.Text);
            Assert.AreEqual(2, backendAttempts);
        }

        [Test]
        public void RuntimePreferences_LocalConversationDoesNotFallbackToBackendWhenVisionIsUnavailable()
        {
            var preferences = YuiLocalAiRuntimePreferencePolicy.For(
                YuiConversationModes.LocalAi,
                ttsMode: "aivis-native",
                localVisionAvailable: false);

            Assert.IsTrue(preferences.PreferLocalVision);
            Assert.IsFalse(preferences.FallbackToBackendVision);
        }

        [Test]
        public void Router_DoesNotCallBackendWhenLocalTranscriptionIsRequiredButMissing()
        {
            var backendCalled = false;
            var router = new YuiAiRuntimeRouter(
                null,
                (_, __) => Task.FromResult(new ChatResponse()),
                (_, __, ___, ____) =>
                {
                    backendCalled = true;
                    return Task.FromResult(new SttResponse { Text = "backend" });
                },
                (_, __, ___, ____, _____) => Task.FromResult(new VisionResponse()))
            {
                PreferLocalTranscription = true,
                FallbackToBackend = false,
                FallbackToBackendTranscription = false
            };

            Assert.Throws<InvalidOperationException>(
                () => router.TranscribeAsync(new byte[64], "recording.wav", 1000, CancellationToken.None).GetAwaiter().GetResult());
            Assert.IsFalse(backendCalled);
        }

        [Test]
        public void Router_DoesNotCallBackendWhenLocalVisionIsRequiredButMissing()
        {
            var backendCalled = false;
            var router = new YuiAiRuntimeRouter(
                null,
                (_, __) => Task.FromResult(new ChatResponse()),
                (_, __, ___, ____) => Task.FromResult(new SttResponse()),
                (_, __, ___, ____, _____) =>
                {
                    backendCalled = true;
                    return Task.FromResult(new VisionResponse { Summary = "backend" });
                })
            {
                PreferLocalVision = true,
                FallbackToBackendVision = false
            };

            Assert.Throws<InvalidOperationException>(
                () => router.AnalyzeImageAsync(new byte[64], "image.png", "file", "image/png", CancellationToken.None).GetAwaiter().GetResult());
            Assert.IsFalse(backendCalled);
        }

        [Test]
        public void LightweightVisionRuntime_DescribesImageWithoutChatCapability()
        {
            var runtime = new YuiLightweightImageVisionRuntime();
            var texture = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[32];
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(220, 64, 48, 255);
                }
                texture.SetPixels32(pixels);
                texture.Apply();

                var response = runtime.AnalyzeImageAsync(
                    new YuiLocalAiVisionRequest
                    {
                        ImageBytes = texture.EncodeToPNG(),
                        MimeType = "image/png",
                        PromptType = "file"
                    },
                    CancellationToken.None).GetAwaiter().GetResult();

                Assert.IsTrue(response.Success);
                Assert.AreEqual("lightweight-image-vision", response.ModelId);
                StringAssert.Contains("8x4", response.Summary);
                Assert.IsTrue(runtime.Supports(YuiLocalAiCapability.Vision));
                Assert.IsFalse(runtime.Supports(YuiLocalAiCapability.Chat));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void LocalPromptBuilder_AppliesYuiVoiceCustomInstructionAndAssumptionGuard()
        {
            var prepared = YuiLocalAiPromptBuilder.PrepareChatRequest(
                new YuiLocalAiChatRequest
                {
                    Message = "冷房をつけたまま寝たら風邪をひく？",
                    CharacterName = "ゆい",
                    CustomInstruction = "少し妹っぽく、でも説明は正確に。"
                });

            StringAssert.Contains("ゆい", prepared.SystemInstruction);
            StringAssert.Contains("ユーザーの前提を勝手に変えない", prepared.SystemInstruction);
            StringAssert.Contains("短期の欲求よりも体調、安全、明日の負担", prepared.SystemInstruction);
            StringAssert.Contains("確かな部分と不確かな部分", prepared.SystemInstruction);
            StringAssert.Contains("Markdown", prepared.SystemInstruction);
            StringAssert.Contains("少し妹っぽく", prepared.Prompt);
            StringAssert.Contains("冷房をつけたまま寝たら風邪をひく？", prepared.Prompt);
        }

        [Test]
        public void LocalPromptBuilder_DoesNotExposeModelIdentityInCharacterPrompt()
        {
            var prepared = YuiLocalAiPromptBuilder.PrepareChatRequest(
                new YuiLocalAiChatRequest
                {
                    Message = "今使っているモデル名は何？",
                    CharacterName = "Yui"
                });

            StringAssert.DoesNotContain("Gemma", prepared.SystemInstruction);
            StringAssert.DoesNotContain("LiteRT", prepared.SystemInstruction);
            StringAssert.Contains("内緒", prepared.SystemInstruction);
            StringAssert.Contains("AI、モデル名、実装", prepared.SystemInstruction);
        }

        [Test]
        public void LocalPromptBuilder_TellsModelNotToTreatHypotheticalsAsUserExperience()
        {
            var prepared = YuiLocalAiPromptBuilder.PrepareChatRequest(
                new YuiLocalAiChatRequest
                {
                    Message = "仮に電車遅延でタクシーを使ったら会社に請求できるでしょうか。",
                    CharacterName = "Yui"
                });

            StringAssert.Contains("仮定の質問", prepared.SystemInstruction);
            StringAssert.Contains("実際に体験した話として扱わない", prepared.SystemInstruction);
            StringAssert.Contains("体験済み前提の共感", prepared.SystemInstruction);
        }

        [Test]
        public void Service_UsesRuntimeWhenCapabilityIsAvailable()
        {
            var runtime = new YuiMockLocalAiRuntime(YuiLocalAiCapability.Chat);
            var service = new YuiLocalAiService(runtime);

            var response = service.ChatAsync(
                new YuiLocalAiChatRequest { Message = "こんにちは" },
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(response.Success);
            StringAssert.Contains("こんにちは", response.Text);
        }

        [Test]
        public void Service_ReturnsUnavailableWhenCapabilityIsMissing()
        {
            var runtime = new YuiMockLocalAiRuntime(YuiLocalAiCapability.Chat);
            var service = new YuiLocalAiService(runtime);

            var response = service.TranscribeAsync(
                new YuiLocalAiAudioRequest { AudioBytes = new byte[] { 1, 2, 3 }, MimeType = "audio/wav" },
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(response.Success);
            Assert.AreEqual("capability_unavailable", response.ErrorCode);
        }

        [Test]
        public void Service_ReportsSupportedCapabilityForColdRuntimeStatus()
        {
            var service = new YuiLocalAiService(
                new ColdStatusRuntime(YuiLocalAiCapability.Chat));

            Assert.IsTrue(service.Supports(YuiLocalAiCapability.Chat));
            Assert.IsFalse(service.Supports(YuiLocalAiCapability.SpeechSynthesis));
        }

        [Test]
        public void CompositeRuntime_TriesNextRuntimeWhenFirstRuntimeIsUnavailable()
        {
            var runtime = new YuiCompositeLocalAiRuntime(
                new IYuiLocalAiRuntime[]
                {
                    new FailingLocalAiRuntime(YuiLocalAiCapability.Chat, "runtime_unavailable"),
                    new YuiMockLocalAiRuntime(YuiLocalAiCapability.Chat)
                });

            var response = runtime.ChatAsync(
                new YuiLocalAiChatRequest { Message = "こんにちは" },
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(response.Success);
            StringAssert.Contains("こんにちは", response.Text);
            StringAssert.Contains("local mock", response.Text);
        }

        [Test]
        public void CompositeRuntime_TriesLightweightVisionWhenModelFileIsMissing()
        {
            var runtime = new YuiCompositeLocalAiRuntime(
                new IYuiLocalAiRuntime[]
                {
                    new FailingLocalAiRuntime(YuiLocalAiCapability.Vision, "model_file_missing"),
                    new YuiMockLocalAiRuntime(YuiLocalAiCapability.Vision)
                });

            var response = runtime.AnalyzeImageAsync(
                new YuiLocalAiVisionRequest { ImageBytes = new byte[] { 1, 2, 3 } },
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(response.Success);
            Assert.AreEqual("mock-vision", response.ModelId);
        }

        [Test]
        public void Compatibility_MapsLocalChatToBackendChatResponse()
        {
            var local = new YuiLocalAiChatResponse
            {
                Success = true,
                Text = "ローカル応答",
                Face = "happy",
                Animation = "wave",
                VoiceStyle = "normal",
                ShouldTts = true
            };

            ChatResponse backend = YuiLocalAiBackendCompatibility.ToChatResponse(local);

            Assert.AreEqual("ローカル応答", backend.Text);
            Assert.AreEqual("Neutral", backend.Face);
            Assert.AreEqual("wave_small", backend.Animation);
            Assert.AreEqual("normal", backend.VoiceStyle);
            Assert.IsTrue(backend.ShouldTts);
        }

        [Test]
        public void Compatibility_NormalizesEmbeddedJsonAndCatalogValues()
        {
            var local = new YuiLocalAiChatResponse
            {
                Success = true,
                Text = "```json\n{\"text\":\"少し休もう。\",\"face\":\"neutral\",\"animation\":\"wave\",\"voice_style\":\"excited\",\"should_tts\":true}\n```",
                Face = "bad",
                Animation = "bad",
                VoiceStyle = "bad",
                ShouldTts = true
            };

            ChatResponse backend = YuiLocalAiBackendCompatibility.ToChatResponse(local);

            Assert.AreEqual("少し休もう。", backend.Text);
            Assert.AreEqual("Neutral", backend.Face);
            Assert.AreEqual("wave_small", backend.Animation);
            Assert.AreEqual("excited", backend.VoiceStyle);
            Assert.IsTrue(backend.ShouldTts);
        }

        [Test]
        public void Router_UsesLocalChatWhenPreferredAndAvailable()
        {
            var router = new YuiAiRuntimeRouter(
                new YuiLocalAiService(new YuiMockLocalAiRuntime(YuiLocalAiCapability.Chat)),
                (request, token) => Task.FromResult(new ChatResponse { Text = "backend" }),
                (bytes, filename, durationMs, token) => Task.FromResult(new SttResponse { Text = "backend stt" }),
                (bytes, filename, promptType, mimeType, token) => Task.FromResult(new VisionResponse { Summary = "backend vision" }))
            {
                PreferLocal = true
            };

            var response = router.SendChatAsync(
                new ChatRequest { Message = "hello" },
                CancellationToken.None).GetAwaiter().GetResult();

            StringAssert.Contains("hello", response.Text);
            StringAssert.Contains("local mock", response.Text);
        }

        [Test]
        public void Router_FallsBackToBackendWhenLocalCapabilityIsUnavailable()
        {
            var router = new YuiAiRuntimeRouter(
                new YuiLocalAiService(new YuiMockLocalAiRuntime(YuiLocalAiCapability.Chat)),
                (request, token) => Task.FromResult(new ChatResponse { Text = "backend" }),
                (bytes, filename, durationMs, token) => Task.FromResult(new SttResponse { Text = "backend stt" }),
                (bytes, filename, promptType, mimeType, token) => Task.FromResult(new VisionResponse { Summary = "backend vision" }))
            {
                PreferLocal = true
            };

            var response = router.TranscribeAsync(
                new byte[] { 1, 2, 3 },
                "recording.wav",
                100,
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("backend stt", response.Text);
        }

        [Test]
        public void Router_DoesNotFallbackToBackendWhenLocalChatIsExplicitlySelected()
        {
            var backendCalled = false;
            var router = new YuiAiRuntimeRouter(
                new YuiLocalAiService(new YuiMockLocalAiRuntime(YuiLocalAiCapability.Transcription)),
                (request, token) =>
                {
                    backendCalled = true;
                    return Task.FromResult(new ChatResponse { Text = "backend" });
                },
                (bytes, filename, durationMs, token) => Task.FromResult(new SttResponse { Text = "backend stt" }),
                (bytes, filename, promptType, mimeType, token) => Task.FromResult(new VisionResponse { Summary = "backend vision" }))
            {
                PreferLocalChat = true,
                FallbackToBackend = false
            };

            try
            {
                router.SendChatAsync(new ChatRequest { Message = "hello" }, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                Assert.Fail("Expected local chat failure to throw without backend fallback.");
            }
            catch (System.InvalidOperationException)
            {
            }

            Assert.IsFalse(backendCalled);
        }

        [Test]
        public void Router_DoesNotFallbackToBackendWhenLocalTranscriptionIsExplicitlySelected()
        {
            var backendCalled = false;
            var router = new YuiAiRuntimeRouter(
                new YuiLocalAiService(new YuiMockLocalAiRuntime(YuiLocalAiCapability.Chat)),
                (request, token) => Task.FromResult(new ChatResponse { Text = "backend" }),
                (bytes, filename, durationMs, token) =>
                {
                    backendCalled = true;
                    return Task.FromResult(new SttResponse { Text = "backend stt" });
                },
                (bytes, filename, promptType, mimeType, token) => Task.FromResult(new VisionResponse { Summary = "backend vision" }))
            {
                PreferLocalTranscription = true,
                FallbackToBackend = false,
                FallbackToBackendTranscription = false
            };

            try
            {
                router.TranscribeAsync(new byte[] { 1, 2, 3 }, "recording.wav", 100, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                Assert.Fail("Expected local STT failure to throw without backend fallback.");
            }
            catch (System.InvalidOperationException)
            {
            }

            Assert.IsFalse(backendCalled);
        }

        [Test]
        public void Router_FallsBackToBackendSttWhenLocalModeHasNoLocalTranscription()
        {
            var backendCalled = false;
            var router = new YuiAiRuntimeRouter(
                new YuiLocalAiService(new YuiMockLocalAiRuntime(YuiLocalAiCapability.Chat, YuiLocalAiCapability.Vision)),
                (request, token) => Task.FromResult(new ChatResponse { Text = "local chat unavailable" }),
                (bytes, filename, durationMs, token) =>
                {
                    backendCalled = true;
                    return Task.FromResult(new SttResponse { Text = "backend stt" });
                },
                (bytes, filename, promptType, mimeType, token) => Task.FromResult(new VisionResponse { Summary = "local vision unavailable" }))
            {
                PreferLocalChat = true,
                PreferLocalTranscription = false,
                FallbackToBackend = false,
                FallbackToBackendTranscription = true
            };

            var response = router.TranscribeAsync(new byte[] { 1, 2, 3 }, "recording.wav", 100, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.IsTrue(backendCalled);
            Assert.AreEqual("backend stt", response.Text);
        }

        [Test]
        public void Router_UsesVisionFallbackFlagForVisionFailures()
        {
            var backendCalled = false;
            var router = new YuiAiRuntimeRouter(
                new YuiLocalAiService(new FailingLocalAiRuntime(YuiLocalAiCapability.Vision, "runtime_unavailable")),
                (request, token) => Task.FromResult(new ChatResponse { Text = "backend" }),
                (bytes, filename, durationMs, token) => Task.FromResult(new SttResponse { Text = "backend stt" }),
                (bytes, filename, promptType, mimeType, token) =>
                {
                    backendCalled = true;
                    return Task.FromResult(new VisionResponse { Summary = "backend vision" });
                })
            {
                PreferLocalVision = true,
                FallbackToBackend = true,
                FallbackToBackendVision = false
            };

            try
            {
                router.AnalyzeImageAsync(new byte[] { 1, 2, 3 }, "image.jpg", "file", "image/jpeg", CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                Assert.Fail("Expected local vision failure to throw when vision fallback is disabled.");
            }
            catch (System.InvalidOperationException)
            {
            }

            Assert.IsFalse(backendCalled);
        }

        [Test]
        public void Router_CanFallbackVisionEvenWhenChatFallbackIsDisabled()
        {
            var router = new YuiAiRuntimeRouter(
                new YuiLocalAiService(new FailingLocalAiRuntime(YuiLocalAiCapability.Vision, "runtime_unavailable")),
                (request, token) => Task.FromResult(new ChatResponse { Text = "backend" }),
                (bytes, filename, durationMs, token) => Task.FromResult(new SttResponse { Text = "backend stt" }),
                (bytes, filename, promptType, mimeType, token) => Task.FromResult(new VisionResponse { Summary = "backend vision" }))
            {
                PreferLocalVision = true,
                FallbackToBackend = false,
                FallbackToBackendVision = true
            };

            var response = router.AnalyzeImageAsync(
                    new byte[] { 1, 2, 3 },
                    "image.jpg",
                    "file",
                    "image/jpeg",
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual("backend vision", response.Summary);
        }

        [Test]
        public void Router_CanPreferLocalTranscriptionWithoutLocalChat()
        {
            var router = new YuiAiRuntimeRouter(
                new YuiLocalAiService(new YuiMockLocalAiRuntime(YuiLocalAiCapability.Transcription)),
                (request, token) => Task.FromResult(new ChatResponse { Text = "backend chat" }),
                (bytes, filename, durationMs, token) => Task.FromResult(new SttResponse { Text = "backend stt" }),
                (bytes, filename, promptType, mimeType, token) => Task.FromResult(new VisionResponse { Summary = "backend vision" }))
            {
                PreferLocalChat = false,
                PreferLocalTranscription = true,
                PreferLocalVision = false
            };

            var chat = router.SendChatAsync(new ChatRequest { Message = "hello" }, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            var stt = router.TranscribeAsync(new byte[] { 1, 2, 3 }, "recording.wav", 100, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual("backend chat", chat.Text);
            Assert.AreEqual("ローカル音声認識のテスト結果", stt.Text);
        }

        [Test]
        public void ConversationMode_LocalAiIsSelectable()
        {
            Assert.AreEqual(1, YuiConversationModes.DropdownIndex(YuiConversationModes.LocalAi));
            Assert.AreEqual(YuiConversationModes.LocalAi, YuiConversationModes.FromDropdownIndex(1));
            Assert.AreEqual("Local AI ON", YuiConversationModes.StatusLabel("on-device"));
        }

        [Test]
        public void ConversationMode_LocalGemmaDropdownSelectionUsesDesktopGemmaPack()
        {
            var selectedMode = YuiConversationModes.FromDropdownIndex(1);
            var preferences = YuiLocalAiRuntimePreferencePolicy.For(
                selectedMode,
                ttsMode: "server",
                localVisionAvailable: true,
                localTranscriptionAvailable: true);
            var pack = YuiLocalAiModelRegistry.CreateDefaultLocalAi()
                .BestFor(YuiLocalAiCapability.Chat, "macos");

            Assert.AreEqual(YuiConversationModes.LocalAi, selectedMode);
            Assert.IsTrue(preferences.PreferLocalChat);
            Assert.IsFalse(preferences.FallbackToBackend);
            Assert.AreEqual("core_text", pack.Id);
            Assert.AreEqual("gemma-4-E4B-it.litertlm", pack.RuntimeModelRef);
        }

        [Test]
        public void TtsMode_OrdersVoicevoxAivisIrodoriAndSilent()
        {
            var labels = YuiTtsModeOptions.Labels(true, true);

            Assert.AreEqual(YuiTtsModeOptions.BackendVoicevoxLabel, labels[0]);
            Assert.AreEqual(YuiTtsModeOptions.AivisLabel, labels[1]);
            Assert.AreEqual(YuiTtsModeOptions.IrodoriLabel, labels[2]);
            Assert.AreEqual(YuiTtsModeOptions.SilentLabel, labels[3]);
            Assert.AreEqual("server", YuiTtsModeOptions.ModeFromIndex(0, true));
            Assert.AreEqual("aivis", YuiTtsModeOptions.ModeFromIndex(1, true));
            Assert.AreEqual("server-http", YuiTtsModeOptions.ModeFromIndex(2, true));
            Assert.AreEqual("silent", YuiTtsModeOptions.ModeFromIndex(3, true));
        }

        [Test]
        public void TtsMode_LocalAndNativeAivisAreSelectableWhenIncluded()
        {
            var labels = YuiTtsModeOptions.Labels(true, true, true, true);

            Assert.AreEqual(YuiTtsModeOptions.LocalAiVoiceLabel, labels[0]);
            Assert.AreEqual(YuiTtsModeOptions.BackendVoicevoxLabel, labels[1]);
            Assert.AreEqual(YuiTtsModeOptions.AivisLabel, labels[2]);
            Assert.AreEqual(YuiTtsModeOptions.IrodoriLabel, labels[3]);
            Assert.AreEqual(YuiTtsModeOptions.SilentLabel, labels[4]);
            Assert.AreEqual("local-ai", YuiTtsModeOptions.ModeFromIndex(0, true, true, true));
            Assert.AreEqual("server", YuiTtsModeOptions.ModeFromIndex(1, true, true, true));
            Assert.AreEqual("aivis", YuiTtsModeOptions.ModeFromIndex(2, true, true, true));
            Assert.AreEqual("server-http", YuiTtsModeOptions.ModeFromIndex(3, true, true, true));
            Assert.AreEqual("silent", YuiTtsModeOptions.ModeFromIndex(4, true, true, true));
            Assert.AreEqual(2, YuiTtsModeOptions.IndexFromMode("aivis-native", true, true, true));
        }

        [Test]
        public void LocalPromptBuilder_InstructsImageFollowupsToAnswerFromVisionContext()
        {
            var prepared = YuiLocalAiPromptBuilder.PrepareChatRequest(
                new YuiLocalAiChatRequest
                {
                    Message = "これは何が写っているの？",
                    ScreenContext = "画像にはcat、tableが写っている可能性があります。"
                });

            StringAssert.Contains("直前の画像コンテキスト", prepared.Prompt);
            StringAssert.Contains("画像について聞かれたら", prepared.Prompt);
            StringAssert.Contains("最有力の候補", prepared.Prompt);
            StringAssert.Contains("過度にぼかさず", prepared.Prompt);
        }

        [Test]
        public void LocalPromptBuilder_ImageFollowupsAskForConversationalObservation()
        {
            var prepared = YuiLocalAiPromptBuilder.PrepareChatRequest(
                new YuiLocalAiChatRequest
                {
                    Message = "これどう思う？",
                    ScreenContext = "主な候補として、猫、成猫、動物が検出されています。"
                });

            StringAssert.Contains("観察を1つ以上", prepared.Prompt);
            StringAssert.Contains("会話が続く一言", prepared.Prompt);
            StringAssert.Contains("一語回答", prepared.Prompt);
        }

        [Test]
        public void LocalPromptBuilder_ImageFollowupsUseSecondaryCluesWhenTopLabelIsWeak()
        {
            var prepared = YuiLocalAiPromptBuilder.PrepareChatRequest(
                new YuiLocalAiChatRequest
                {
                    Message = "これは何ですか？あなたにわかるかな？",
                    ScreenContext = "主な候補として、テーブルウェア、皿、フォーク、グラス、飲み物、文字が検出されています。"
                });

            StringAssert.Contains("最有力候補が弱い時", prepared.Prompt);
            StringAssert.Contains("確度の高い周辺情報", prepared.Prompt);
            StringAssert.Contains("確認します", prepared.Prompt);
        }

        [Test]
        public void LocalPromptBuilder_InstructsShiritoriFromUnpunctuatedSpeech()
        {
            var prepared = YuiLocalAiPromptBuilder.PrepareChatRequest(
                new YuiLocalAiChatRequest
                {
                    Message = "しりとりしましょう。カムチャッカ半島。"
                });

            StringAssert.Contains("しりとり", prepared.Prompt);
            StringAssert.Contains("最後の文字", prepared.Prompt);
            StringAssert.Contains("一語だけ", prepared.Prompt);
        }

        [Test]
        public void LocalPromptBuilder_AllowsLongerAnswersForContextualTasks()
        {
            var instruction = YuiLocalAiPromptBuilder.BuildCompactSystemInstruction(
                new YuiLocalAiChatRequest { CharacterName = "Yui" });

            StringAssert.Contains("通常は短く", instruction);
            StringAssert.Contains("40〜80字", instruction);
            StringAssert.Contains("100字前後", instruction);
            StringAssert.Contains("回答として必要な情報", instruction);
            StringAssert.Contains("2〜4文", instruction);
            StringAssert.DoesNotContain("1〜2文で", instruction);
        }

        [Test]
        public void LocalPromptBuilder_BrevityMustNotDropNecessaryAnswerParts()
        {
            var prepared = YuiLocalAiPromptBuilder.PrepareChatRequest(
                new YuiLocalAiChatRequest
                {
                    Message = "少し困っているんだけど、どうしたらいいと思う？"
                });

            StringAssert.Contains("短さだけを優先しない", prepared.SystemInstruction);
            StringAssert.Contains("40〜80字", prepared.SystemInstruction);
            StringAssert.Contains("100字前後", prepared.SystemInstruction);
            StringAssert.Contains("回答として必要な情報", prepared.SystemInstruction);
            StringAssert.Contains("受け止め", prepared.SystemInstruction);
            StringAssert.Contains("次の行動", prepared.SystemInstruction);
            StringAssert.Contains("一言だけ", prepared.SystemInstruction);
        }

        [Test]
        public void LocalPromptBuilder_AllowsRoleplayWithoutBreakingSafetyOrAccuracy()
        {
            var prepared = YuiLocalAiPromptBuilder.PrepareChatRequest(
                new YuiLocalAiChatRequest
                {
                    Message = "少し探偵っぽく推理して話して。"
                });

            StringAssert.Contains("ロールプレイ", prepared.SystemInstruction);
            StringAssert.Contains("安全性や正確さ", prepared.SystemInstruction);
            StringAssert.Contains("模範解答だけ", prepared.SystemInstruction);
            StringAssert.Contains("キャラクターらしい反応", prepared.SystemInstruction);
        }

        [Test]
        public void LocalTranscriptNormalizer_RestoresJapaneseTaskBoundaries()
        {
            var normalized = YuiLocalTranscriptNormalizer.Normalize(
                "しりとりしましょうカムチャッカ半島");

            Assert.AreEqual("しりとりしましょう。カムチャッカ半島。", normalized);
        }

        [Test]
        public void LocalTranscriptNormalizer_KeepsExistingPunctuation()
        {
            var normalized = YuiLocalTranscriptNormalizer.Normalize(
                "これは何ですか。ワインですか？");

            Assert.AreEqual("これは何ですか。ワインですか？", normalized);
        }

        private sealed class FailingLocalAiRuntime : IYuiLocalAiRuntime
        {
            private readonly YuiLocalAiCapability capability;
            private readonly string errorCode;

            public FailingLocalAiRuntime(YuiLocalAiCapability capability, string errorCode)
            {
                this.capability = capability;
                this.errorCode = errorCode;
            }

            public string RuntimeName => "failing-local-ai";

            public YuiLocalAiStatus GetStatus()
            {
                return new YuiLocalAiStatus
                {
                    Available = true,
                    RuntimeName = RuntimeName,
                    Detail = "Intentional test failure runtime.",
                    Capabilities = new[] { capability }
                };
            }

            public bool Supports(YuiLocalAiCapability candidate)
            {
                return candidate == capability;
            }

            public Task WarmAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task ReleaseAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task<YuiLocalAiChatResponse> ChatAsync(YuiLocalAiChatRequest request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Fail<YuiLocalAiChatResponse>());
            }

            public Task<YuiLocalAiTranscriptionResponse> TranscribeAsync(YuiLocalAiAudioRequest request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Fail<YuiLocalAiTranscriptionResponse>());
            }

            public Task<YuiLocalAiSpeechResponse> SynthesizeSpeechAsync(YuiLocalAiSpeechRequest request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Fail<YuiLocalAiSpeechResponse>());
            }

            public Task<YuiLocalAiVisionResponse> AnalyzeImageAsync(YuiLocalAiVisionRequest request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Fail<YuiLocalAiVisionResponse>());
            }

            private T Fail<T>()
                where T : YuiLocalAiResponse, new()
            {
                return new T
                {
                    Success = false,
                    ErrorCode = errorCode,
                    ErrorMessage = "Intentional failure."
                };
            }
        }

        private sealed class ColdStatusRuntime : IYuiLocalAiRuntime
        {
            private readonly YuiLocalAiCapability capability;

            public ColdStatusRuntime(YuiLocalAiCapability capability)
            {
                this.capability = capability;
            }

            public string RuntimeName => "cold-status-local-ai";

            public YuiLocalAiStatus GetStatus()
            {
                return new YuiLocalAiStatus
                {
                    Available = false,
                    RuntimeName = RuntimeName,
                    Detail = "Runtime is cold until first use.",
                    Capabilities = new[] { capability }
                };
            }

            public bool Supports(YuiLocalAiCapability candidate)
            {
                return candidate == capability;
            }

            public Task WarmAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task ReleaseAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task<YuiLocalAiChatResponse> ChatAsync(YuiLocalAiChatRequest request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new YuiLocalAiChatResponse { Success = true, Text = "cold runtime warmed" });
            }

            public Task<YuiLocalAiTranscriptionResponse> TranscribeAsync(YuiLocalAiAudioRequest request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new YuiLocalAiTranscriptionResponse());
            }

            public Task<YuiLocalAiSpeechResponse> SynthesizeSpeechAsync(YuiLocalAiSpeechRequest request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new YuiLocalAiSpeechResponse());
            }

            public Task<YuiLocalAiVisionResponse> AnalyzeImageAsync(YuiLocalAiVisionRequest request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new YuiLocalAiVisionResponse());
            }
        }

        private sealed class InMemoryAssetHttpClient : IYuiLocalAiAssetHttpClient
        {
            private readonly string manifestJson;
            private readonly Dictionary<string, byte[]> assets;

            public InMemoryAssetHttpClient(string manifestJson, Dictionary<string, byte[]> assets)
            {
                this.manifestJson = manifestJson;
                this.assets = assets ?? new Dictionary<string, byte[]>();
            }

            public Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
            {
                return Task.FromResult(manifestJson);
            }

            public Task DownloadFileAsync(
                string url,
                string destinationPath,
                long expectedBytes,
                IProgress<YuiLocalAiAssetDownloadProgress> progress,
                CancellationToken cancellationToken)
            {
                if (!assets.TryGetValue(url, out var bytes))
                {
                    throw new FileNotFoundException("Missing in-memory asset: " + url);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                File.WriteAllBytes(destinationPath, bytes);
                progress?.Report(new YuiLocalAiAssetDownloadProgress(url, bytes.Length, expectedBytes, 1f, "download"));
                return Task.CompletedTask;
            }
        }

        private static string Sha256(byte[] bytes)
        {
            return BitConverter.ToString(SHA256.Create().ComputeHash(bytes ?? Array.Empty<byte>()))
                .Replace("-", "")
                .ToLowerInvariant();
        }
    }
}
