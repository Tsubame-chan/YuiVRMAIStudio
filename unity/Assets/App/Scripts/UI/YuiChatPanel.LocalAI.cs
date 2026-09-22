using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.LocalAI;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        [Header("Local AI Experimental")]
        [SerializeField] private bool enableLocalAiRuntime;
        [SerializeField] private bool useMockLocalAiRuntimeInEditor;
        [SerializeField] private bool localAiFallbackToBackend = true;

        private YuiAiRuntimeRouter aiRuntimeRouter;
        private YuiLocalAiService localAiService;
        private bool localAiUnavailableWarningShown;
        private HealthResponse routingBackendHealth;
        private string routingBackendUrl;
        private float routingBackendCheckedAt = -999f;
        private YuiAiEndpoint selectedChatEndpoint;

        public void RequestLocalAiAssetRepairDownload()
        {
            EnsureLocalAiDownloadOverlay();
            localAiDownloadOverlay?.ShowRepairDownload();
        }

        public void RequestOptionalTtsAssetDownload()
        {
            EnsureLocalAiDownloadOverlay();
            localAiDownloadOverlay?.ShowOptionalTtsDownload();
        }

        public void RefreshLocalAiRuntimeAfterAssetInstall()
        {
            localVoicevoxUnavailable = false;
            ConfigureAiRuntimeRouter();
            localAiUnavailableWarningShown = false;
            if (LocalChatRuntimeAvailable())
            {
                AppendLog("System", "会話の準備ができました。話しかけてください。");
                SetStatus("話しかけてください");
            }
            else
            {
                AppendLog("System", "データの準備は完了しましたが、この環境の端末内Gemmaは利用できません。APIまたは設定済みBackendを選択してください。");
                SetStatus("データ取得完了 · API / Backendを使用してください");
            }
        }

        public void RefreshAfterOptionalTtsAssetInstall()
        {
            AppendLog("System", "追加音声データの準備が完了しました。Backend再起動後にAivisSpeech HDなどの追加TTSを確認できます。");
            SetStatus("Additional voices ready");
            if (cancellationTokenSource != null)
            {
                _ = CheckBackendOnceAsync(cancellationTokenSource.Token);
            }
        }

        private void EnsureLocalAiDownloadOverlay()
        {
            if (localAiDownloadOverlay == null)
            {
                localAiDownloadOverlay = GetComponent<YuiLocalAiDownloadOverlay>();
            }

            if (localAiDownloadOverlay == null)
            {
                localAiDownloadOverlay = gameObject.AddComponent<YuiLocalAiDownloadOverlay>();
            }

            localAiDownloadOverlay.Initialize(this);
        }

        private void ConfigureAiRuntimeRouter()
        {
            IYuiLocalAiRuntime runtime = null;
            var preferLocalConversation = IsLocalAiConversationMode();
            var allowLocalChatFallback = ShouldFallbackToLocalChatForCurrentMode();
            var shouldUseLocalVision = YuiLocalAiRoutingPolicy.RequestsLocalVision(conversationMode);
            var initialPreferences = YuiLocalAiRuntimePreferencePolicy.For(
                conversationMode,
                ttsMode,
                localVisionAvailable: false,
                localTranscriptionAvailable: ShouldUseOnDeviceSpeechForCurrentPlatform());
            var preferLocalSpeech = initialPreferences.PreferLocalTranscription || IsLocalAiTtsMode() || ShouldUseOnDeviceSpeechForCurrentPlatform();
            var shouldUseLocalRuntime = enableLocalAiRuntime || preferLocalConversation || allowLocalChatFallback || preferLocalSpeech || shouldUseLocalVision
                || YuiConversationModes.Normalize(conversationMode) == YuiConversationModes.Stable;
            if (shouldUseLocalRuntime)
            {
#if UNITY_EDITOR
                if (useMockLocalAiRuntimeInEditor)
                {
                    runtime = new YuiMockLocalAiRuntime(
                        YuiLocalAiCapability.Chat,
                        YuiLocalAiCapability.Transcription,
                        YuiLocalAiCapability.SpeechSynthesis,
                        YuiLocalAiCapability.Vision);
                }
                else
#endif
                {
                    runtime = YuiLocalAiRuntimeFactory.Create(YuiLocalAiModelRegistry.FromStreamingAssetsOrDefault());
                }
            }

            localAiService = runtime != null ? new YuiLocalAiService(runtime) : null;
            var localVisionAvailable = false;
            var localTranscriptionAvailable = false;
            if (shouldUseLocalRuntime && localAiService != null)
            {
                var status = localAiService.GetStatus();
                localVisionAvailable = status.Available
                    && runtime.Supports(YuiLocalAiCapability.Vision);
                localTranscriptionAvailable = status.Available
                    && runtime.Supports(YuiLocalAiCapability.Transcription);
                Debug.Log(
                    $"Yui Local AI runtime status: available={status.Available}, runtime={status.RuntimeName}, detail={status.Detail}, capabilities={string.Join(",", status.Capabilities)}");
                if (!status.Available && !localAiUnavailableWarningShown)
                {
                    localAiUnavailableWarningShown = true;
                    if (preferLocalConversation || preferLocalSpeech)
                    {
                        AppendLog("System", "Local AI runtime is not available in this build. Local mode will show an error instead of using backend/API.");
                        SetStatus("Local AI unavailable");
                    }
                    else
                    {
                        AppendLog("System", "Local AI runtime is not available in this build, so requests are falling back to the backend/API path.");
                        SetStatus("Local AI unavailable; backend fallback");
                    }
                }
            }

            var preferences = YuiLocalAiRuntimePreferencePolicy.For(
                conversationMode,
                ttsMode,
                localVisionAvailable,
                localTranscriptionAvailable);
            var chatEndpoint = IsDirectOpenAiConversationMode()
                ? (Func<ChatRequest, CancellationToken, Task<ChatResponse>>)SendDirectOpenAiChatAsync
                : ((request, token) => SendWithPermissionAsync(backendUrl, false, () => client.SendChatAsync(request, token), token));
            aiRuntimeRouter = new YuiAiRuntimeRouter(
                localAiService,
                chatEndpoint,
                (wavBytes, filename, durationMs, token) => SendWithPermissionAsync(backendUrl, false, () => client.TranscribeAudioAsync(wavBytes, filename, durationMs, token), token),
                (imageBytes, filename, promptType, mimeType, token) => SendWithPermissionAsync(backendUrl, false, () => client.AnalyzeImageAsync(imageBytes, filename, promptType, mimeType, token), token))
            {
                PreferLocal = false,
                PreferLocalChat = preferences.PreferLocalChat,
                PreferLocalTranscription = preferences.PreferLocalTranscription,
                PreferLocalVision = preferences.PreferLocalVision,
                FallbackToBackend = localAiFallbackToBackend && preferences.FallbackToBackend,
                FallbackToBackendTranscription = localAiFallbackToBackend && preferences.FallbackToBackendTranscription,
                FallbackToBackendVision = preferences.FallbackToBackendVision,
                FallbackToLocalChat = allowLocalChatFallback,
                FallbackToLocalTranscription = allowLocalChatFallback && localTranscriptionAvailable,
                SelectEndpoint = SelectAiEndpointAsync,
                DirectChat = SendDirectOpenAiChatAsync,
                DirectTranscribe = (bytes, filename, token) => SendWithPermissionAsync(YuiExternalDataConsent.OpenAiDestination, true, () => DirectOpenAiClient().TranscribeAudioAsync(bytes, filename, token), token),
                DirectVision = (bytes, mime, token) => SendWithPermissionAsync(YuiExternalDataConsent.OpenAiDestination, true, () => DirectOpenAiClient().AnalyzeImageAsync(bytes, mime, token), token)
            };
        }

        private async Task<YuiAiEndpoint> SelectAiEndpointAsync(YuiLocalAiCapability capability, CancellationToken token)
        {
            if (YuiConversationModes.Normalize(conversationMode) == YuiConversationModes.Stable
                && (routingBackendUrl != backendUrl || Time.realtimeSinceStartup - routingBackendCheckedAt > 10f))
            {
                routingBackendUrl = backendUrl;
                routingBackendHealth = null;
                using var probe = CancellationTokenSource.CreateLinkedTokenSource(token);
                probe.CancelAfter(TimeSpan.FromSeconds(2));
                try { routingBackendHealth = await client.GetHealthAsync(probe.Token); }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                catch (YuiBackendException) { }
                token.ThrowIfCancellationRequested();
                routingBackendCheckedAt = Time.realtimeSinceStartup;
            }
            var endpoint = YuiAiEndpointPolicy.Resolve(conversationMode, routingBackendHealth,
                !string.IsNullOrWhiteSpace(openAiApiKey), capability);
            if (capability == YuiLocalAiCapability.Chat) selectedChatEndpoint = endpoint;
            return endpoint;
        }

        private bool IsLocalAiConversationMode()
        {
            return string.Equals(
                YuiPhysicalAI.Core.YuiConversationModes.Normalize(conversationMode),
                YuiPhysicalAI.Core.YuiConversationModes.LocalAi,
                System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsDirectOpenAiConversationMode()
        {
            return string.Equals(
                YuiPhysicalAI.Core.YuiConversationModes.Normalize(conversationMode),
                YuiPhysicalAI.Core.YuiConversationModes.DirectOpenAi,
                System.StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldFallbackToLocalChatForCurrentMode()
        {
            return autoAiFallbackEnabled
                && string.Equals(
                YuiPhysicalAI.Core.YuiConversationModes.Normalize(conversationMode),
                YuiPhysicalAI.Core.YuiConversationModes.Stable,
                System.StringComparison.OrdinalIgnoreCase);
        }

        private Task<ChatResponse> SendDirectOpenAiChatAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            return SendWithPermissionAsync(YuiExternalDataConsent.OpenAiDestination, true, () => DirectOpenAiClient().SendChatAsync(request, cancellationToken), cancellationToken);
        }

        private YuiDirectOpenAiClient DirectOpenAiClient()
        {
            if (directOpenAiClient == null
                || !string.Equals(directOpenAiClient.Model, YuiDirectOpenAiClient.NormalizeModel(openAiModel), System.StringComparison.Ordinal)
                || !directOpenAiClient.IsConfigured)
            {
                directOpenAiClient = new YuiDirectOpenAiClient(openAiApiKey, openAiModel);
            }

            return directOpenAiClient;
        }

        private bool IsLocalAiTtsMode()
        {
            return IsTtsMode("local-ai");
        }

        private bool ShouldUseOnDeviceSpeechForCurrentPlatform()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            return IsLocalAiTtsMode();
#else
            return false;
#endif
        }

        private Task<ChatResponse> SendChatViaRuntimeAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            if (aiRuntimeRouter == null)
            {
                ConfigureAiRuntimeRouter();
            }

            return aiRuntimeRouter.SendChatAsync(request, cancellationToken);
        }

        private Task<SttResponse> TranscribeViaRuntimeAsync(
            byte[] wavBytes,
            string filename,
            int? durationMs,
            CancellationToken cancellationToken)
        {
            if (aiRuntimeRouter == null)
            {
                ConfigureAiRuntimeRouter();
            }

            return aiRuntimeRouter.TranscribeAsync(wavBytes, filename, durationMs, cancellationToken);
        }

        private Task<VisionResponse> AnalyzeImageViaRuntimeAsync(
            byte[] imageBytes,
            string filename,
            string promptType,
            string mimeType,
            CancellationToken cancellationToken)
        {
            if (aiRuntimeRouter == null)
            {
                ConfigureAiRuntimeRouter();
            }

            return aiRuntimeRouter.AnalyzeImageAsync(imageBytes, filename, promptType, mimeType, cancellationToken);
        }
    }
}
