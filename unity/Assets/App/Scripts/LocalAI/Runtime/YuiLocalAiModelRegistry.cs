using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public sealed class YuiLocalAiModelRegistry
    {
        private readonly List<YuiLocalAiModelPack> packs;

        public YuiLocalAiModelRegistry(IEnumerable<YuiLocalAiModelPack> packs)
        {
            this.packs = (packs ?? Array.Empty<YuiLocalAiModelPack>())
                .Where(pack => pack != null && !string.IsNullOrWhiteSpace(pack.Id))
                .OrderBy(pack => pack.Priority)
                .ThenBy(pack => pack.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<YuiLocalAiModelPack> Packs => packs;

        public static YuiLocalAiModelRegistry FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new YuiLocalAiModelRegistry(Array.Empty<YuiLocalAiModelPack>());
            }

            var manifest = JsonConvert.DeserializeObject<YuiLocalAiModelPackManifest>(json);
            return new YuiLocalAiModelRegistry(manifest?.Packs ?? new List<YuiLocalAiModelPack>());
        }

        public static YuiLocalAiModelRegistry FromStreamingAssets(string relativePath = "YuiLocalAI/local_ai_model_packs.json")
        {
            var path = System.IO.Path.Combine(Application.streamingAssetsPath, relativePath);
            if (!System.IO.File.Exists(path))
            {
                return new YuiLocalAiModelRegistry(Array.Empty<YuiLocalAiModelPack>());
            }

            return FromJson(System.IO.File.ReadAllText(path));
        }

        public static YuiLocalAiModelRegistry FromStreamingAssetsOrDefault(string relativePath = "YuiLocalAI/local_ai_model_packs.json")
        {
            var registry = FromStreamingAssets(relativePath);

            return registry.Packs.Count > 0 ? registry : CreateDefaultLocalAi();
        }

        public static YuiLocalAiModelRegistry CreateDefaultLocalAi()
        {
            return new YuiLocalAiModelRegistry(
                new[]
                {
                    new YuiLocalAiModelPack
                    {
                        Id = "core_text",
                        DisplayName = "Gemma 4 E4B LiteRT-LM Text",
                        Provider = "google-litert-lm",
                        ModelId = "litert-community/gemma-4-E4B-it-litert-lm",
                        Format = "litert-lm",
                        RuntimeModelRef = "gemma-4-E4B-it.litertlm",
                        DeploymentKind = YuiLocalAiDeploymentKind.OnDeviceEmbedded,
                        Capabilities = new[]
                        {
                            YuiLocalAiCapability.Chat,
                            YuiLocalAiCapability.Summarization,
                            YuiLocalAiCapability.Translation,
                            YuiLocalAiCapability.Extraction
                        },
                        EnabledByDefault = true,
                        DownloadRequired = true,
                        MemoryBudgetMb = 2600,
                        DiskBudgetMb = 3400,
                        Priority = 8,
                        StartupPolicy = YuiLocalAiStartupPolicy.WarmTextOnly,
                        Platforms = new[] { "macos", "windows" },
                        Notes = "Desktop offline default chat candidate for Mac/Windows. Keep out of the mobile default package because its memory footprint can prevent repeated local TTS/chat turns."
                    },
                    new YuiLocalAiModelPack
                    {
                        Id = "core_text_e2b",
                        DisplayName = "Gemma 4 E2B LiteRT-LM Text",
                        Provider = "google-litert-lm",
                        ModelId = "litert-community/gemma-4-E2B-it-litert-lm",
                        Format = "litert-lm",
                        RuntimeModelRef = "gemma-4-E2B-it.litertlm",
                        DeploymentKind = YuiLocalAiDeploymentKind.OnDeviceEmbedded,
                        Capabilities = new[]
                        {
                            YuiLocalAiCapability.Chat,
                            YuiLocalAiCapability.Summarization,
                            YuiLocalAiCapability.Translation,
                            YuiLocalAiCapability.Extraction
                        },
                        EnabledByDefault = true,
                        DownloadRequired = true,
                        MemoryBudgetMb = 1200,
                        DiskBudgetMb = 2400,
                        Priority = 10,
                        StartupPolicy = YuiLocalAiStartupPolicy.WarmTextOnly,
                        Platforms = new[] { "ios", "android" },
                        Notes = "Mobile offline default. Desktop public beta builds use the higher-quality E4B asset as the bundled minimum SLM."
                    },
                    new YuiLocalAiModelPack
                    {
                        Id = "vision_gemma4_e2b",
                        DisplayName = "Gemma 4 E2B LiteRT-LM Vision",
                        Provider = "google-litert-lm",
                        ModelId = "litert-community/gemma-4-E2B-it-litert-lm",
                        Format = "litert-lm",
                        RuntimeModelRef = "gemma-4-E2B-it.litertlm",
                        DeploymentKind = YuiLocalAiDeploymentKind.OnDeviceEmbedded,
                        Capabilities = new[] { YuiLocalAiCapability.Vision },
                        EnabledByDefault = true,
                        DownloadRequired = true,
                        MemoryBudgetMb = 1500,
                        DiskBudgetMb = 0,
                        Priority = 11,
                        StartupPolicy = YuiLocalAiStartupPolicy.OnDemand,
                        Platforms = new[] { "ios", "android" },
                        Notes = "Reuses the embedded Gemma 4 E2B LiteRT-LM artifact for multimodal image understanding through the official LiteRT-LM image Content API. If a device/model build lacks vision resources, the router falls back to the lightweight descriptor."
                    },
                    new YuiLocalAiModelPack
                    {
                        Id = "core_text_12b_experimental",
                        DisplayName = "Gemma 4 12B LiteRT-LM Text (Experimental)",
                        Provider = "google-litert-lm",
                        ModelId = "litert-community/gemma-4-12B-it-litert-lm",
                        Format = "litert-lm",
                        RuntimeModelRef = "gemma-4-12B-it.litertlm",
                        DeploymentKind = YuiLocalAiDeploymentKind.DesktopAudition,
                        Capabilities = new[]
                        {
                            YuiLocalAiCapability.Chat,
                            YuiLocalAiCapability.Summarization,
                            YuiLocalAiCapability.Translation,
                            YuiLocalAiCapability.Extraction
                        },
                        EnabledByDefault = false,
                        DownloadRequired = true,
                        MemoryBudgetMb = 8000,
                        DiskBudgetMb = 9000,
                        Priority = 18,
                        StartupPolicy = YuiLocalAiStartupPolicy.OnDemand,
                        Platforms = new[] { "macos", "windows" },
                        Notes = "Desktop/high-memory experiment only. Keep off for iPhone 16 / Pixel 8a class mobile defaults until real device benchmarks prove otherwise."
                    },
                    new YuiLocalAiModelPack
                    {
                        Id = "tts_kokoro_sherpa_onnx",
                        DisplayName = "Kokoro ONNX TTS (English Audition Only)",
                        Provider = "kokoro-onnx",
                        ModelId = "onnx-community/Kokoro-82M-v1.0-ONNX",
                        Format = "sherpa-onnx-tts",
                        RuntimeModelRef = "kokoro-v1.0.onnx",
                        DeploymentKind = YuiLocalAiDeploymentKind.DesktopAudition,
                        Capabilities = new[] { YuiLocalAiCapability.SpeechSynthesis },
                        EnabledByDefault = false,
                        DownloadRequired = true,
                        MemoryBudgetMb = 512,
                        DiskBudgetMb = 400,
                        Priority = 22,
                        StartupPolicy = YuiLocalAiStartupPolicy.OnDemand,
                        Platforms = new[] { "macos", "windows" },
                        Notes = "Rejected for Japanese Yui voice by the current kokoro-onnx path: the v1.0 tokenizer is English phonemizer based, so Japanese text can produce broken English-like audio. Keep only as a desktop English audition reference unless a Japanese-capable Kokoro runtime/model is selected."
                    },
                    new YuiLocalAiModelPack
                    {
                        Id = "tts_aivis_desktop_audition",
                        DisplayName = "AivisSpeech TTS (Desktop Audition)",
                        Provider = "aivis-speech",
                        ModelId = "AivisSpeech/local-engine",
                        Format = "voicevox-compatible-http",
                        DeploymentKind = YuiLocalAiDeploymentKind.DesktopAudition,
                        Capabilities = new[] { YuiLocalAiCapability.SpeechSynthesis },
                        EnabledByDefault = false,
                        DownloadRequired = false,
                        MemoryBudgetMb = 2048,
                        DiskBudgetMb = 0,
                        Priority = 24,
                        StartupPolicy = YuiLocalAiStartupPolicy.OnDemand,
                        Platforms = new[] { "macos", "windows" },
                        Notes = "High-quality desktop/Advanced voice audition path. Do not use this as the mobile default unless a real embedded Aivis runtime/model package is selected."
                    },
                });
        }

        public IEnumerable<YuiLocalAiModelPack> EnabledFor(YuiLocalAiCapability capability)
        {
            return EnabledFor(capability, CurrentPlatformKey());
        }

        public IEnumerable<YuiLocalAiModelPack> EnabledFor(YuiLocalAiCapability capability, string platform)
        {
            return packs.Where(pack =>
                pack.EnabledByDefault
                && HasCapability(pack, capability)
                && SupportsPlatform(pack, platform));
        }

        public YuiLocalAiModelPack BestFor(YuiLocalAiCapability capability)
        {
            return EnabledFor(capability).FirstOrDefault();
        }

        public YuiLocalAiModelPack BestFor(YuiLocalAiCapability capability, string platform)
        {
            return EnabledFor(capability, platform).FirstOrDefault();
        }

        public YuiLocalAiModelPack BestFor(YuiLocalAiCapability capability, Func<YuiLocalAiModelPack, bool> predicate)
        {
            predicate ??= (_ => true);
            return EnabledFor(capability).FirstOrDefault(predicate);
        }

        public YuiLocalAiModelPack BestFor(
            YuiLocalAiCapability capability,
            string platform,
            Func<YuiLocalAiModelPack, bool> predicate)
        {
            predicate ??= (_ => true);
            return EnabledFor(capability, platform).FirstOrDefault(predicate);
        }

        public static bool HasCapability(YuiLocalAiModelPack pack, YuiLocalAiCapability capability)
        {
            return pack?.Capabilities != null && pack.Capabilities.Contains(capability);
        }

        public static bool SupportsCurrentPlatform(YuiLocalAiModelPack pack)
        {
            return SupportsPlatform(pack, CurrentPlatformKey());
        }

        public static bool SupportsPlatform(YuiLocalAiModelPack pack, string platform)
        {
            if (pack == null || pack.Platforms == null || pack.Platforms.Length == 0)
            {
                return true;
            }

            var normalizedPlatform = NormalizePlatform(platform);
            return pack.Platforms.Any(value =>
            {
                var normalizedValue = NormalizePlatform(value);
                return string.Equals(normalizedValue, "all", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedValue, normalizedPlatform, StringComparison.OrdinalIgnoreCase);
            });
        }

        public static string CurrentPlatformKey()
        {
#if UNITY_IOS
            return "ios";
#elif UNITY_ANDROID
            return "android";
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return "windows";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return "macos";
#else
            return NormalizePlatform(Application.platform.ToString());
#endif
        }

        private static string NormalizePlatform(string platform)
        {
            if (string.IsNullOrWhiteSpace(platform))
            {
                return "all";
            }

            var value = platform.Trim().Replace("-", "_").ToLowerInvariant();
            switch (value)
            {
                case "osx":
                case "mac":
                case "macosx":
                case "standaloneosx":
                case "standalone_osx":
                    return "macos";
                case "win":
                case "windows":
                case "standalonewindows":
                case "standalone_windows":
                case "standalonewindows64":
                case "standalone_windows_64":
                    return "windows";
                case "iphone":
                case "ios":
                    return "ios";
                case "android":
                    return "android";
                default:
                    return value;
            }
        }
    }
}
