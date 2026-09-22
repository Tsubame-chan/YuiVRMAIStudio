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

            var speechSource = ResolveSpeechText(chat);
            var shouldSpeak = chat.ShouldTts
                || (forceTtsForNonEmptyReplies && !string.IsNullOrWhiteSpace(speechSource));
            Debug.Log(
                $"Yui TTS decision: should_tts={chat.ShouldTts}, force_non_empty={forceTtsForNonEmptyReplies}, should_speak={shouldSpeak}, display_length={(chat.Text ?? string.Empty).Length}, speech_length={speechSource.Length}");

            if (!shouldSpeak)
            {
                return;
            }

            var speechText = YuiSpeechTextUtility.CleanSpeechText(speechSource);
            if (string.IsNullOrWhiteSpace(speechText))
            {
                return;
            }

            if (IsLikelyBrokenSpeechText(speechText))
            {
                Debug.LogWarning("Yui TTS skipped broken speech text.");
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

            try
            {
                for (var index = 0; index < chunks.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var clip = await SynthesizeSpeechClipAsync(chunks[index], chat.VoiceStyle,
                        $"{chatRequestId}-tts-{index}", cancellationToken);
                    await PlayPreparedSpeechClipAsync(clip, cancellationToken);
                }
                await WaitForCurrentPlaybackToFinishAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                SetStatus("Connected");
            }
            finally { ReleaseCurrentPlaybackClip(); }
        }

        private async Task PlayPreparedSpeechClipAsync(AudioClip clip, CancellationToken cancellationToken)
        {
            if (clip == null) throw new InvalidOperationException("Speech synthesis returned no audio.");
            var ownedByPlayer = false;
            try
            {
                await WaitForCurrentPlaybackToFinishAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var previous = audioSource.clip;
                audioSource.Stop();
                audioSource.clip = clip;
                ownedByPlayer = true;
                DestroyOwnedAudioClip(previous, clip);
                audioSource.Play();
            }
            finally { if (!ownedByPlayer) DestroyOwnedAudioClip(clip, null); }
        }

        private string ResolveSpeechText(ChatResponse chat)
        {
            if (chat == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(chat.SpokenText))
            {
                return chat.SpokenText;
            }

            if (!YuiChatRequestModes.IsWork(chatInteractionMode))
            {
                return chat.Text ?? string.Empty;
            }

            var compact = string.Join(
                " ",
                (chat.Text ?? string.Empty).Split(
                    new[] { ' ', '\r', '\n', '\t' },
                    StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(compact))
            {
                return "作業結果を画面にまとめたよ。";
            }

            var end = compact.IndexOfAny(new[] { '。', '！', '？', '!', '?' });
            if (end >= 0)
            {
                compact = compact.Substring(0, end + 1);
            }

            return compact.Length > 160 ? compact.Substring(0, 157).TrimEnd() + "..." : compact;
        }

        private async Task SpeakResponseWithPrefetchAsync(
            string[] chunks,
            string voiceStyle,
            string chatRequestId,
            CancellationToken cancellationToken)
        {
            using var prefetch = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var tasks = new Task<AudioClip>[chunks.Length];
            try
            {
                // Keep only two chunks in flight instead of synthesizing the
                // whole answer while the user is still hearing its first sentence.
                for (var index = 0; index < chunks.Length; index++)
                {
                    prefetch.Token.ThrowIfCancellationRequested();
                    tasks[index] ??= SynthesizeSpeechClipAsync(chunks[index], voiceStyle,
                        $"{chatRequestId}-tts-{index}", prefetch.Token);
                    if (index + 1 < chunks.Length)
                        tasks[index + 1] ??= SynthesizeSpeechClipAsync(chunks[index + 1], voiceStyle,
                            $"{chatRequestId}-tts-{index + 1}", prefetch.Token);
                    var clip = await tasks[index];
                    tasks[index] = null;
                    await PlayPreparedSpeechClipAsync(clip, prefetch.Token);
                }
                await WaitForCurrentPlaybackToFinishAsync(prefetch.Token);
                prefetch.Token.ThrowIfCancellationRequested();
                SetStatus("Connected");
            }
            finally
            {
                prefetch.Cancel();
                ReleaseCurrentPlaybackClip();
                foreach (var pending in tasks)
                {
                    if (pending == null) continue;
                    try { DestroyOwnedAudioClip(await pending, null); }
                    catch (Exception) { /* Observe failed/cancelled synthesis while preserving the original error. */ }
                }
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
                var explicitlyNative = IsTtsMode("voicevox-native");
                if (explicitlyNative && !NativeVoicevoxAvailable())
                    throw new InvalidOperationException("端末内の音声データが未準備です。設定からデータを取得してください。");
                var route = explicitlyNative ? YuiTtsExecutionRoute.NativeVoicevox : YuiTtsRuntimeRouting.ResolveVoicevoxRoute(
                    BackendVoicevoxAvailable(),
                    NativeVoicevoxAvailable(),
                    IsRemoteBackend(),
                    preferNative: IsLocalAiConversationMode() || IsDirectOpenAiConversationMode()
                        || (YuiPhysicalAI.Core.YuiConversationModes.Normalize(conversationMode) == YuiPhysicalAI.Core.YuiConversationModes.Stable
                            && selectedChatEndpoint != YuiPhysicalAI.LocalAI.YuiAiEndpoint.Backend));
                // Route by the installed voice model, independently of LLM mode.
                if (speakerId > 0 && !YuiPhysicalAI.LocalAI.YuiVoicevoxModelCatalog.IsAvailable(speakerId))
                {
                    if (explicitlyNative || !BackendVoicevoxAvailable())
                        throw new InvalidOperationException("This voice requires Backend. Choose the available voice in Settings → Voice.");
                    route = YuiTtsExecutionRoute.Backend;
                }
                if (route == YuiTtsExecutionRoute.NativeVoicevox)
                {
                    try
                    {
                        return await SynthesizeVoicevoxCoreSpeechClipAsync(text, requestId, cancellationToken);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        localVoicevoxUnavailable = true;
                        if (explicitlyNative || IsLocalAiConversationMode() || IsDirectOpenAiConversationMode() || !BackendVoicevoxAvailable())
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

            await EnsureExternalDataPermissionAsync(backendUrl, false, cancellationToken);
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
                var result = await YuiPhysicalAI.LocalAI.YuiAivisNativeBridge.SynthesizeAsync(
                    text, speakerId > 0 ? speakerId : 1431611904, safeSpeed, safePitch,
                    intonationScale, synthesisVolumeScale, prePhonemeLength, postPhonemeLength, cancellationToken);
                if (result == null || !result.Ok)
                    throw new InvalidOperationException(result == null ? "Aivis native bridge returned no response." :
                        $"{result.ErrorCode} {result.ErrorMessage} {FormatMissingComponents(result.MissingComponents)}".Trim());
                var audioBytes = result.AudioBytes();
                if (audioBytes == null || audioBytes.Length <= 44)
                    throw new InvalidOperationException("Aivis native bridge returned empty audio.");

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

        private async Task<AudioClip> SynthesizeVoicevoxCoreSpeechClipAsync(
            string text, string requestId, CancellationToken cancellationToken)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var result = await YuiPhysicalAI.LocalAI.YuiVoicevoxCoreBridge.SynthesizeAsync(
                text, speakerId > 0 ? speakerId : 14,
                YuiTtsTuning.SafeSpeedForMode(ttsMode, speedScale),
                YuiTtsTuning.SafePitchForMode(ttsMode, pitchScale),
                intonationScale, synthesisVolumeScale, prePhonemeLength, postPhonemeLength, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result == null || !result.Ok)
                throw new InvalidOperationException(result == null ? "VOICEVOX Core returned no response." : (result.ErrorCode + " " + result.ErrorMessage));
            var audioBytes = result.AudioBytes();
            if (audioBytes == null || audioBytes.Length <= 44) throw new InvalidOperationException("VOICEVOX Core returned empty audio.");
            Debug.Log($"Yui TTS source: VOICEVOX Core, latency={timer.ElapsedMilliseconds} ms, bytes={audioBytes.Length}");
            // AudioClip is a Unity object and must be created on the main thread after the worker completes.
            return WavUtility.ToAudioClip(audioBytes, requestId);
        }


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
            if (localVoicevoxUnavailable || !YuiPhysicalAI.LocalAI.YuiVoicevoxCoreBridge.IsSupported) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android's bundled files are extracted from the APK on first synthesis.
            return true;
#else
            var root = YuiPhysicalAI.LocalAI.YuiLocalAiPathResolver.VoicevoxRootPath();
            return System.IO.File.Exists(System.IO.Path.Combine(root, "Models", "meimei_himari_1.vvm"))
                && System.IO.File.Exists(System.IO.Path.Combine(root, "open_jtalk_dic_utf_8-1.11", "sys.dic"));
#endif
        }

        public bool HasBackendVoicevox => BackendVoicevoxAvailable();

        private bool BackendVoicevoxAvailable()
        {
            if (!IsBackendRecentlyReachable() || !backendConfigLoaded || ttsProviderOptions == null)
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
