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
using YuiPhysicalAI.LocalAI;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        public void TestMicrophone()
        {
            var device = SelectMicrophoneDevice();
            if (string.IsNullOrEmpty(device))
            {
                SetStatus("Mic: none");
                AppendLog("System", "マイクが見つかりません。WindowsとUnityのマイク設定を確認してください。");
                return;
            }

            var frequencyText = microphoneDeviceSelector.DescribeCaps(device);
            SetMicrophoneDeviceText($"Mic: {device}");
            SetStatus($"Mic OK: {device}");
            Debug.Log($"Yui mic test: device='{device}', caps={frequencyText}");
        }

        private void StartRecording()
        {
            if (IsRealtimeConversationMode())
            {
                StopRealtimeAudioPlayback();
            }
            else
            {
                ReleaseCurrentPlaybackClip();
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsRealtimeConversationMode() && YuiAndroidSpeechRecognizer.IsSupported)
            {
                _ = StartAndroidPlatformSpeechRecognitionAsync();
                return;
            }
#endif

            var device = SelectMicrophoneDevice();
            if (string.IsNullOrEmpty(device))
            {
                AppendLog("System", "マイクが見つかりません。WindowsとUnityのマイク設定を確認してください。");
                return;
            }

            if (IsRealtimeConversationMode() && IsMacEditorRuntime())
            {
                StopMacEditorMicrophoneFallback();
                if (unityMicrophoneRecorder != null && unityMicrophoneRecorder.HasClip)
                {
                    unityMicrophoneRecorder.Stop();
                }

                activeMicrophoneDevice = device;
                activeRecordingFrequency = 24000;
                recordingClip = null;
                SetMicrophoneDeviceText($"Mic: {activeMicrophoneDevice}");
                isRecording = true;
                recordingStartedAt = Time.realtimeSinceStartup;
                SetInteractable(false);
                SetStatus("Realtime listening... 00:00");
                SetRecordButtonText("Stop");
                _ = StartRealtimeStreamAsync();
                return;
            }

            if (!TryStartMicrophone(device))
            {
                foreach (var fallbackDevice in microphoneDeviceSelector.GetDevices())
                {
                    if (fallbackDevice == device)
                    {
                        continue;
                    }

                    if (TryStartMicrophone(fallbackDevice))
                    {
                        break;
                    }
                }
            }

            if (recordingClip == null)
            {
                StopMacEditorMicrophoneFallback();
                AppendLog("System", "マイクを開始できませんでした。Consoleの `Unity microphones:` に出た名前を Preferred Microphone Device に指定してみてください。");
                return;
            }

            StartMacEditorMicrophoneFallback();
            SetMicrophoneDeviceText($"Mic: {activeMicrophoneDevice}");
            isRecording = true;
            recordingStartedAt = Time.realtimeSinceStartup;
            SetInteractable(false);
            SetStatus(IsRealtimeConversationMode() ? "Realtime listening... 00:00" : $"Recording... 0/{EffectiveRecordingClipLengthSeconds(false)}s");
            SetRecordButtonText("Stop");
            if (IsRealtimeConversationMode())
            {
                _ = StartRealtimeStreamAsync();
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private async Task StartAndroidPlatformSpeechRecognitionAsync()
        {
            try
            {
                isSending = true;
                SetInteractable(false);
                SetRecordButtonText("Rec");
                UpdateMicrophoneLevel(0f);
                SetMicrophoneDeviceText("Mic: Android speech");
                SetStatus("Listening...");

                var transcript = await YuiAndroidSpeechRecognizer.TranscribeLiveAsync(
                    "ja-JP",
                    cancellationTokenSource.Token);
                var message = transcript.Text?.Trim();
                if (!transcript.Ok || string.IsNullOrEmpty(message))
                {
                    if (transcript.ErrorCode != "cancelled")
                    {
                        SetStatus("STT failed");
                        AppendLog("System", string.IsNullOrWhiteSpace(transcript.ErrorMessage)
                            ? "Android音声認識で文字起こしできませんでした。"
                            : transcript.ErrorMessage);
                    }
                    return;
                }

                if (IsLikelyBrokenSpeechTranscript(message))
                {
                    Debug.LogWarning($"Yui Android platform STT rejected broken transcript: {message}");
                    SetStatus("STT failed");
                    AppendLog("System", "Android音声認識に失敗しました。もう一度短めにはっきり話してください。");
                    return;
                }

                await SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                SetStatus("Error");
                AppendLog("System", ex.Message);
                Debug.LogError(ex);
            }
            finally
            {
                isSending = false;
                SetInteractable(true);
                UpdateMicrophoneLevel(0f);
            }
        }
#endif

        private bool TryStartMicrophone(string device)
        {
            if (unityMicrophoneRecorder == null)
            {
                unityMicrophoneRecorder = new YuiUnityMicrophoneRecorder();
            }
            activeMicrophoneDevice = device;
            activeRecordingFrequency = ResolveRecordingFrequency(device);
            var realtimeMode = IsRealtimeConversationMode();
            var clipLengthSeconds = EffectiveRecordingClipLengthSeconds(realtimeMode);
            Debug.Log($"Starting microphone device='{activeMicrophoneDevice}', frequency={activeRecordingFrequency}, maxSeconds={clipLengthSeconds}, realtime={realtimeMode}");

            if (unityMicrophoneRecorder.Start(
                    activeMicrophoneDevice,
                    activeRecordingFrequency,
                    clipLengthSeconds,
                    realtimeMode))
            {
                recordingClip = unityMicrophoneRecorder.Clip;
                return true;
            }

            recordingClip = null;
            return false;
        }

        private int EffectiveRecordingClipLengthSeconds(bool realtimeMode)
        {
            if (realtimeMode)
            {
                return 10;
            }

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            return Mathf.Clamp(maxRecordingSeconds, 1, 60);
#else
            return maxRecordingSeconds;
#endif
        }

        private void StartMacEditorMicrophoneFallback()
        {
            StopMacEditorMicrophoneFallback();
            macEditorMicrophoneFallbackRms = 0f;
            macEditorMicrophoneFallbackPeak = 0f;
            if (!YuiMacEditorMicrophoneRecorder.IsSupported)
            {
                return;
            }

            var recorder = new YuiMacEditorMicrophoneRecorder();
            if (recorder.Start(activeRecordingFrequency, maxRecordingSeconds))
            {
                macEditorMicrophoneRecorder = recorder;
                Debug.Log("Yui macOS editor microphone fallback started.");
                return;
            }

            recorder.Dispose();
        }

        private void StopMacEditorMicrophoneFallback()
        {
            macEditorMicrophoneRecorder?.Dispose();
            macEditorMicrophoneRecorder = null;
        }

        private bool StartMacEditorRealtimeMicrophoneStreamer()
        {
            StopMacEditorRealtimeMicrophoneStreamer();
            if (!IsMacEditorRuntime())
            {
                return true;
            }

            if (!YuiMacEditorRealtimeMicrophoneStreamer.IsSupported)
            {
                Debug.LogWarning($"Yui macOS realtime microphone streamer unsupported. platform={Application.platform}");
                return false;
            }

            var streamer = new YuiMacEditorRealtimeMicrophoneStreamer();
            if (streamer.Start(24000))
            {
                macEditorRealtimeMicrophoneStreamer = streamer;
                return true;
            }

            streamer.Dispose();
            Debug.LogWarning("Yui macOS realtime microphone streamer could not start.");
            return false;
        }

        private void StopMacEditorRealtimeMicrophoneStreamer()
        {
            macEditorRealtimeMicrophoneStreamer?.Dispose();
            macEditorRealtimeMicrophoneStreamer = null;
        }

        private static bool IsMacEditorRuntime()
        {
            return Application.platform == RuntimePlatform.OSXEditor;
        }

        private async Task<byte[]> StopMacEditorMicrophoneFallbackAsync()
        {
            var recorder = macEditorMicrophoneRecorder;
            macEditorMicrophoneRecorder = null;
            if (recorder == null)
            {
                return null;
            }

            try
            {
                var bytes = await recorder.StopAsync();
                macEditorMicrophoneFallbackRms = recorder.FinalRms;
                macEditorMicrophoneFallbackPeak = recorder.FinalPeak;
                Debug.Log(
                    $"Yui macOS editor microphone fallback stopped. bytes={bytes?.Length ?? 0}, rms={macEditorMicrophoneFallbackRms:F8}, peak={macEditorMicrophoneFallbackPeak:F8}");
                return bytes;
            }
            finally
            {
                recorder.Dispose();
            }
        }

        private static bool IsSilentRecording(float rms, float peak)
        {
            return rms < 0.0005f && peak < 0.003f;
        }

        private async Task StopRecordingAndSendAsync()
        {
            var stopResult = unityMicrophoneRecorder != null
                ? unityMicrophoneRecorder.Stop()
                : new YuiUnityMicrophoneRecorder.StopResult(null, 0, false);
            recordingClip = stopResult.Clip;
            var samplePosition = stopResult.SamplePosition;
            var macEditorWavBytesTask = StopMacEditorMicrophoneFallbackAsync();
            isRecording = false;
            SetRecordButtonText("Rec");
            UpdateMicrophoneLevel(0f);

            if (IsRealtimeConversationMode())
            {
                try
                {
                    var macEditorWavBytes = await macEditorWavBytesTask;
                    isSending = true;
                    SetInteractable(false);
                    SetStatus("Realtime responding...");
                    var unityStats = YuiUnityMicrophoneRecorder.CalculateAudioStats(recordingClip, samplePosition);
                    Debug.Log(
                        $"Yui realtime stop: chunks={realtimeVadGate.SentAudioChunks}, unitySamples={samplePosition}, unityRms={unityStats.rms:F8}, unityPeak={unityStats.peak:F8}, macBytes={macEditorWavBytes?.Length ?? 0}, macRms={macEditorMicrophoneFallbackRms:F8}, macPeak={macEditorMicrophoneFallbackPeak:F8}");
                    await StopRealtimeStreamAsync();
                }
                catch (Exception ex)
                {
                    SetStatus("Realtime error");
                    AppendLog("System", ex.Message);
                    Debug.LogError(ex);
                }
                finally
                {
                    isSending = false;
                    SetInteractable(true);
                }
                return;
            }

            if (recordingClip == null || samplePosition <= 0)
            {
                _ = await macEditorWavBytesTask;
                SetStatus("Ready");
                SetInteractable(true);
                AppendLog("System", $"音声を録音できませんでした。device={activeMicrophoneDevice}");
                return;
            }

            try
            {
                isSending = true;
                SetInteractable(false);
                SetStatus("Transcribing...");
                var unityStats = YuiUnityMicrophoneRecorder.CalculateAudioStats(recordingClip, samplePosition);
                var wavBytes = WavUtility.FromAudioClip(recordingClip, samplePosition);
                var completedRecordingClip = recordingClip;
                recordingClip = null;
                DestroyOwnedAudioClip(completedRecordingClip, null);
                CollectAivisMobileGarbage();
                var macEditorWavBytes = await macEditorWavBytesTask;
                if (IsSilentRecording(unityStats.rms, unityStats.peak) && macEditorWavBytes != null && macEditorWavBytes.Length > 44)
                {
                    Debug.Log(
                        $"Unity microphone recording was silent in macOS Editor; using AVFoundation fallback. unityRms={unityStats.rms:F8}, unityPeak={unityStats.peak:F8}, fallbackRms={macEditorMicrophoneFallbackRms:F8}, fallbackPeak={macEditorMicrophoneFallbackPeak:F8}");
                    wavBytes = macEditorWavBytes;
                }
                var durationMs = Mathf.RoundToInt(samplePosition * 1000f / activeRecordingFrequency);
                var transcript = await TranscribeViaRuntimeAsync(
                    wavBytes,
                    "ptt_recording.wav",
                    durationMs,
                    cancellationTokenSource.Token);

                var message = transcript.Text?.Trim();
                if (string.IsNullOrEmpty(message))
                {
                    AppendLog("System", "音声を文字起こしできませんでした。");
                    return;
                }

                if (IsLikelyBrokenSpeechTranscript(message))
                {
                    Debug.LogWarning($"Yui local STT rejected broken transcript: {message}");
                    SetStatus("STT failed");
                    AppendLog("System", "ローカル音声認識に失敗しました。もう一度短めにはっきり話すか、音声/STT設定を変更してください。");
                    return;
                }

                await SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                SetStatus("Error");
                var errorMessage = ex is YuiBackendException backendException
                    ? backendException.UserMessage
                    : ex.Message;
                AppendLog("System", errorMessage);
                Debug.LogError(ex);
            }
            finally
            {
                isSending = false;
                SetInteractable(true);
            }
        }

        private static bool IsLikelyBrokenSpeechTranscript(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return true;
            }

            var meaningful = 0;
            var broken = 0;
            foreach (var ch in message)
            {
                if (char.IsWhiteSpace(ch))
                {
                    continue;
                }

                meaningful++;
                if (ch == '?' || ch == '？' || ch == '\uFFFD')
                {
                    broken++;
                }
            }

            if (meaningful == 0)
            {
                return true;
            }

            return meaningful >= 6 && broken >= meaningful * 0.45f;
        }


        private string SelectMicrophoneDevice()
        {
            if (microphoneDeviceSelector == null)
            {
                microphoneDeviceSelector = new YuiMicrophoneDeviceSelector(preferredRecordingFrequency);
            }
            var device = microphoneDeviceSelector.Select(preferredMicrophoneDevice);
            if (string.IsNullOrEmpty(device))
            {
                SetMicrophoneDeviceText("Mic: none");
                return null;
            }

            SetMicrophoneDeviceText($"Mic: {device}");
            return device;
        }

        private int ResolveRecordingFrequency(string device)
        {
            if (microphoneDeviceSelector == null)
            {
                microphoneDeviceSelector = new YuiMicrophoneDeviceSelector(preferredRecordingFrequency);
            }
            return microphoneDeviceSelector.ResolveFrequency(device);
        }

        private static string FormatElapsedTime(float seconds)
        {
            var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private static byte[] ConvertToPcm16Mono24k(float[] source, int channels, int sourceRate)
        {
            if (source == null || source.Length == 0 || channels <= 0 || sourceRate <= 0)
            {
                return Array.Empty<byte>();
            }

            var frameCount = source.Length / channels;
            var outputFrames = Mathf.Max(1, Mathf.RoundToInt(frameCount * 24000f / sourceRate));
            var bytes = new byte[outputFrames * 2];
            for (var i = 0; i < outputFrames; i++)
            {
                var sourceFrame = Mathf.Clamp(Mathf.RoundToInt(i * sourceRate / 24000f), 0, frameCount - 1);
                var sum = 0f;
                for (var channel = 0; channel < channels; channel++)
                {
                    sum += source[sourceFrame * channels + channel];
                }
                var sample = Mathf.Clamp(sum / channels, -1f, 1f);
                var value = (short)(sample * short.MaxValue);
                bytes[i * 2] = (byte)(value & 0xff);
                bytes[i * 2 + 1] = (byte)((value >> 8) & 0xff);
            }
            return bytes;
        }


    }
}
