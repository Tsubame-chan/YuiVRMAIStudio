using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YuiPhysicalAI.Audio;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private void Update()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (savedDataPanel != null) { Destroy(savedDataPanel); RefreshCharacterSettings(); }
                else if (avatarLibraryPanel != null) { Destroy(avatarLibraryPanel); RefreshCharacterSettings(); }
                else if (composerMenu != null && composerMenu.gameObject.activeSelf) { composerMenu.gameObject.SetActive(false); FocusDesktopComposer(); }
            }
            if(Input.GetKeyDown(KeyCode.Tab)) MoveComposerFocus(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));
            if (!isSending && inputField != null && inputField.isFocused
                && (Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)
                    || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                SendCurrentInput();
#endif
            UpdateAppAwareness();
            PlayNextRealtimeQueuedClip();
            var realtimeMode = IsRealtimeConversationMode();
            var macRealtimeInputActive = realtimeMode
                && macEditorRealtimeMicrophoneStreamer != null
                && macEditorRealtimeMicrophoneStreamer.IsRunning;
            if (!isRecording)
            {
                return;
            }

            if (realtimeMode
                && IsMacEditorRuntime()
                && realtimeStreamActive
                && !realtimeRestarting
                && macEditorRealtimeMicrophoneStreamer != null
                && !macEditorRealtimeMicrophoneStreamer.IsRunning)
            {
                Debug.LogWarning("Yui macOS realtime microphone streamer stopped; restarting input stream.");
                StopMacEditorRealtimeMicrophoneStreamer();
                if (!StartMacEditorRealtimeMicrophoneStreamer())
                {
                    SetStatus("Realtime mic error");
                    AppendLog("System", "Mac EditorのRealtime用マイク入力が停止しました。録音を一度停止して再開してください。");
                    StopRecordingAfterRealtimeError();
                    return;
                }

                macRealtimeInputActive = true;
                ResetRealtimeClientVadState();
                realtimeTranslatePcmBuffer.Clear();
                realtimeNextChunkAt = Time.realtimeSinceStartup + 0.05f;
            }

            if (!macRealtimeInputActive && (unityMicrophoneRecorder == null || !unityMicrophoneRecorder.HasClip))
            {
                return;
            }

            var elapsed = Time.realtimeSinceStartup - recordingStartedAt;
            var recordingLimitSeconds = EffectiveRecordingClipLengthSeconds(realtimeMode);
            if (macRealtimeInputActive)
            {
                UpdateMicrophoneLevel(Mathf.Clamp01(macEditorRealtimeMicrophoneStreamer.LatestLevel * 32f));
            }
            SetStatus(realtimeMode
                ? $"Realtime listening... {FormatElapsedTime(elapsed)}"
                : $"Recording... {Mathf.FloorToInt(elapsed)}/{recordingLimitSeconds}s");
            if ((!realtimeMode && elapsed >= recordingLimitSeconds - 0.05f)
                || (!macRealtimeInputActive && !unityMicrophoneRecorder.IsRecording()))
            {
                Debug.LogWarning($"Recording reached max length or stopped by device. elapsed={elapsed:F1}s, maxSeconds={recordingLimitSeconds}");
                if (elapsed >= recordingLimitSeconds - 0.05f)
                {
                    AppendLog("System", $"入力制限の{recordingLimitSeconds}秒を超過しました。ここまでの音声で送信します。");
                }
                _ = StopRecordingAndSendAsync();
                return;
            }

            var shouldHoldRealtimeMic = IsRealtimeTextTtsMode()
                ? IsRealtimeInputHeldForVoicevox()
                : IsRealtimeTranslateMode()
                    ? realtimeWaitingForResponse
                        || realtimeAssistantTurnActive
                        || (audioSource != null && audioSource.isPlaying)
                    : realtimeWaitingForResponse
                        || realtimeAssistantTurnActive
                        || (audioSource != null && audioSource.isPlaying);
            if (realtimeMode && shouldHoldRealtimeMic)
            {
                ResetRealtimeClientVadState();
                if (macRealtimeInputActive)
                {
                    macEditorRealtimeMicrophoneStreamer.DiscardPendingChunks();
                }
                else
                {
                    realtimeLastSamplePosition = unityMicrophoneRecorder.GetPosition();
                }
                return;
            }

            if (macRealtimeInputActive && realtimeStreamActive && !realtimeRestarting && Time.realtimeSinceStartup >= realtimeNextChunkAt)
            {
                realtimeNextChunkAt = Time.realtimeSinceStartup + 0.12f;
                var chunks = macEditorRealtimeMicrophoneStreamer.DrainChunks();
                foreach (var chunk in chunks)
                {
                    SendRealtimePcm16Chunk(chunk, CalculatePcm16Rms(chunk));
                }
                return;
            }

            var position = unityMicrophoneRecorder.GetPosition();
            if (position <= microphoneSampleBuffer.Length)
            {
                UpdateMicrophoneLevel(0f);
                return;
            }

            var fallbackLevel = macEditorMicrophoneRecorder != null ? macEditorMicrophoneRecorder.LatestLevel : 0f;
            var level = unityMicrophoneRecorder.RecentLevel(microphoneSampleBuffer, fallbackLevel);
            UpdateMicrophoneLevel(Mathf.Clamp01(level));

            if (realtimeMode && realtimeStreamActive && !realtimeRestarting && Time.realtimeSinceStartup >= realtimeNextChunkAt)
            {
                realtimeNextChunkAt = Time.realtimeSinceStartup + 0.12f;
                SendRealtimeMicrophoneDelta(position);
            }
        }

        internal void FocusDesktopComposer()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            if (inputField != null && inputField.interactable)
            {
                inputField.Select();
                inputField.ActivateInputField();
            }
#endif
        }

        private async void Start()
        {
            FocusDesktopComposer();
            MigrateRecentDialogue();
            await RestoreConversationViewAsync();
            _ = CheckLocalAiAssetsOnFirstLaunchAsync();
            if (ShouldMonitorBackend())
            {
                EnsureBackendMonitorIfNeeded();
                await CheckBackendOnceAsync(cancellationTokenSource.Token);
            }
            else if (IsDirectOpenAiConversationMode())
            {
                SetStatus("API ready");
            }
            else
            {
                SetStatus(LocalChatRuntimeAvailable() ? "話しかけてください" : "会話データを準備してください");
            }
        }

        private async Task CheckLocalAiAssetsOnFirstLaunchAsync()
        {
            try
            {
                // Startup is an onboarding check, not a forced update. An installed
                // runtime or app-only API setup must remain usable offline.
                var mode = YuiPhysicalAI.Core.YuiConversationModes.Normalize(conversationMode);
                var canChatWithoutBackend = mode == YuiPhysicalAI.Core.YuiConversationModes.LocalAi
                    ? LocalChatRuntimeAvailable()
                    : mode == YuiPhysicalAI.Core.YuiConversationModes.DirectOpenAi
                        ? !string.IsNullOrWhiteSpace(openAiApiKey)
                        : mode == YuiPhysicalAI.Core.YuiConversationModes.Stable
                            && (!string.IsNullOrWhiteSpace(openAiApiKey) || LocalChatRuntimeAvailable());
                if (canChatWithoutBackend && (NativeVoicevoxAvailable() || IsTtsMode("silent"))) return;
                EnsureLocalAiDownloadOverlay();
                if (localAiDownloadOverlay != null && cancellationTokenSource != null)
                {
                    await localAiDownloadOverlay.CheckAndPromptIfNeededAsync(cancellationTokenSource.Token);
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Debug.LogWarning($"Yui Local AI first-launch asset check failed: {ex.Message}");
            }
        }

        private async Task MonitorBackendAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                if (!ShouldMonitorBackend())
                {
                    continue;
                }

                await CheckBackendOnceAsync(cancellationToken);
            }
        }

        public bool RequiresBackend => ShouldMonitorBackend();
        private bool ShouldMonitorBackend()
        {
            return YuiBackendMonitorPolicy.ShouldMonitorBackend(
                conversationMode,
                ttsMode,
                NativeVoicevoxAvailable());
        }

        private bool IsBackendIndependentRuntimePreferred()
        {
            return !ShouldMonitorBackend();
        }

        private void EnsureBackendMonitorIfNeeded()
        {
            if (ShouldMonitorBackend()) GetComponent<YuiPhysicalAI.Backend.YuiDesktopBackendSupervisor>()?.RequestEnsureBackend();
            if (backendMonitorStarted || cancellationTokenSource == null || !ShouldMonitorBackend())
            {
                return;
            }

            backendMonitorStarted = true;
            _ = MonitorBackendAsync(cancellationTokenSource.Token);
        }

        private async Task CheckBackendOnceAsync(CancellationToken cancellationToken)
        {
            try
            {
                var health = await client.GetHealthAsync(cancellationToken);
                routingBackendHealth = health;
                routingBackendUrl = backendUrl;
                routingBackendCheckedAt = Time.realtimeSinceStartup;
                MarkBackendSuccess();
                if (!isSending)
                {
                    SetStatus(FormatBackendStatus(health));
                }

                if (EnableBackendDiagnosticsLog)
                {
                    LogBackendDiagnostics(health);
                }

                await RefreshBackendConfigAsync(cancellationToken);

            }
            catch (Exception ex)
            {
                if (await TryConfirmBackendReachableAsync(cancellationToken))
                {
                    if (!isSending)
                    {
                        SetStatus("Connected");
                    }
                }
                else if (!isSending && Time.realtimeSinceStartup - lastBackendSuccessAt > 20f)
                {
                    SetStatus("Backend offline");
                }

                Debug.LogWarning($"Backend health check failed: {ex.Message}");
            }
        }

        private async Task RefreshBackendConfigAsync(CancellationToken cancellationToken)
        {
            try
            {
                var config = await client.GetConfigAsync(cancellationToken);
                chatProviderOptions = config?.ChatProviders != null ? config.ChatProviders : Array.Empty<string>();
                visionProviderOptions = config?.VisionProviders != null ? config.VisionProviders : Array.Empty<string>();
                ttsProviderOptions = config?.TtsProviders != null ? config.TtsProviders : Array.Empty<string>();
                backendAivisVoiceOptions = BackendVoiceOptions(config, "aivis");
                sttProviderOptions = config?.SttProviders != null ? config.SttProviders : Array.Empty<string>();
                httpTtsAvailable = config?.TtsProviders != null
                    && config.TtsProviders.Exists(provider => string.Equals(provider, "http", StringComparison.OrdinalIgnoreCase));
                backendConfigLoaded = true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                chatProviderOptions = Array.Empty<string>();
                visionProviderOptions = Array.Empty<string>();
                ttsProviderOptions = Array.Empty<string>();
                backendAivisVoiceOptions = Array.Empty<TtsVoiceOption>();
                sttProviderOptions = Array.Empty<string>();
                httpTtsAvailable = false;
                backendConfigLoaded = false;
                if (EnableBackendDiagnosticsLog)
                {
                    Debug.LogWarning($"Yui backend config refresh failed: {ex.Message}");
                }
            }
        }

        private static IReadOnlyList<TtsVoiceOption> BackendVoiceOptions(ConfigResponse config, string provider)
        {
            if (config?.TtsVoiceOptions == null || string.IsNullOrWhiteSpace(provider))
            {
                return Array.Empty<TtsVoiceOption>();
            }

            foreach (var pair in config.TtsVoiceOptions)
            {
                if (string.Equals(pair.Key, provider, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value ?? (IReadOnlyList<TtsVoiceOption>)Array.Empty<TtsVoiceOption>();
                }
            }

            return Array.Empty<TtsVoiceOption>();
        }

        private async Task<bool> TryConfirmBackendReachableAsync(CancellationToken cancellationToken)
        {
            try
            {
                await client.GetRecentConversationsAsync(userId, 1, cancellationToken);
                MarkBackendSuccess();
                return true;
            }
            catch (Exception confirmEx)
            {
                Debug.LogWarning($"Backend secondary connectivity probe failed: {confirmEx.Message}");
                return false;
            }
        }

        private void MarkBackendSuccess()
        {
            lastBackendSuccessAt = Time.realtimeSinceStartup;
        }

        private void OnDestroy()
        {
            YuiUiLocalization.Changed -= RenderStatus;
            if (savedDataPanel != null) Destroy(savedDataPanel);
            if (composerMenu != null) Destroy(composerMenu.gameObject);
            if (avatarLibraryPanel != null) Destroy(avatarLibraryPanel);
            if (sendButton != null)
            {
                sendButton.onClick.RemoveListener(SendCurrentInput);
            }

            if (recordButton != null)
            {
                recordButton.onClick.RemoveListener(ToggleRecording);
            }

            if (lookButton != null)
            {
                lookButton.onClick.RemoveListener(CaptureScreenAndAnalyze);
            }

            if (importImageButton != null)
            {
                importImageButton.onClick.RemoveListener(ImportImageAndAnalyze);
            }

            if (secretModeButton != null)
            {
                secretModeButton.onClick.RemoveListener(ToggleSecretMode);
            }

            if (talkModeButton != null)
            {
                talkModeButton.onClick.RemoveListener(SelectTalkMode);
            }

            if (workModeButton != null)
            {
                workModeButton.onClick.RemoveListener(SelectWorkMode);
            }

            if (isRecording)
            {
                unityMicrophoneRecorder?.Stop();
            }
            macEditorMicrophoneRecorder?.Dispose();
            macEditorMicrophoneRecorder = null;

            cancellationTokenSource?.Cancel();
            realtimeCancellationTokenSource?.Cancel();
            realtimeVoicevoxSpeechCancellationTokenSource?.Cancel();
            realtimeSocket?.Dispose();
            cancellationTokenSource?.Dispose();
            realtimeCancellationTokenSource?.Dispose();
            realtimeVoicevoxSpeechCancellationTokenSource?.Dispose();
        }


        private void UpdateAppAwareness()
        {
            if (!EnableDormantAppAwarenessPrototype
                || !appAwarenessEnabled
                || appMonitor == null
                || !appMonitor.IsSupported
                || Time.realtimeSinceStartup < nextAppAwarenessPollAt)
            {
                return;
            }

            nextAppAwarenessPollAt = Time.realtimeSinceStartup + Mathf.Max(0.5f, appAwarenessPollInterval);
            var app = appMonitor.GetForegroundApp();
            var nextKey = app.StableKey();
            if (nextKey == currentForegroundAppKey)
            {
                return;
            }

            currentForegroundApp = app;
            currentForegroundAppKey = nextKey;
            appContextStatus = app.IsAvailable ? app.StatusLabel() : "";
            if (app.IsAvailable)
            {
                Debug.Log($"Yui app awareness: category={app.Category}, process={app.ProcessName}, display={app.DisplayName}");
            }

            RenderStatus();
        }

    }
}
