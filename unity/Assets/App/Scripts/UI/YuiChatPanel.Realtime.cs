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
        private async Task SendRealtimeRecordingAsync(byte[] wavBytes)
        {
            SetStatus("Realtime...");
            SetPendingLine(CharacterName, "Realtime接続中...");
            AppendLog("You", "(voice)");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var mode = RealtimeBackendMode();
            var response = await client.SendRealtimeAudioAsync(
                wavBytes,
                mode,
                RealtimeInstructionsForMode(mode),
                "realtime_recording.wav",
                cancellationTokenSource.Token);
            Debug.Log(
                $"Yui realtime audio latency: {timer.ElapsedMilliseconds} ms, events={YuiRealtimeLog.FormatEvents(response.Events, YuiRealtimeLog.VerboseEnabled)}");

            ClearPendingLine();
            if (!string.IsNullOrWhiteSpace(response.Text))
            {
                var responseText = response.Text.Trim();
                AppendLog(CharacterName, responseText);
                if (IsRealtimeTextTtsMode())
                {
                    EnqueueRealtimeVoicevoxSpeech(responseText);
                }
            }

            var clip = YuiBackendClient.Pcm16Base64ToAudioClip(
                response.AudioBase64,
                response.SampleRate,
                "YuiRealtimeAudio");
            if (clip != null && audioSource != null)
            {
                SetStatus("Speaking...");
                var previousClip = audioSource.clip;
                audioSource.Stop();
                audioSource.clip = clip;
                DestroyOwnedAudioClip(previousClip, clip);
                audioSource.Play();
            }

            SetStatus("Ready");
        }

        private async Task SendRealtimeTranslatePhraseAsync(byte[] pcm16, int chunks)
        {
            try
            {
                if (pcm16 == null || pcm16.Length < 2)
                {
                    realtimeWaitingForResponse = false;
                    realtimeAssistantTurnActive = false;
                    return;
                }

                SetStatus("Translating...");
                SetPendingLine(CharacterName, "Translating...");
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var wavBytes = Pcm16BytesToWav(pcm16, 24000);
                var response = await client.SendRealtimeAudioAsync(
                    wavBytes,
                    YuiConversationModes.BackendTranslate,
                    RealtimeInstructionsForMode(YuiConversationModes.BackendTranslate),
                    "realtime_translate_phrase.wav",
                    cancellationTokenSource.Token);
                Debug.Log(
                    $"Yui realtime translate phrase latency: {timer.ElapsedMilliseconds} ms, chunks={chunks}, pcm_bytes={pcm16.Length}, input_text={response.InputText}, events={YuiRealtimeLog.FormatEvents(response.Events, YuiRealtimeLog.VerboseEnabled)}");

                ClearPendingLine();
                if (!string.IsNullOrWhiteSpace(response.Text))
                {
                    AppendLog(CharacterName, response.Text.Trim());
                }

                var clip = YuiBackendClient.Pcm16Base64ToAudioClip(
                    response.AudioBase64,
                    response.SampleRate,
                    "YuiRealtimeTranslateAudio");
                realtimeWaitingForResponse = false;
                realtimeCompletedTurns++;
                if (clip != null && audioSource != null)
                {
                    await PlayRealtimeTranslateClipAsync(clip);
                }
                else
                {
                    realtimeAssistantTurnActive = false;
                    realtimeNextChunkAt = Time.realtimeSinceStartup + 0.05f;
                    SetStatus(isRecording ? "Realtime listening..." : "Ready");
                }
            }
            catch (OperationCanceledException)
            {
                realtimeWaitingForResponse = false;
                realtimeAssistantTurnActive = false;
                ClearPendingLine();
            }
            catch (Exception ex)
            {
                realtimeWaitingForResponse = false;
                realtimeAssistantTurnActive = false;
                ClearPendingLine();
                SetStatus("Realtime error");
                AppendLog("System", ex is YuiBackendException backendException ? backendException.UserMessage : ex.Message);
                Debug.LogError(ex);
            }
        }

        private async Task PlayRealtimeTranslateClipAsync(AudioClip clip)
        {
            if (clip == null || audioSource == null)
            {
                realtimeAssistantTurnActive = false;
                return;
            }

            try
            {
                SetStatus("Speaking...");
                var previousClip = audioSource.clip;
                audioSource.Stop();
                audioSource.loop = false;
                audioSource.clip = clip;
                DestroyOwnedAudioClip(previousClip, clip);
                audioSource.Play();
                while (audioSource != null
                    && audioSource.isPlaying
                    && !cancellationTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(30, cancellationTokenSource.Token);
                }
            }
            finally
            {
                if (macEditorRealtimeMicrophoneStreamer != null)
                {
                    macEditorRealtimeMicrophoneStreamer.DiscardPendingChunks();
                }
                else if (unityMicrophoneRecorder != null && unityMicrophoneRecorder.HasClip)
                {
                    realtimeLastSamplePosition = unityMicrophoneRecorder.GetPosition();
                }

                realtimeTranslatePcmBuffer.Clear();
                ResetRealtimeClientVadState();
                realtimeAssistantTurnActive = false;
                realtimeNextChunkAt = Time.realtimeSinceStartup + 0.05f;
                Debug.Log(
                    "Yui realtime translate playback finished; listening resumed. "
                    + $"mac_streamer_running={macEditorRealtimeMicrophoneStreamer != null && macEditorRealtimeMicrophoneStreamer.IsRunning}");
                SetStatus(isRecording ? "Realtime listening..." : "Ready");
            }
        }

        private async Task StartRealtimeStreamAsync()
        {
            await CloseRealtimeStreamAsync();
            if (IsRealtimeTranslateMode())
            {
                realtimeActiveBackendMode = YuiConversationModes.BackendTranslate;
                realtimeStreamActive = true;
                realtimeAssistantTurnActive = false;
                realtimeWaitingForResponse = false;
                realtimeRestarting = false;
                realtimeCompletedTurns = 0;
                realtimeTranslatePcmBuffer.Clear();
                ResetRealtimeClientVadState();
                realtimeNextChunkAt = Time.realtimeSinceStartup + 0.05f;
                if (IsMacEditorRuntime() && !StartMacEditorRealtimeMicrophoneStreamer())
                {
                    realtimeStreamActive = false;
                    isRecording = false;
                    SetRecordButtonText("Mic");
                    SetInteractable(true);
                    UpdateMicrophoneLevel(0f);
                    AppendLog("System", "Mac EditorのRealtime用マイク入力を開始できませんでした。通常の音声入力を使ってください。");
                    return;
                }

                Debug.Log("Yui realtime translate ready: local VAD + phrase request mode.");
                SetStatus("Realtime listening...");
                return;
            }

            realtimeCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
            realtimeSocket = new ClientWebSocket();
            realtimeTextBuffer.Clear();
            realtimeVoicevoxPendingText.Clear();
            realtimeVoicevoxSpeechCancellationTokenSource?.Cancel();
            realtimeVoicevoxSpeechCancellationTokenSource?.Dispose();
            realtimeVoicevoxSpeechCancellationTokenSource = null;
            lock (realtimeVoicevoxLock)
            {
                realtimeVoicevoxSpeechQueue.Clear();
            }
            realtimeAssistantTurnActive = false;
            realtimeWaitingForResponse = false;
            realtimeVoicevoxSpeechActive = false;
            realtimeRestarting = false;
            realtimeCompletedTurns = 0;
            ResetRealtimeClientVadState();
            lock (realtimeAudioLock)
            {
                realtimeAudioPcmBuffer.Clear();
                realtimeAudioPcmQueue.Clear();
            }
            realtimeTranslatePcmBuffer.Clear();
            realtimeLastSamplePosition = 0;
            realtimeNextChunkAt = Time.realtimeSinceStartup + 0.05f;

            var uri = new Uri(ToWebSocketUrl("/realtime/stream"));
            try
            {
                SetStatus("Realtime connecting...");
                await realtimeSocket.ConnectAsync(uri, realtimeCancellationTokenSource.Token);
                var mode = RealtimeBackendMode();
                realtimeActiveBackendMode = mode;
                await SendRealtimeJsonAsync(new
                {
                    type = "start",
                    mode,
                    user_id = userId,
                    character_name = characterName,
                    instructions = RealtimeInstructionsForMode(mode)
                });
                realtimeStreamActive = true;
                if (!StartMacEditorRealtimeMicrophoneStreamer())
                {
                    realtimeStreamActive = false;
                    isRecording = false;
                    SetRecordButtonText("Mic");
                    SetInteractable(true);
                    UpdateMicrophoneLevel(0f);
                    AppendLog("System", "Mac EditorのRealtime用マイク入力を開始できませんでした。通常の音声入力を使ってください。");
                    await CloseRealtimeStreamAsync();
                    return;
                }
                _ = ReceiveRealtimeLoopAsync(realtimeSocket, realtimeCancellationTokenSource.Token);
                SetStatus("Realtime listening...");
            }
            catch (Exception ex)
            {
                realtimeStreamActive = false;
                realtimeAssistantTurnActive = false;
                SetStatus("Realtime failed");
                AppendLog("System", $"Realtime接続に失敗しました: {ex.Message}");
                Debug.LogError(ex);
            }
        }

        private async Task StopRealtimeStreamAsync()
        {
            realtimeStreamActive = false;
            realtimeAssistantTurnActive = false;
            if (realtimeSocket == null || realtimeSocket.State != WebSocketState.Open)
            {
                await CloseRealtimeStreamAsync();
                SetStatus("Ready");
                return;
            }

            if (realtimeVadGate.SentAudioChunks > 0 && !realtimeWaitingForResponse)
            {
                realtimeWaitingForResponse = true;
                await SendRealtimeJsonAsync(new { type = "stop" });
            }
            else
            {
                await CloseRealtimeStreamAsync();
            }
        }

        private async Task CloseRealtimeStreamAsync()
        {
            realtimeStreamActive = false;
            realtimeAssistantTurnActive = false;
            var socketToClose = realtimeSocket;
            var cancellationToDispose = realtimeCancellationTokenSource;
            realtimeSocket = null;
            realtimeCancellationTokenSource = null;
            try
            {
                if (socketToClose != null && socketToClose.State == WebSocketState.Open)
                {
                    await SendRealtimeJsonAsync(socketToClose, new { type = "close" }, CancellationToken.None);
                    await socketToClose.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "closed",
                        CancellationToken.None);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
            finally
            {
                cancellationToDispose?.Cancel();
                cancellationToDispose?.Dispose();
                StopMacEditorRealtimeMicrophoneStreamer();
                socketToClose?.Dispose();
                realtimeTranslatePcmBuffer.Clear();
                ResetRealtimeClientVadState();
                SyncRealtimeActiveBackendModeWithConversation();
            }
        }

        private void SendRealtimeMicrophoneDelta(int currentPosition)
        {
            if (realtimeWaitingForResponse
                || unityMicrophoneRecorder == null
                || !unityMicrophoneRecorder.HasClip
                || currentPosition == realtimeLastSamplePosition)
            {
                return;
            }

            var sampleCount = currentPosition > realtimeLastSamplePosition
                ? currentPosition - realtimeLastSamplePosition
                : unityMicrophoneRecorder.Clip.samples - realtimeLastSamplePosition + currentPosition;
            if (sampleCount < Mathf.Max(64, activeRecordingFrequency / 20))
            {
                return;
            }

            var data = unityMicrophoneRecorder.ReadSamplesBetween(
                realtimeLastSamplePosition,
                currentPosition,
                out var nextPosition);
            realtimeLastSamplePosition = nextPosition;
            var rms = CalculateRms(data);
            var pcm16 = ConvertToPcm16Mono24k(data, unityMicrophoneRecorder.Clip.channels, activeRecordingFrequency);
            SendRealtimePcm16Chunk(pcm16, rms);
        }

        private void SendRealtimePcm16Chunk(byte[] pcm16, float rms)
        {
            if (pcm16 == null || pcm16.Length == 0)
            {
                return;
            }
            if (realtimeWaitingForResponse || realtimeAssistantTurnActive)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            var vadSettings = YuiRealtimeTuning.ClientVadFor(IsRealtimeTranslateMode());
            realtimeVadGate.Configure(vadSettings);
            var decision = realtimeVadGate.Feed(pcm16, rms, now);
            for (var i = 0; i < decision.ChunksToSend.Count; i++)
            {
                var chunk = decision.ChunksToSend[i];
                SendRealtimeAudioPayload(chunk.Pcm16, rms, chunk.ChunkIndex);
            }

            if (decision.Kind == YuiRealtimeVadDecisionKind.DiscardShortNoise)
            {
                Debug.Log($"Yui realtime client VAD discarded short noise: mode={realtimeActiveBackendMode}, chunks={decision.CommittedChunks}, silence={decision.SilenceSeconds:F2}s");
                if (IsRealtimeTranslateMode())
                {
                    realtimeTranslatePcmBuffer.Clear();
                }
                return;
            }

            if (decision.Kind != YuiRealtimeVadDecisionKind.Commit)
            {
                return;
            }

            if (IsRealtimeTranslateMode())
            {
                realtimeWaitingForResponse = true;
                realtimeAssistantTurnActive = true;
                Debug.Log($"Yui realtime translate VAD commit: chunks={decision.CommittedChunks}, silence={decision.SilenceSeconds:F2}s");
                var phrasePcm = realtimeTranslatePcmBuffer.ToArray();
                realtimeTranslatePcmBuffer.Clear();
                realtimeVadGate.Reset();
                _ = SendRealtimeTranslatePhraseAsync(phrasePcm, decision.CommittedChunks);
                return;
            }

            realtimeWaitingForResponse = true;
            realtimeAssistantTurnActive = true;
            Debug.Log($"Yui realtime client VAD commit: mode={realtimeActiveBackendMode}, chunks={decision.CommittedChunks}, silence={decision.SilenceSeconds:F2}s");
            _ = SendRealtimeJsonAsync(new { type = "stop" });
        }

        private void SendRealtimeAudioPayload(byte[] pcm16, float rms, int chunkIndex)
        {
            if (IsRealtimeTranslateMode())
            {
                realtimeTranslatePcmBuffer.AddRange(pcm16);
                if (chunkIndex == 1 || chunkIndex % 20 == 0)
                {
                    YuiRealtimeLog.Verbose($"Yui realtime translate chunks buffered: {chunkIndex}, bytes={pcm16.Length}, rms={rms:F6}");
                }
                return;
            }

            _ = SendRealtimeJsonAsync(new
            {
                type = "audio",
                audio = Convert.ToBase64String(pcm16)
            });
            if (chunkIndex == 1 || chunkIndex % 20 == 0)
            {
                YuiRealtimeLog.Verbose($"Yui realtime audio chunks sent: {chunkIndex}, bytes={pcm16.Length}, rms={rms:F6}");
            }
        }

        private void ResetRealtimeClientVadState()
        {
            realtimeVadGate.Reset();
        }

        private static float CalculateRms(float[] data)
        {
            if (data == null || data.Length == 0)
            {
                return 0f;
            }

            var sum = 0f;
            for (var i = 0; i < data.Length; i++)
            {
                sum += data[i] * data[i];
            }
            return Mathf.Sqrt(sum / data.Length);
        }

        private static float CalculatePcm16Rms(byte[] pcm16)
        {
            if (pcm16 == null || pcm16.Length < 2)
            {
                return 0f;
            }

            var sum = 0.0;
            var count = 0;
            for (var index = 0; index + 1 < pcm16.Length; index += 2)
            {
                var sample = (short)(pcm16[index] | (pcm16[index + 1] << 8));
                var value = sample / 32768.0;
                sum += value * value;
                count++;
            }

            return count > 0 ? Mathf.Sqrt((float)(sum / count)) : 0f;
        }

        private async Task SendRealtimeJsonAsync(object payload)
        {
            await SendRealtimeJsonAsync(
                realtimeSocket,
                payload,
                realtimeCancellationTokenSource != null
                    ? realtimeCancellationTokenSource.Token
                    : CancellationToken.None);
        }

        private async Task SendRealtimeJsonAsync(ClientWebSocket socket, object payload, CancellationToken cancellationToken)
        {
            if (socket == null || socket.State != WebSocketState.Open)
            {
                return;
            }

            var json = JsonConvert.SerializeObject(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            await realtimeSendLock.WaitAsync();
            try
            {
                if (socket.State != WebSocketState.Open)
                {
                    return;
                }

                await socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
            }
            finally
            {
                realtimeSendLock.Release();
            }
        }

        private async Task ReceiveRealtimeLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[64 * 1024];
            var stream = new MemoryStream();
            try
            {
                while (!cancellationToken.IsCancellationRequested
                    && socket != null
                    && socket.State == WebSocketState.Open)
                {
                    stream.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }
                        stream.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    var json = Encoding.UTF8.GetString(stream.ToArray());
                    HandleRealtimeMessage(JObject.Parse(json));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                SetStatus("Realtime error");
                AppendLog("System", $"Realtime受信に失敗しました: {ex.Message}");
                Debug.LogError(ex);
            }
        }

        private void HandleRealtimeMessage(JObject message)
        {
            var type = message.Value<string>("type");
            if (type == "ready")
            {
                realtimeActiveBackendMode = message.Value<string>("mode") ?? realtimeActiveBackendMode;
                var vad = message["turn_detection"] != null ? message["turn_detection"].ToString(Formatting.None) : "default";
                var readyMessage = $"Yui realtime stream ready: mode={realtimeActiveBackendMode}, voice={message.Value<string>("voice")}, localMode={conversationMode}";
                Debug.Log(YuiRealtimeLog.VerboseEnabled ? $"{readyMessage}, vad={vad}" : readyMessage);
                return;
            }

            if (type == "event")
            {
                var eventName = message.Value<string>("event");
                var inputTranscript = message.Value<string>("transcript");
                if (eventName == "conversation.item.input_audio_transcription.completed"
                    && !string.IsNullOrWhiteSpace(inputTranscript))
                {
                    var trimmedTranscript = inputTranscript.Trim();
                    Debug.Log($"Yui realtime input transcript: {trimmedTranscript}");
                    if (!IsRealtimeTranslateMode())
                    {
                        AppendLog("You", trimmedTranscript);
                    }
                }
                if (eventName == "response.created")
                {
                    realtimeAssistantTurnActive = true;
                    if (IsRealtimeTextTtsMode() || string.Equals(realtimeActiveBackendMode, YuiConversationModes.BackendVoiceText, StringComparison.OrdinalIgnoreCase))
                    {
                        realtimeVoicevoxTurnTimer = System.Diagnostics.Stopwatch.StartNew();
                        realtimeVoicevoxFirstTextMs = -1;
                        realtimeVoicevoxDoneMs = -1;
                        realtimeVoicevoxPendingText.Clear();
                    }
                }
                else if (eventName == "input_audio_buffer.speech_started"
                    && (IsRealtimeTextTtsMode() || string.Equals(realtimeActiveBackendMode, YuiConversationModes.BackendVoiceText, StringComparison.OrdinalIgnoreCase)))
                {
                    ClearRealtimeVoicevoxSpeechQueue();
                }
                else if (eventName == "input_audio_buffer.no_speech")
                {
                    realtimeAssistantTurnActive = false;
                    realtimeWaitingForResponse = false;
                    ResetRealtimeClientVadState();
                    if (isRecording && realtimeStreamActive)
                    {
                        SetStatus("Realtime listening...");
                    }
                }
                return;
            }

            if (type == "text_delta")
            {
                var delta = message.Value<string>("delta") ?? string.Empty;
                realtimeTextBuffer.Append(delta);
                if (IsRealtimeTextTtsMode() || string.Equals(realtimeActiveBackendMode, YuiConversationModes.BackendVoiceText, StringComparison.OrdinalIgnoreCase))
                {
                    if (realtimeVoicevoxTurnTimer != null && realtimeVoicevoxFirstTextMs < 0)
                    {
                        realtimeVoicevoxFirstTextMs = realtimeVoicevoxTurnTimer.ElapsedMilliseconds;
                    }
                }
                return;
            }

            if (type == "audio_delta")
            {
                if (IsRealtimeTextTtsMode() || string.Equals(realtimeActiveBackendMode, YuiConversationModes.BackendVoiceText, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var audio = message.Value<string>("audio");
                if (!string.IsNullOrWhiteSpace(audio))
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(audio);
                        lock (realtimeAudioLock)
                        {
                            realtimeAudioPcmBuffer.AddRange(bytes);
                        }
                    }
                    catch (FormatException ex)
                    {
                        Debug.LogWarning($"Invalid realtime audio chunk: {ex.Message}");
                    }
                }
                return;
            }

            if (type == "done")
            {
                var text = realtimeTextBuffer.ToString().Trim();
                if (realtimeVoicevoxTurnTimer != null)
                {
                    realtimeVoicevoxDoneMs = realtimeVoicevoxTurnTimer.ElapsedMilliseconds;
                }
                if (!string.IsNullOrWhiteSpace(text))
                {
                    AppendLog(CharacterName, text);
                }
                realtimeTextBuffer.Clear();
                realtimeAssistantTurnActive = false;
                realtimeWaitingForResponse = false;
                ResetRealtimeClientVadState();
                lock (realtimeAudioLock)
                {
                    if (IsRealtimeTextTtsMode() || string.Equals(realtimeActiveBackendMode, YuiConversationModes.BackendVoiceText, StringComparison.OrdinalIgnoreCase))
                    {
                        realtimeAudioPcmBuffer.Clear();
                        realtimeAudioPcmQueue.Clear();
                    }
                    else if (realtimeAudioPcmBuffer.Count > 0)
                    {
                        realtimeAudioPcmQueue.Enqueue(realtimeAudioPcmBuffer.ToArray());
                        realtimeAudioPcmBuffer.Clear();
                    }
                }
                if (IsRealtimeTextTtsMode() && !string.IsNullOrWhiteSpace(text))
                {
                    EnqueueRealtimeVoicevoxSpeech(text);
                }
                realtimeCompletedTurns++;
                if (isRecording
                    && realtimeStreamActive
                    && ShouldRefreshRealtimeSessionAfterTurn()
                    && !realtimeRestarting)
                {
                    _ = RestartRealtimeStreamAfterPlaybackAsync();
                }
                if (isRecording && realtimeStreamActive)
                {
                    SetStatus("Realtime listening...");
                }
                else
                {
                    SetStatus("Ready");
                    _ = CloseRealtimeStreamAsync();
                }
                return;
            }

            if (type == "error")
            {
                var messageText = message.Value<string>("message") ?? "Realtime error";
                var code = message.Value<string>("code") ?? TryExtractRealtimeErrorCode(messageText);
                if (string.Equals(code, "input_audio_buffer_commit_empty", StringComparison.OrdinalIgnoreCase)
                    || messageText.Contains("input_audio_buffer_commit_empty", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"Yui realtime ignored empty audio commit: {messageText}");
                    return;
                }
                if (string.Equals(code, "beta_api_shape_disabled", StringComparison.OrdinalIgnoreCase))
                {
                    messageText = RealtimeUnavailableMessage();
                }
                realtimeAssistantTurnActive = false;
                realtimeWaitingForResponse = false;
                ResetRealtimeClientVadState();
                AppendLog("System", messageText);
                SetStatus(string.Equals(code, "beta_api_shape_disabled", StringComparison.OrdinalIgnoreCase) ? "Realtime unavailable" : "Realtime error");
                StopRecordingAfterRealtimeError();
                _ = CloseRealtimeStreamAsync();
            }
        }

        private void StopRecordingAfterRealtimeError()
        {
            isRecording = false;
            unityMicrophoneRecorder?.Stop();
            StopMacEditorMicrophoneFallback();
            recordingClip = null;
            StopRealtimeAudioPlayback();
            UpdateMicrophoneLevel(0f);
            SetRecordButtonText("Mic");
            SetInteractable(true);
        }

        private void StopRealtimeForModeChange()
        {
            isRecording = false;
            unityMicrophoneRecorder?.Stop();
            StopMacEditorMicrophoneFallback();
            recordingClip = null;
            StopRealtimeAudioPlayback();
            realtimeWaitingForResponse = false;
            realtimeAssistantTurnActive = false;
            realtimeStreamActive = false;
            realtimeTranslatePcmBuffer.Clear();
            ResetRealtimeClientVadState();
            UpdateMicrophoneLevel(0f);
            SetRecordButtonText("Mic");
            SetInteractable(true);
            AppendLog("System", "Realtimeモードを変更したため、現在のRealtime録音を停止しました。もう一度Micを押してください。");
            _ = CloseRealtimeStreamAsync();
        }

        private void StopRealtimeAudioPlayback()
        {
            realtimeVoicevoxSpeechCancellationTokenSource?.Cancel();
            realtimeVoicevoxSpeechCancellationTokenSource?.Dispose();
            realtimeVoicevoxSpeechCancellationTokenSource = null;
            realtimeVoicevoxSpeechActive = false;
            lock (realtimeVoicevoxLock)
            {
                realtimeVoicevoxSpeechQueue.Clear();
            }
            lock (realtimeAudioLock)
            {
                realtimeAudioPcmBuffer.Clear();
                realtimeAudioPcmQueue.Clear();
            }
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }
        }

        private static string TryExtractRealtimeErrorCode(string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText) || !messageText.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                return JObject.Parse(messageText).Value<string>("code");
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string RealtimeUnavailableMessage()
        {
            return "Realtime APIの仕様が更新されたため、このRealtimeモードは現在利用できません。通常の音声入力またはテキスト入力を使ってください。";
        }

        private string ToWebSocketUrl(string path)
        {
            var baseUrl = client != null ? client.BaseUrl : backendUrl;
            if (baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "wss://" + baseUrl.Substring("https://".Length).TrimEnd('/') + "/" + path.TrimStart('/');
            }
            if (baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return "ws://" + baseUrl.Substring("http://".Length).TrimEnd('/') + "/" + path.TrimStart('/');
            }
            return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        }

    }
}
