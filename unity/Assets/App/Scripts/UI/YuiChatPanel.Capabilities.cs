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
        private string providerStatusUrl;

        public System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> VoiceEnvironmentOptions(string selected)
        {
            var localBackendInstalled = false;
#if (UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN) && !UNITY_EDITOR
            var root = YuiPhysicalAI.Backend.YuiDesktopBackendPaths.ResolveBackendRoot(Application.dataPath, Application.persistentDataPath);
            localBackendInstalled = YuiPhysicalAI.Backend.YuiDesktopBackendSupervisor.ShouldAutoStart(backendUrl, true, root);
#endif
            var nativeAivis = false;
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            nativeAivis = YuiAivisNativeBridge.GetStatus()?.RuntimeReady == true;
#endif
            var deviceSpeech = false;
#if UNITY_IOS && !UNITY_EDITOR
            deviceSpeech = true;
#endif
            return YuiVoiceEnvironmentOptions.Build(providerStatusUrl == backendUrl ? cachedProviderStatus : null,
                IsBackendRecentlyReachable(), localBackendInstalled, NativeVoicevoxAvailable(), nativeAivis, deviceSpeech, selected);
        }

        public YuiCapabilitySnapshot CurrentCapabilitySnapshot()
        {
            var providerStatus = RecentProviderStatus();
            if (providerStatus == null && routingBackendHealth != null && routingBackendUrl == backendUrl
                && Time.realtimeSinceStartup - routingBackendCheckedAt <= 15f)
                return YuiCapabilityMatrix.FromHealth(routingBackendHealth, IsBackendRecentlyReachable(),
                    NativeVoicevoxAvailable(), LocalChatRuntimeAvailable(), !string.IsNullOrWhiteSpace(openAiApiKey), IsRemoteBackend());
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
                // Installed desktop services may provide voices even when the LLM is on-device.
                GetComponent<YuiPhysicalAI.Backend.YuiDesktopBackendSupervisor>()?.RequestEnsureBackend();
                var requestedUrl = backendUrl;
                var status = await client.GetProviderStatusAsync(cancellationToken);
                if (requestedUrl != backendUrl) return;
                cachedProviderStatus = status;
                providerStatusUrl = requestedUrl;
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
                lastProviderStatusSuccessAt = -999f;
                lastBackendSuccessAt = -999f;
                routingBackendHealth = null;
                if (EnableBackendDiagnosticsLog)
                {
                    Debug.LogWarning($"Yui capability snapshot refresh failed: {ex.Message}");
                }
            }
        }

        private ProviderStatusResponse RecentProviderStatus()
        {
            return providerStatusUrl == backendUrl && cachedProviderStatus != null
                && Time.realtimeSinceStartup - lastProviderStatusSuccessAt <= 15f
                    ? cachedProviderStatus
                    : null;
        }

        private bool IsBackendRecentlyReachable()
        {
            return Time.realtimeSinceStartup - lastBackendSuccessAt <= 15f;
        }

        public bool HasInstalledLocalChat => LocalChatRuntimeAvailable();
        public bool HasInstalledNativeVoicevox => NativeVoicevoxAvailable();

        private bool LocalChatRuntimeAvailable()
        {
            if (!YuiGoogleAiEdgeBridge.IsSupported) return false;
            var registry = YuiLocalAiModelRegistry.FromStreamingAssetsOrDefault();
            return YuiLocalAiRuntimeFactory.HasOnDeviceEmbeddedPack(
                registry,
                YuiLocalAiCapability.Chat,
                RuntimeAssetAvailableForLocalChat);
        }

        private static bool RuntimeAssetAvailableForLocalChat(YuiLocalAiModelPack pack)
        {
            if (pack == null || !pack.DownloadRequired)
            {
                return true;
            }

            return System.IO.File.Exists(YuiLocalAiModelPathResolver.PersistentModelPath(pack))
                || System.IO.File.Exists(YuiLocalAiModelPathResolver.StreamingAssetsModelPath(pack));
        }
    }
}
