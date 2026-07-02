using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        public YuiCapabilitySnapshot CurrentCapabilitySnapshot()
        {
            var providerStatus = RecentProviderStatus();
            return YuiCapabilityMatrix.FromProviderStatus(
                providerStatus,
                backendReachable: providerStatus != null || IsBackendRecentlyReachable(),
                nativeVoicevoxAvailable: NativeVoicevoxAvailable(),
                localChatAvailable: LocalChatRuntimeAvailable(),
                directOpenAiConfigured: !string.IsNullOrWhiteSpace(openAiApiKey),
                backendIsRemote: IsRemoteBackend());
        }

        public async Task RefreshCapabilitySnapshotAsync(CancellationToken cancellationToken)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                cachedProviderStatus = await client.GetProviderStatusAsync(cancellationToken);
                lastProviderStatusSuccessAt = Time.realtimeSinceStartup;
                MarkBackendSuccess();
                await RefreshBackendConfigAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Keep the previous snapshot while the UI opens; a later refresh can replace it.
            }
            catch (Exception ex)
            {
                cachedProviderStatus = null;
                lastProviderStatusSuccessAt = -999f;
                if (EnableBackendDiagnosticsLog)
                {
                    Debug.LogWarning($"Yui capability snapshot refresh failed: {ex.Message}");
                }
            }
        }

        private ProviderStatusResponse RecentProviderStatus()
        {
            return cachedProviderStatus != null
                && Time.realtimeSinceStartup - lastProviderStatusSuccessAt <= 15f
                    ? cachedProviderStatus
                    : null;
        }

        private bool IsBackendRecentlyReachable()
        {
            return backendConfigLoaded
                || Time.realtimeSinceStartup - lastBackendSuccessAt <= 15f;
        }

        private bool LocalChatRuntimeAvailable()
        {
            if (localAiService != null && localAiService.Supports(YuiLocalAiCapability.Chat))
            {
                return true;
            }

            var registry = YuiLocalAiModelRegistry.FromStreamingAssetsOrDefault();
            return YuiLocalAiRuntimeFactory.HasOnDeviceEmbeddedPack(
                registry,
                YuiLocalAiCapability.Chat);
        }
    }
}
