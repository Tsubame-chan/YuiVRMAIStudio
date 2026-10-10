using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.LocalAI
{
    public sealed class YuiAiRuntimeRouter
    {
        private readonly YuiLocalAiService localService;
        private readonly Func<ChatRequest, CancellationToken, Task<ChatResponse>> backendChat;
        private readonly Func<byte[], string, int?, CancellationToken, Task<SttResponse>> backendTranscribe;
        private readonly Func<byte[], string, string, string, CancellationToken, Task<VisionResponse>> backendVision;

        public YuiAiRuntimeRouter(
            YuiLocalAiService localService,
            Func<ChatRequest, CancellationToken, Task<ChatResponse>> backendChat,
            Func<byte[], string, int?, CancellationToken, Task<SttResponse>> backendTranscribe,
            Func<byte[], string, string, string, CancellationToken, Task<VisionResponse>> backendVision)
        {
            this.localService = localService;
            this.backendChat = backendChat ?? throw new ArgumentNullException(nameof(backendChat));
            this.backendTranscribe = backendTranscribe ?? throw new ArgumentNullException(nameof(backendTranscribe));
            this.backendVision = backendVision ?? throw new ArgumentNullException(nameof(backendVision));
        }

        public string SpeechLanguageCode { get; set; } = "ja";
        public bool PreferLocal { get; set; }
        public bool PreferLocalChat { get; set; }
        public bool PreferLocalTranscription { get; set; }
        public bool PreferLocalVision { get; set; }
        public bool FallbackToBackend { get; set; } = true;
        public bool FallbackToBackendTranscription { get; set; } = true;
        public bool FallbackToBackendVision { get; set; } = true;
        public bool FallbackToLocalChat { get; set; }
        public bool FallbackToLocalTranscription { get; set; }
        public Func<YuiLocalAiCapability, CancellationToken, Task<YuiAiEndpoint>> SelectEndpoint { get; set; }
        public Func<ChatRequest, CancellationToken, Task<ChatResponse>> DirectChat { get; set; }
        public Func<byte[], string, CancellationToken, Task<SttResponse>> DirectTranscribe { get; set; }
        public Func<byte[], string, CancellationToken, Task<VisionResponse>> DirectVision { get; set; }

        public async Task<ChatResponse> SendChatAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var endpoint = SelectEndpoint != null ? await SelectEndpoint(YuiLocalAiCapability.Chat, cancellationToken) : YuiAiEndpoint.Backend;
            if (endpoint == YuiAiEndpoint.DirectOpenAi && !(PreferLocal || PreferLocalChat))
                return await (DirectChat ?? throw new InvalidOperationException("Direct OpenAI chat is unavailable."))(request, cancellationToken);
            var requiresLocal = PreferLocal || PreferLocalChat || endpoint == YuiAiEndpoint.Local;
            if (requiresLocal && localService == null && (!FallbackToBackend || endpoint == YuiAiEndpoint.Local))
            {
                throw new InvalidOperationException("Local AI request failed: local runtime is not available.");
            }

            if (requiresLocal && localService != null)
            {
                var local = await localService.ChatAsync(
                    ToLocalChatRequest(request),
                    cancellationToken);
                if (local.Success)
                {
                    return YuiLocalAiBackendCompatibility.ToChatResponse(local, request?.Mode);
                }

                if (!FallbackToBackend || endpoint == YuiAiEndpoint.Local)
                {
                    throw new InvalidOperationException(LocalError(local));
                }

                Debug.LogWarning($"Local AI chat failed; falling back to backend: {LocalError(local)}");
            }

            try
            {
                return await backendChat(request, cancellationToken);
            }
            catch (YuiBackendException ex) when (ShouldFallbackFromBackendToLocal(ex))
            {
                Debug.LogWarning($"Backend chat failed; falling back to Local AI for this request: {ex.Message}");
                var local = await localService.ChatAsync(
                    ToLocalChatRequest(request),
                    cancellationToken);
                if (local.Success)
                {
                    return YuiLocalAiBackendCompatibility.ToChatResponse(local, request?.Mode);
                }

                throw new InvalidOperationException(LocalError(local), ex);
            }
        }

        private static YuiLocalAiChatRequest ToLocalChatRequest(ChatRequest request) => new YuiLocalAiChatRequest
        {
            RequestId = request?.RequestId,
            LanguageCode = request?.LanguageCode ?? "ja",
            UserId = request?.UserId,
            Message = request?.Message,
            Mode = request?.Mode ?? "talk",
            CharacterName = request?.CharacterName,
            CustomInstruction = request?.CustomInstruction,
            ResponseInstruction = request?.ResponseInstruction,
            ScreenContext = request?.Context?.ScreenContext,
            Extra = request?.Context?.Extra
        };

        public async Task<SttResponse> TranscribeAsync(
            byte[] wavBytes,
            string filename,
            int? durationMs,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var endpoint = SelectEndpoint != null ? await SelectEndpoint(YuiLocalAiCapability.Transcription, cancellationToken) : YuiAiEndpoint.Backend;
            if (endpoint == YuiAiEndpoint.DirectOpenAi && !(PreferLocal || PreferLocalTranscription))
                return await (DirectTranscribe ?? throw new InvalidOperationException("Direct OpenAI transcription is unavailable."))(wavBytes, filename, cancellationToken);
            var requiresLocal = PreferLocal || PreferLocalTranscription || endpoint == YuiAiEndpoint.Local;
            if (requiresLocal && localService == null && (!FallbackToBackendTranscription || endpoint == YuiAiEndpoint.Local))
            {
                throw new InvalidOperationException("Local AI STT failed: local transcription runtime is not available.");
            }

            if (requiresLocal && localService != null)
            {
                ValidateLocalTranscriptionDuration(durationMs);
                if (TryReadWavInfo(wavBytes, out _, out var audioDurationMs) && audioDurationMs > 30000)
                    ValidateLocalTranscriptionDuration((int)Math.Ceiling(audioDurationMs));
                var local = await localService.TranscribeAsync(
                    new YuiLocalAiAudioRequest
                    {
                        AudioBytes = wavBytes,
                        LanguageCode = SpeechLanguageCode,
                        MimeType = "audio/wav",
                        SampleRate = TryReadWavSampleRate(wavBytes) ?? 0
                    },
                    cancellationToken);
                if (local.Success)
                {
                    return YuiLocalAiBackendCompatibility.ToSttResponse(local);
                }

                if (!FallbackToBackendTranscription || endpoint == YuiAiEndpoint.Local)
                {
                    throw new InvalidOperationException(LocalError(local));
                }

                Debug.LogWarning($"Local AI STT failed; falling back to backend: {LocalError(local)}");
            }

            try
            {
                return await backendTranscribe(wavBytes, filename, durationMs, cancellationToken);
            }
            catch (YuiBackendException ex) when (FallbackToLocalTranscription && localService != null
                && IsBackendUnavailable(ex))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateLocalTranscriptionDuration(durationMs);
                if (TryReadWavInfo(wavBytes, out _, out var audioDurationMs) && audioDurationMs > 30000)
                    ValidateLocalTranscriptionDuration((int)Math.Ceiling(audioDurationMs));
                var local = await localService.TranscribeAsync(new YuiLocalAiAudioRequest
                {
                    AudioBytes = wavBytes,
                        LanguageCode = SpeechLanguageCode,
                    MimeType = "audio/wav",
                    SampleRate = TryReadWavSampleRate(wavBytes) ?? 0
                }, cancellationToken);
                if (local.Success) return YuiLocalAiBackendCompatibility.ToSttResponse(local);
                throw new InvalidOperationException(LocalError(local), ex);
            }
        }

        private static void ValidateLocalTranscriptionDuration(int? durationMs)
        {
            if (durationMs.HasValue && durationMs.Value > 30000)
                throw new InvalidOperationException("ローカル音声入力は1回30秒までです。短く録音し直すか、API音声認識を使用してください。");
        }

        private static int? TryReadWavSampleRate(byte[] wavBytes)
        {
            return TryReadWavInfo(wavBytes, out var sampleRate, out _) ? sampleRate : (int?)null;
        }

        internal static bool TryReadWavInfo(byte[] bytes, out int sampleRate, out double durationMs)
        {
            sampleRate = 0;
            durationMs = 0;
            if (bytes == null || bytes.Length < 12 || !ChunkIs(bytes, 0, "RIFF") || !ChunkIs(bytes, 8, "WAVE")) return false;
            uint byteRate = 0;
            int blockAlign = 0;
            long dataSize = -1;
            for (long offset = 12; offset + 8 <= bytes.Length;)
            {
                var index = (int)offset;
                uint size = ReadWavUInt32(bytes, index + 4);
                long body = offset + 8;
                long next = body + size + (size & 1);
                // Reject truncated and overflowing chunks rather than looping on
                // a negative signed size or reading outside the supplied buffer.
                if (body + size > bytes.Length || next <= offset) return false;
                if (ChunkIs(bytes, index, "fmt "))
                {
                    if (size < 16) return false;
                    uint rate = ReadWavUInt32(bytes, (int)body + 4);
                    if (rate == 0 || rate > int.MaxValue) return false;
                    sampleRate = (int)rate;
                    byteRate = ReadWavUInt32(bytes, (int)body + 8);
                    blockAlign = bytes[(int)body + 12] | (bytes[(int)body + 13] << 8);
                    if (blockAlign == 0 || byteRate != (long)sampleRate * blockAlign) return false;
                }
                else if (ChunkIs(bytes, index, "data")) dataSize = size;
                offset = next;
            }
            if (sampleRate == 0 || byteRate == 0 || dataSize < 0 || dataSize % blockAlign != 0) return false;
            durationMs = dataSize * 1000.0 / byteRate;
            return true;
        }

        private static bool ChunkIs(byte[] bytes, int offset, string name) =>
            bytes[offset] == name[0] && bytes[offset + 1] == name[1]
            && bytes[offset + 2] == name[2] && bytes[offset + 3] == name[3];

        private static uint ReadWavUInt32(byte[] bytes, int offset) =>
            (uint)bytes[offset] | ((uint)bytes[offset + 1] << 8)
            | ((uint)bytes[offset + 2] << 16) | ((uint)bytes[offset + 3] << 24);

        public async Task<VisionResponse> AnalyzeImageAsync(
            byte[] imageBytes,
            string filename,
            string promptType,
            string mimeType,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var endpoint = SelectEndpoint != null ? await SelectEndpoint(YuiLocalAiCapability.Vision, cancellationToken) : YuiAiEndpoint.Backend;
            var requiresLocal = PreferLocalVision || endpoint == YuiAiEndpoint.Local;
            if (!requiresLocal && endpoint == YuiAiEndpoint.DirectOpenAi)
                return await (DirectVision ?? throw new InvalidOperationException("Direct OpenAI vision is unavailable."))(imageBytes, mimeType, cancellationToken);
            if (requiresLocal && localService == null && (!FallbackToBackendVision || endpoint == YuiAiEndpoint.Local))
            {
                throw new InvalidOperationException("Local AI vision failed: local vision runtime is not available.");
            }

            if (requiresLocal && localService != null)
            {
                var local = await localService.AnalyzeImageAsync(
                    new YuiLocalAiVisionRequest
                    {
                        ImageBytes = imageBytes,
                        MimeType = mimeType,
                        PromptType = promptType
                    },
                    cancellationToken);
                if (local.Success)
                {
                    return YuiLocalAiBackendCompatibility.ToVisionResponse(local);
                }

                if (!FallbackToBackendVision || endpoint == YuiAiEndpoint.Local)
                {
                    throw new InvalidOperationException(LocalError(local));
                }

                Debug.LogWarning($"Local AI vision failed; falling back to backend: {LocalError(local)}");
            }

            return await backendVision(imageBytes, filename, promptType, mimeType, cancellationToken);
        }

        private static string LocalError(YuiLocalAiResponse response)
        {
            if (response == null)
            {
                return "Local AI request failed.";
            }

            return $"Local AI request failed: {response.ErrorCode} {response.ErrorMessage}".Trim();
        }

        private bool ShouldFallbackFromBackendToLocal(YuiBackendException ex)
        {
            return FallbackToLocalChat
                && localService != null
                && ex != null
                && IsBackendUnavailable(ex);
        }

        public static bool IsBackendUnavailable(YuiBackendException ex)
        {
            // Do not retry rejected/invalid user input or authentication failures.
            // 503 is the backend's explicit missing-provider configuration response.
            return ex != null && (ex.StatusCode == 0 || ex.StatusCode == 503);
        }
    }
}
