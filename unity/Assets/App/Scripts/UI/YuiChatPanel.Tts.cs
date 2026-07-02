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
                ReleaseCurrentPlaybackClip();
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

            if (IsLikelyBrokenSpeechText(speechText))
            {
                Debug.LogWarning($"Yui TTS skipped broken speech text: {speechText}");
                SetStatus("Ready");
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

            await WaitForCurrentPlaybackToFinishAsync(cancellationToken);
            ReleaseCurrentPlaybackClip();
            SetStatus("Connected");
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

            await WaitForCurrentPlaybackToFinishAsync(cancellationToken);
            ReleaseCurrentPlaybackClip();
            SetStatus("Connected");
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
            if (IsTtsMode("aivis-native") || ShouldSynthesizeWithLocalAiRuntime())
            {
                var localMaxCharacters = Mathf.Max(speechChunkMaxCharacters, 180);
                if (speechText.Length <= localMaxCharacters)
                {
                    return new[] { speechText };
                }

                return YuiSpeechTextUtility.SplitSpeechText(
                    speechText,
                    localMaxCharacters,
                    localMaxCharacters,
                    localMaxCharacters);
            }

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

        private async Task WaitForCurrentPlaybackToFinishAsync(CancellationToken cancellationToken)
        {
            while (audioSource != null && audioSource.isPlaying && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(30, cancellationToken);
            }
        }

        private void ReleaseCurrentPlaybackClip()
        {
            if (audioSource == null)
            {
                return;
            }

            var previousClip = audioSource.clip;
            audioSource.Stop();
            audioSource.clip = null;
            DestroyOwnedAudioClip(previousClip, null);
            CollectAivisMobileGarbage();
        }

        private async Task<AudioClip> SynthesizeSpeechClipAsync(
            string text,
            string voiceStyle,
            string requestId,
            CancellationToken cancellationToken)
        {
            if (IsTtsMode("aivis-native"))
            {
                return await SynthesizeAivisNativeSpeechClipAsync(text, requestId, cancellationToken);
            }

            if (YuiTtsRuntimeRouting.IsVoicevoxIntent(ttsMode))
            {
                var route = YuiTtsRuntimeRouting.ResolveVoicevoxRoute(
                    BackendVoicevoxAvailable(),
                    NativeVoicevoxAvailable(),
                    IsRemoteBackend());
                if (route == YuiTtsExecutionRoute.NativeVoicevox)
                {
                    try
                    {
                        return await SynthesizeVoicevoxCoreSpeechClipAsync(text, requestId, cancellationToken);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        localVoicevoxUnavailable = true;
                        if (!BackendVoicevoxAvailable())
                        {
                            throw;
                        }

                        Debug.LogWarning($"VOICEVOX Core failed; falling back to backend VOICEVOX: {ex.Message}");
                    }
                }
            }

            if (ShouldSynthesizeWithLocalAiRuntime())
            {
                if (localAiService == null)
                {
                    ConfigureAiRuntimeRouter();
                }

                if (localAiService != null)
                {
                    var localSpeech = await localAiService.SynthesizeSpeechAsync(
                        new YuiPhysicalAI.LocalAI.YuiLocalAiSpeechRequest
                        {
                            Text = text,
                            VoiceStyle = voiceStyle,
                            LanguageCode = "ja",
                            SpeedScale = speedScale,
                            PitchScale = pitchScale
                        },
                        cancellationToken);

                    if (localSpeech != null && localSpeech.Success && localSpeech.AudioBytes != null && localSpeech.AudioBytes.Length > 44)
                    {
                        Debug.Log($"Yui TTS source: Local AI ({localSpeech.ModelId ?? "local runtime"}), latency={localSpeech.LatencyMs} ms");
                        return WavUtility.ToAudioClip(localSpeech.AudioBytes, requestId);
                    }

                    var error = localSpeech == null
                        ? "Local AI TTS returned no response."
                        : $"{localSpeech.ErrorCode} {localSpeech.ErrorMessage}".Trim();
                    Debug.LogWarning($"Local AI TTS failed: {error}");
                }

                return null;
            }

            try
            {
                var canTryLocalVoicevox = !localVoicevoxUnavailable
                    && YuiTtsRuntimeRouting.ShouldTryChatdollKitVoicevoxFallback(ttsMode)
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
            var backendTtsProvider = BackendTtsProviderForMode();
            var safeSpeed = YuiTtsTuning.SafeSpeedForMode(ttsMode, speedScale);
            var safePitch = YuiTtsTuning.SafePitchForMode(ttsMode, pitchScale);
            Debug.Log($"Yui TTS request: provider={backendTtsProvider}, speaker={speakerId}, speed={safeSpeed:0.###}, pitch={safePitch:0.###}, intonation={intonationScale:0.###}");
            return await client.SynthesizeSpeechClipAsync(
                new TtsRequest
                {
                    RequestId = requestId,
                    Provider = backendTtsProvider,
                    Text = text,
                    SpeakerId = speakerId,
                    SpeedScale = safeSpeed,
                    PitchScale = safePitch,
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

        private async Task<AudioClip> SynthesizeAivisNativeSpeechClipAsync(
            string text,
            string requestId,
            CancellationToken cancellationToken)
        {
            await aivisNativeSynthesisLock.WaitAsync(cancellationToken);
            try
            {
                CollectAivisMobileGarbage();
                YuiMemoryDiagnostics.LogSnapshot("aivis_before_native", $"tts_chars={text?.Length ?? 0}");
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var safeSpeed = YuiTtsTuning.SafeSpeedForMode(ttsMode, speedScale);
                var safePitch = YuiTtsTuning.SafePitchForMode(ttsMode, pitchScale);
                var audioBytes = await Task.Run(
                    () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var result = YuiPhysicalAI.LocalAI.YuiAivisNativeBridge.Synthesize(
                            text,
                            speakerId > 0 ? speakerId : 1431611904,
                            safeSpeed,
                            safePitch,
                            intonationScale,
                            synthesisVolumeScale,
                            prePhonemeLength,
                            postPhonemeLength);
                        if (result == null || !result.Ok)
                        {
                            var error = result == null
                                ? "Aivis native bridge returned no response."
                                : $"{result.ErrorCode} {result.ErrorMessage} {FormatMissingComponents(result.MissingComponents)}".Trim();
                            throw new InvalidOperationException(error);
                        }

                        var bytes = result.AudioBytes();
                        if (bytes == null || bytes.Length <= 44)
                        {
                            throw new InvalidOperationException("Aivis native bridge returned empty audio.");
                        }

                        return bytes;
                    },
                    cancellationToken);

                timer.Stop();
                Debug.Log($"Yui TTS source: Aivis Native, latency={timer.ElapsedMilliseconds} ms, bytes={audioBytes.Length}");
                YuiMemoryDiagnostics.LogSnapshot(
                    "aivis_after_native",
                    $"tts_chars={text?.Length ?? 0},tts_ms={timer.ElapsedMilliseconds},wav_bytes={audioBytes.Length}");
                var clip = WavUtility.ToAudioClip(audioBytes, requestId);
                audioBytes = null;
                CollectAivisMobileGarbage();
                YuiMemoryDiagnostics.LogSnapshot("aivis_after_clip", $"tts_chars={text?.Length ?? 0}");
                return clip;
            }
            finally
            {
                aivisNativeSynthesisLock.Release();
            }
        }

        private static void CollectAivisMobileGarbage()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
#endif
        }

#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
        private async Task<AudioClip> SynthesizeVoicevoxCoreSpeechClipAsync(
            string text,
            string requestId,
            CancellationToken cancellationToken)
        {
            return await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var safeSpeed = YuiTtsTuning.SafeSpeedForMode(ttsMode, speedScale);
                    var safePitch = YuiTtsTuning.SafePitchForMode(ttsMode, pitchScale);
                    var result = YuiPhysicalAI.LocalAI.YuiVoicevoxCoreBridge.Synthesize(
                        text,
                        speakerId > 0 ? speakerId : 14,
                        safeSpeed,
                        safePitch,
                        intonationScale,
                        synthesisVolumeScale,
                        prePhonemeLength,
                        postPhonemeLength);
                    timer.Stop();
                    if (result == null || !result.Ok)
                    {
                        var error = result == null
                            ? "VOICEVOX Core returned no response."
                            : $"{result.ErrorCode} {result.ErrorMessage}".Trim();
                        throw new InvalidOperationException(error);
                    }

                    var audioBytes = result.AudioBytes();
                    if (audioBytes == null || audioBytes.Length <= 44)
                    {
                        throw new InvalidOperationException("VOICEVOX Core returned empty audio.");
                    }

                    Debug.Log($"Yui TTS source: VOICEVOX Core, latency={timer.ElapsedMilliseconds} ms, bytes={audioBytes.Length}");
                    return WavUtility.ToAudioClip(audioBytes, requestId);
                },
                cancellationToken);
        }
#else
        private Task<AudioClip> SynthesizeVoicevoxCoreSpeechClipAsync(
            string text,
            string requestId,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("VOICEVOX Core is not available in this build.");
        }
#endif

        private static string FormatMissingComponents(string[] missingComponents)
        {
            return missingComponents == null || missingComponents.Length == 0
                ? string.Empty
                : $"missing=[{string.Join(",", missingComponents)}]";
        }

        private string BackendTtsProviderForMode()
        {
            return YuiTtsRuntimeRouting.BackendProviderForMode(ttsMode) ?? "voicevox";
        }

        private bool ShouldSynthesizeWithLocalAiRuntime()
        {
            return IsLocalAiTtsMode() || ShouldUseOnDeviceSpeechForCurrentPlatform();
        }

        private bool IsHttpTtsMode()
        {
            return IsTtsMode("server-http");
        }

        private bool NativeVoicevoxAvailable()
        {
            return !localVoicevoxUnavailable
                && YuiPhysicalAI.LocalAI.YuiVoicevoxCoreBridge.IsSupported;
        }

        private bool BackendVoicevoxAvailable()
        {
            if (!backendConfigLoaded || ttsProviderOptions == null)
            {
                return false;
            }

            foreach (var provider in ttsProviderOptions)
            {
                if (string.Equals(provider, "voicevox", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLikelyBrokenSpeechText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            var meaningful = 0;
            var broken = 0;
            foreach (var ch in text)
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

            return meaningful >= 6 && broken >= meaningful * 0.45f;
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
            if (chatdollKitVoicevoxTts == null || !IsTtsMode("server"))
            {
                return;
            }

            chatdollKitVoicevoxTts.Configure(
                "http://127.0.0.1:50021",
                speakerId,
                YuiTtsTuning.SafeSpeedForMode(ttsMode, speedScale),
                YuiTtsTuning.SafePitchForMode(ttsMode, pitchScale),
                intonationScale,
                synthesisVolumeScale,
                prePhonemeLength,
                postPhonemeLength);
        }

    }
}
