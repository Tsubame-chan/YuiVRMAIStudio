using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiLocalAiRuntimeFactory
    {
        public static IYuiLocalAiRuntime Create(YuiLocalAiModelRegistry registry)
        {
            registry ??= new YuiLocalAiModelRegistry(Array.Empty<YuiLocalAiModelPack>());

            var runtimes = new List<IYuiLocalAiRuntime>();
            if (HasGoogleAiEdgePacks(registry))
            {
                runtimes.Add(new YuiGoogleAiEdgeLocalAiRuntime(registry));
            }

            if (YuiPlatformSpeechBridge.IsSupported)
            {
                runtimes.Add(new YuiPlatformSpeechLocalAiRuntime());
            }

            if (YuiPlatformVisionBridge.IsSupported)
            {
                runtimes.Add(new YuiPlatformVisionLocalAiRuntime());
            }

            if (ShouldIncludeLightweightVisionRuntime())
            {
                runtimes.Add(new YuiLightweightImageVisionRuntime());
            }

            if (runtimes.Count == 0)
            {
                return new YuiCompositeLocalAiRuntime(Array.Empty<IYuiLocalAiRuntime>());
            }

            return runtimes.Count == 1 ? runtimes[0] : new YuiCompositeLocalAiRuntime(runtimes);
        }

        public static bool IsOnDeviceEmbeddedPack(YuiLocalAiModelPack pack)
        {
            return pack != null
                && pack.EnabledByDefault
                && pack.DeploymentKind == YuiLocalAiDeploymentKind.OnDeviceEmbedded
                && YuiLocalAiModelRegistry.SupportsCurrentPlatform(pack);
        }

        public static bool HasOnDeviceEmbeddedPack(
            YuiLocalAiModelRegistry registry,
            YuiLocalAiCapability capability)
        {
            return HasOnDeviceEmbeddedPack(registry, capability, HasRuntimeAsset);
        }

        public static bool HasOnDeviceEmbeddedPack(
            YuiLocalAiModelRegistry registry,
            YuiLocalAiCapability capability,
            Func<YuiLocalAiModelPack, bool> runtimeAssetAvailable)
        {
            registry ??= new YuiLocalAiModelRegistry(Array.Empty<YuiLocalAiModelPack>());
            runtimeAssetAvailable ??= (_ => true);
            return registry.Packs.Any(pack =>
                IsOnDeviceEmbeddedPack(pack)
                && YuiLocalAiModelRegistry.HasCapability(pack, capability)
                && runtimeAssetAvailable(pack));
        }

        private static bool HasRuntimeAsset(YuiLocalAiModelPack pack)
        {
            if (string.IsNullOrWhiteSpace(pack?.RuntimeModelRef))
            {
                return true;
            }

            var modelFileName = YuiLocalAiModelPathResolver.ModelFileName(pack);
            var persistentPath = YuiLocalAiModelPathResolver.PersistentModelPath(pack);
            if (!string.IsNullOrWhiteSpace(modelFileName) && File.Exists(persistentPath))
            {
                return true;
            }

            var runtimeRef = pack.RuntimeModelRef.Trim().Replace('\\', '/');
            var streamingPath = runtimeRef.Contains("/", StringComparison.Ordinal)
                ? Path.Combine(Application.streamingAssetsPath, "YuiLocalAI", runtimeRef)
                : YuiLocalAiModelPathResolver.StreamingAssetsModelPath(pack);
            if (streamingPath.Contains("://", StringComparison.Ordinal))
            {
                return true;
            }

            return File.Exists(streamingPath) || Directory.Exists(streamingPath);
        }

        private static bool HasGoogleAiEdgePacks(YuiLocalAiModelRegistry registry)
        {
            return registry.Packs.Any(pack =>
                IsOnDeviceEmbeddedPack(pack)
                && (string.Equals(pack.Provider, "google", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pack.Provider, "google-litert-lm", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pack.Format, "mobile-transformers", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pack.Format, "litert", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pack.Format, "litert-lm", StringComparison.OrdinalIgnoreCase)));
        }

        private static bool ShouldIncludeLightweightVisionRuntime()
        {
#if UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }
}
