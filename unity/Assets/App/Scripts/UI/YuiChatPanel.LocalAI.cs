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
        [Header("Local AI Experimental")]
        [SerializeField] private bool enableLocalAiRuntime;
        [SerializeField] private bool useMockLocalAiRuntimeInEditor;
        [SerializeField] private bool localAiFallbackToBackend = true;

        private YuiAiRuntimeRouter aiRuntimeRouter;
        private YuiLocalAiService localAiService;
        private bool localAiUnavailableWarningShown;

        public void RequestLocalAiAssetRepairDownload()
        {
            EnsureLocalAiDownloadOverlay();
            localAiDownloadOverlay?.ShowRepairDownload();
        }

        public void RefreshLocalAiRuntimeAfterAssetInstall()
        {
            ConfigureAiRuntimeRouter();
            localAiUnavailableWarningShown = false;
            AppendLog("System", "ローカルAIデータの準備が完了しました。Local Gemmaを使用できます。");
            SetStatus("Local AI ready");
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
            var shouldUseLocalRuntime = enableLocalAiRuntime || preferLocalConversation || allowLocalChatFallback || preferLocalSpeech || shouldUseLocalVision;
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
                : ((request, token) => client.SendChatAsync(request, token));
            aiRuntimeRouter = new YuiAiRuntimeRouter(
                localAiService,
                chatEndpoint,
                (wavBytes, filename, durationMs, token) => client.TranscribeAudioAsync(wavBytes, filename, durationMs, token),
                (imageBytes, filename, promptType, mimeType, token) => client.AnalyzeImageAsync(imageBytes, filename, promptType, mimeType, token))
            {
                PreferLocal = false,
                PreferLocalChat = preferences.PreferLocalChat,
                PreferLocalTranscription = preferences.PreferLocalTranscription,
                PreferLocalVision = preferences.PreferLocalVision,
                FallbackToBackend = localAiFallbackToBackend && preferences.FallbackToBackend,
                FallbackToBackendTranscription = localAiFallbackToBackend && preferences.FallbackToBackendTranscription,
                FallbackToBackendVision = preferences.FallbackToBackendVision,
                FallbackToLocalChat = allowLocalChatFallback
            };
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
            if (directOpenAiClient == null
                || !string.Equals(directOpenAiClient.Model, YuiDirectOpenAiClient.NormalizeModel(openAiModel), System.StringComparison.Ordinal)
                || !directOpenAiClient.IsConfigured)
            {
                directOpenAiClient = new YuiDirectOpenAiClient(openAiApiKey, openAiModel);
            }

            return directOpenAiClient.SendChatAsync(request, cancellationToken);
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
