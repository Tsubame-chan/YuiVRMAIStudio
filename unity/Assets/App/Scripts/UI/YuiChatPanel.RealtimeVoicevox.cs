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
        private void EnqueueRealtimeVoicevoxSpeech(string text)
        {
            var speechText = YuiSpeechTextUtility.CleanSpeechText(text);
            if (string.IsNullOrWhiteSpace(speechText))
            {
                return;
            }

            realtimeVoicevoxPendingText.Clear();
            lock (realtimeVoicevoxLock)
            {
                realtimeVoicevoxSpeechQueue.Enqueue(speechText);
            }

            if (!realtimeVoicevoxSpeechActive)
            {
                _ = ProcessRealtimeVoicevoxQueueAsync();
            }
        }

        private void ClearRealtimeVoicevoxSpeechQueue()
        {
            realtimeVoicevoxPendingText.Clear();
            realtimeVoicevoxGeneration++;
            realtimeVoicevoxSpeechCancellationTokenSource?.Cancel();
            lock (realtimeVoicevoxLock)
            {
                realtimeVoicevoxSpeechQueue.Clear();
            }

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        private async Task ProcessRealtimeVoicevoxQueueAsync()
        {
            if (realtimeVoicevoxSpeechActive)
            {
                return;
            }

            realtimeVoicevoxSpeechActive = true;
            var generation = realtimeVoicevoxGeneration;
            try
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    if (generation != realtimeVoicevoxGeneration)
                    {
                        break;
                    }

                    string chunk;
                    lock (realtimeVoicevoxLock)
                    {
                        if (realtimeVoicevoxSpeechQueue.Count == 0)
                        {
                            break;
                        }
                        chunk = realtimeVoicevoxSpeechQueue.Dequeue();
                    }

                    await SpeakRealtimeVoicevoxChunkAsync(chunk, generation);
                }
            }
            finally
            {
                realtimeVoicevoxSpeechActive = false;
                var hasPendingSpeech = false;
                lock (realtimeVoicevoxLock)
                {
                    hasPendingSpeech = realtimeVoicevoxSpeechQueue.Count > 0;
                }

                if (hasPendingSpeech && !cancellationTokenSource.IsCancellationRequested)
                {
                    _ = ProcessRealtimeVoicevoxQueueAsync();
                }
            }
        }

        private async Task SpeakRealtimeVoicevoxChunkAsync(string text, int generation)
        {
            if (audioSource == null || string.IsNullOrWhiteSpace(text) || IsTtsMode("silent"))
            {
                return;
            }

            var pausedRealtimeInput = false;
            string pausedInputDevice = null;
            var pausedInputFrequency = 0;
            try
            {
                var speechText = YuiSpeechTextUtility.CleanSpeechText(text);
                if (string.IsNullOrWhiteSpace(speechText))
                {
                    return;
                }

                var chunkTimer = System.Diagnostics.Stopwatch.StartNew();
                realtimeVoicevoxSpeechCancellationTokenSource?.Dispose();
                realtimeVoicevoxSpeechCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
                var clip = await SynthesizeSpeechClipAsync(
                    speechText,
                    "normal",
                    "realtime-voicevox-" + Guid.NewGuid().ToString("N"),
                    realtimeVoicevoxSpeechCancellationTokenSource.Token);
                var synthMs = chunkTimer.ElapsedMilliseconds;
                if (clip == null)
                {
                    return;
                }
                if (generation != realtimeVoicevoxGeneration)
                {
                    DestroyOwnedAudioClip(clip, null);
                    return;
                }

                pausedRealtimeInput = TryPauseRealtimeInputForAssistantPlayback(
                    out pausedInputDevice,
                    out pausedInputFrequency);
                YuiPlatformAudioSession.PrepareForAssistantPlayback();
                NormalizeRealtimePlaybackClip(clip, out var sourcePeak, out var appliedGain);
                var previousClip = audioSource.clip;
                audioSource.Stop();
                audioSource.clip = clip;
                DestroyOwnedAudioClip(previousClip, clip);
                SetStatus("Speaking...");
                audioSource.Play();
                Debug.Log(
                    $"Yui realtime VOICEVOX playback start: text_first_ms={realtimeVoicevoxFirstTextMs}, response_done_ms={realtimeVoicevoxDoneMs}, synth_ms={synthMs}, chars={speechText.Length}, source_peak={sourcePeak:F4}, gain={appliedGain:F2}, audio_volume={audioSource.volume:F2}, synthesis_volume={synthesisVolumeScale:F2}");
                while (audioSource != null
                    && audioSource.isPlaying
                    && !cancellationTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(30, cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Realtime VOICEVOX synthesis cancelled before playback.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Realtime VOICEVOX playback failed: {ex.Message}");
            }
            finally
            {
                realtimeVoicevoxSpeechCancellationTokenSource?.Dispose();
                realtimeVoicevoxSpeechCancellationTokenSource = null;
                if (pausedRealtimeInput)
                {
                    ResumeRealtimeInputAfterAssistantPlayback(
                        pausedInputDevice,
                        pausedInputFrequency);
                }
            }
        }

        private bool TryPauseRealtimeInputForAssistantPlayback(
            out string pausedDevice,
            out int pausedFrequency)
        {
            pausedDevice = activeMicrophoneDevice;
            pausedFrequency = activeRecordingFrequency;
            if (!IsRealtimeTextTtsMode()
                || IsMacEditorRuntime()
                || unityMicrophoneRecorder == null
                || !unityMicrophoneRecorder.HasClip)
            {
                return false;
            }

            unityMicrophoneRecorder.Stop();
            recordingClip = null;
            realtimeLastSamplePosition = 0;
            ResetRealtimeClientVadState();
            Debug.Log($"Yui realtime input paused for assistant playback: device={pausedDevice}, frequency={pausedFrequency}");
            return true;
        }

        private void ResumeRealtimeInputAfterAssistantPlayback(string device, int frequency)
        {
            if (!isRecording
                || !realtimeStreamActive
                || !IsRealtimeTextTtsMode()
                || IsMacEditorRuntime()
                || string.IsNullOrEmpty(device))
            {
                return;
            }

            if (unityMicrophoneRecorder == null)
            {
                unityMicrophoneRecorder = new YuiUnityMicrophoneRecorder();
            }

            activeMicrophoneDevice = device;
            activeRecordingFrequency = frequency > 0 ? frequency : ResolveRecordingFrequency(device);
            if (!unityMicrophoneRecorder.Start(activeMicrophoneDevice, activeRecordingFrequency, 10, true))
            {
                AppendLog("System", "Realtime用マイクの再開に失敗しました。録音を一度停止して再開してください。");
                StopRecordingAfterRealtimeError();
                return;
            }

            recordingClip = unityMicrophoneRecorder.Clip;
            realtimeLastSamplePosition = unityMicrophoneRecorder.GetPosition();
            realtimeNextChunkAt = Time.realtimeSinceStartup + 0.12f;
            realtimeWaitingForResponse = false;
            realtimeAssistantTurnActive = false;
            ResetRealtimeClientVadState();
            Debug.Log($"Yui realtime input resumed after assistant playback: device={activeMicrophoneDevice}, frequency={activeRecordingFrequency}, position={realtimeLastSamplePosition}");
            SetStatus("Realtime listening...");
        }

        private async Task RestartRealtimeStreamAfterPlaybackAsync()
        {
            realtimeRestarting = true;
            try
            {
                while (isRecording
                    && realtimeStreamActive
                    && (HasRealtimeQueuedAudio() || HasRealtimeVoicevoxQueuedSpeech() || realtimeVoicevoxSpeechActive || (audioSource != null && audioSource.isPlaying))
                    && !cancellationTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationTokenSource.Token);
                }

                if (!isRecording || cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                Debug.Log($"Yui realtime session refresh: turns={realtimeCompletedTurns}");
                SetStatus("Realtime refreshing...");
                await CloseRealtimeStreamAsync();
                await Task.Delay(150, cancellationTokenSource.Token);
                if (isRecording && !cancellationTokenSource.IsCancellationRequested)
                {
                    await StartRealtimeStreamAsync();
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                realtimeRestarting = false;
            }
        }

        private bool HasRealtimeQueuedAudio()
        {
            lock (realtimeAudioLock)
            {
                return realtimeAudioPcmQueue.Count > 0 || realtimeAudioPcmBuffer.Count > 0;
            }
        }

        private bool HasRealtimeVoicevoxQueuedSpeech()
        {
            lock (realtimeVoicevoxLock)
            {
                return realtimeVoicevoxSpeechQueue.Count > 0 || realtimeVoicevoxPendingText.Length > 0;
            }
        }

        private bool IsRealtimeInputHeldForVoicevox()
        {
            return realtimeWaitingForResponse
                || realtimeAssistantTurnActive
                || realtimeVoicevoxSpeechActive
                || HasRealtimeVoicevoxQueuedSpeech()
                || (audioSource != null && audioSource.isPlaying);
        }

        private bool ShouldRefreshRealtimeSessionAfterTurn()
        {
            if (IsRealtimeTranslateMode())
            {
                return false;
            }

            return YuiRealtimeTuning.SessionResetTurns > 0
                && realtimeCompletedTurns >= YuiRealtimeTuning.SessionResetTurns;
        }

    }
}
