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
        public async void PreviewVoice()
        {
            await PreviewVoiceAsync(null);
        }

        public async void PreviewVoice(Action onFinished)
        {
            await PreviewVoiceAsync(onFinished);
        }

        public async Task PreviewVoiceAsync(Action onFinished = null)
        {
            if (audioSource == null)
            {
                SetStatus("Voice unavailable");
                onFinished?.Invoke();
                return;
            }

            if (IsTtsMode("silent"))
            {
                SetStatus("TTS is silent");
                onFinished?.Invoke();
                return;
            }

            try
            {
                SetStatus("Previewing voice...");
                var previewText = IsHttpTtsMode()
                    ? "こんにちは、ユイです。"
                    : "こんにちは、ユイです。声の設定はこんな感じです。";
                var clip = await SynthesizeSpeechClipAsync(
                    previewText,
                    "normal",
                    "voice-preview-" + Guid.NewGuid().ToString("N"),
                    cancellationTokenSource.Token);
                if (clip == null)
                {
                    SetStatus("Preview failed");
                    return;
                }

                var previousClip = audioSource.clip;
                audioSource.Stop();
                audioSource.clip = clip;
                DestroyOwnedAudioClip(previousClip, clip);
                audioSource.Play();
                SetStatus("Voice preview");
                while (audioSource != null && audioSource.isPlaying && !cancellationTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(30, cancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                SetStatus("Preview failed");
                var errorMessage = ex is YuiBackendException backendException
                    ? backendException.UserMessage
                    : ex.Message;
                AppendLog("System", errorMessage);
                Debug.LogError(ex);
            }
            finally
            {
                onFinished?.Invoke();
            }
        }

        private async Task SpeakResponseAsync(
            ChatResponse chat,
            string chatRequestId,
            CancellationToken cancellationToken,
            bool allowChunking = true)
        {
            if (audioSource == null)
            {
                return;
            }

            if (IsTtsMode("silent"))
            {
                Debug.Log("Yui TTS skipped: silent mode");
                return;
            }

            var shouldSpeak = chat.ShouldTts
                || (forceTtsForNonEmptyReplies && !string.IsNullOrWhiteSpace(chat.Text));
            Debug.Log(
                $"Yui TTS decision: should_tts={chat.ShouldTts}, force_non_empty={forceTtsForNonEmptyReplies}, should_speak={shouldSpeak}, text_length={(chat.Text ?? string.Empty).Length}");

            if (!shouldSpeak)
            {
                return;
            }

            var speechText = YuiSpeechTextUtility.CleanSpeechText(chat.Text);
            if (string.IsNullOrWhiteSpace(speechText))
            {
                return;
            }

            SetStatus("Speaking...");
            audioSource.Stop();

            var chunks = allowChunking
                ? SplitSpeechTextForCurrentTts(speechText)
                : new[] { speechText };
            Debug.Log($"Yui TTS chunks: {chunks.Length}");

            if (IsHttpTtsMode() && chunks.Length > 1)
            {
                await SpeakResponseWithPrefetchAsync(chunks, chat.VoiceStyle, chatRequestId, cancellationToken);
                return;
            }

            for (var index = 0; index < chunks.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunkTimer = System.Diagnostics.Stopwatch.StartNew();
                var clip = await SynthesizeSpeechClipAsync(
                    chunks[index],
                    chat.VoiceStyle,
                    $"{chatRequestId}-tts-{index}",
                    cancellationToken);
                Debug.Log($"Yui TTS chunk {index + 1}/{chunks.Length} latency: {chunkTimer.ElapsedMilliseconds} ms, chars={chunks[index].Length}");
                if (clip == null)
                {
                    continue;
                }

                while (audioSource.isPlaying && !cancellationToken.IsCancellationRequested)
                {
                    // Yielding every frame burns CPU. 30 ms is well below typical
                    // VOICEVOX chunk boundaries and stays imperceptible.
                    await Task.Delay(30, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var previousClip = audioSource.clip;
                audioSource.Stop();
                audioSource.clip = clip;
                DestroyOwnedAudioClip(previousClip, clip);
                audioSource.Play();
            }
        }

        private async Task SpeakResponseWithPrefetchAsync(
            string[] chunks,
            string voiceStyle,
            string chatRequestId,
            CancellationToken cancellationToken)
        {
            var tasks = new Task<AudioClip>[chunks.Length];
            var prefetchGate = new SemaphoreSlim(2, 2);
            for (var index = 0; index < chunks.Length; index++)
            {
                var chunkIndex = index;
                tasks[chunkIndex] = SynthesizeSpeechClipWithPrefetchGateAsync(
                    prefetchGate,
                    chunks[chunkIndex],
                    voiceStyle,
                    $"{chatRequestId}-tts-{chunkIndex}",
                    cancellationToken);
            }

            for (var index = 0; index < chunks.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunkTimer = System.Diagnostics.Stopwatch.StartNew();
                AudioClip clip = null;
                try
                {
                    clip = await tasks[index];
                }
                catch
                {
                    for (var cleanupIndex = index + 1; cleanupIndex < tasks.Length; cleanupIndex++)
                    {
                        if (tasks[cleanupIndex].IsCompletedSuccessfully)
                        {
                            DestroyOwnedAudioClip(tasks[cleanupIndex].Result, null);
                        }
                    }

                    throw;
                }

                Debug.Log($"Yui TTS chunk {index + 1}/{chunks.Length} latency: {chunkTimer.ElapsedMilliseconds} ms, chars={chunks[index].Length}, prefetch=true");
                if (clip == null)
                {
                    continue;
                }

                while (audioSource.isPlaying && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(30, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var previousClip = audioSource.clip;
                audioSource.Stop();
                audioSource.clip = clip;
                DestroyOwnedAudioClip(previousClip, clip);
                audioSource.Play();
            }
        }

        private async Task<AudioClip> SynthesizeSpeechClipWithPrefetchGateAsync(
            SemaphoreSlim gate,
            string text,
            string voiceStyle,
            string requestId,
            CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await SynthesizeSpeechClipAsync(text, voiceStyle, requestId, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        private string[] SplitSpeechTextForCurrentTts(string speechText)
        {
            if (IsHttpTtsMode())
            {
                var httpMaxCharacters = Mathf.Max(speechChunkMaxCharacters, 220);
                if (speechText.Length <= httpMaxCharacters)
                {
                    return new[] { speechText };
                }

                return YuiSpeechTextUtility.SplitSpeechText(
                    speechText,
                    httpMaxCharacters,
                    httpMaxCharacters,
                    httpMaxCharacters);
            }

            return YuiSpeechTextUtility.SplitSpeechText(speechText, speechChunkMaxCharacters);
        }

        private static void DestroyOwnedAudioClip(AudioClip previousClip, AudioClip nextClip)
        {
            if (previousClip == null || previousClip == nextClip)
            {
                return;
            }

            Destroy(previousClip);
        }

        private async Task<AudioClip> SynthesizeSpeechClipAsync(
            string text,
            string voiceStyle,
            string requestId,
            CancellationToken cancellationToken)
        {
            try
            {
                var canTryLocalVoicevox = !localVoicevoxUnavailable
                    && !IsTtsMode("server")
                    && !IsHttpTtsMode()
                    && !IsRemoteBackend()
                    && preferChatdollKitVoicevoxTts
                    && chatdollKitVoicevoxTts != null;
                if (canTryLocalVoicevox)
                {
                    var clip = await chatdollKitVoicevoxTts.SynthesizeAsync(
                        text,
                        voiceStyle,
                        cancellationToken);
                    if (clip != null)
                    {
                        Debug.Log("Yui TTS source: ChatdollKit VoicevoxSpeechSynthesizer");
                        return clip;
                    }

                    Debug.LogWarning("Local VOICEVOX TTS returned no audio clip; falling back to backend TTS.");
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                localVoicevoxUnavailable = true;
                Debug.LogWarning($"ChatdollKit VOICEVOX TTS failed; falling back to backend TTS: {ex.Message}");
            }

            Debug.Log("Yui TTS source: FastAPI backend direct audio");
            return await client.SynthesizeSpeechClipAsync(
                new TtsRequest
                {
                    RequestId = requestId,
                    Provider = IsHttpTtsMode() ? "http" : null,
                    Text = text,
                    SpeakerId = speakerId,
                    SpeedScale = speedScale,
                    PitchScale = YuiTtsTuning.SafePitchForMode(ttsMode, pitchScale),
                    IntonationScale = intonationScale,
                    VolumeScale = synthesisVolumeScale,
                    PrePhonemeLength = prePhonemeLength,
                    PostPhonemeLength = postPhonemeLength,
                    VoiceInstruct = IsHttpTtsMode() ? irodoriVoiceInstruct : null,
                    VoiceGender = IsHttpTtsMode() ? irodoriVoiceGender : null,
                    VoiceLangCode = IsHttpTtsMode() ? "ja" : null
                },
                cancellationToken);
        }

        private bool IsHttpTtsMode()
        {
            return IsTtsMode("server-http");
        }

        private bool IsRemoteBackend()
        {
            if (client == null || string.IsNullOrWhiteSpace(client.BaseUrl))
            {
                return false;
            }

            return !client.BaseUrl.Contains("127.0.0.1")
                && !client.BaseUrl.Contains("localhost");
        }

        private void ConfigureChatdollKitVoicevoxTts()
        {
            if (chatdollKitVoicevoxTts == null)
            {
                return;
            }

            chatdollKitVoicevoxTts.Configure(
                "http://127.0.0.1:50021",
                speakerId,
                speedScale,
                pitchScale,
                intonationScale,
                synthesisVolumeScale,
                prePhonemeLength,
                postPhonemeLength);
        }

    }
}
