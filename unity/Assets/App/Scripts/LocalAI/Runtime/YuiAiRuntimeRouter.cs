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
                    new YuiLocalAiChatRequest
                    {
                        RequestId = request?.RequestId,
                        UserId = request?.UserId,
                        Message = request?.Message,
                        Mode = request?.Mode ?? "talk",
                        CharacterName = request?.CharacterName,
                        CustomInstruction = request?.CustomInstruction,
                        ScreenContext = request?.Context?.ScreenContext,
                        Extra = request?.Context?.Extra
                    },
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
                    new YuiLocalAiChatRequest
                    {
                        RequestId = request?.RequestId,
                        UserId = request?.UserId,
                        Message = request?.Message,
                        Mode = request?.Mode ?? "talk",
                        CharacterName = request?.CharacterName,
                        CustomInstruction = request?.CustomInstruction,
                        ScreenContext = request?.Context?.ScreenContext,
                        Extra = request?.Context?.Extra
                    },
                    cancellationToken);
                if (local.Success)
                {
                    return YuiLocalAiBackendCompatibility.ToChatResponse(local, request?.Mode);
                }

                throw new InvalidOperationException(LocalError(local), ex);
            }
        }

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
                var local = await localService.TranscribeAsync(
                    new YuiLocalAiAudioRequest
                    {
                        AudioBytes = wavBytes,
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
                var local = await localService.TranscribeAsync(new YuiLocalAiAudioRequest
                {
                    AudioBytes = wavBytes,
                    MimeType = "audio/wav",
                    SampleRate = TryReadWavSampleRate(wavBytes) ?? 0
                }, cancellationToken);
                if (local.Success) return YuiLocalAiBackendCompatibility.ToSttResponse(local);
                throw new InvalidOperationException(LocalError(local), ex);
            }
        }

        private static int? TryReadWavSampleRate(byte[] wavBytes)
        {
            if (wavBytes == null || wavBytes.Length < 28)
            {
                return null;
            }

            if (wavBytes[0] != (byte)'R'
                || wavBytes[1] != (byte)'I'
                || wavBytes[2] != (byte)'F'
                || wavBytes[3] != (byte)'F'
                || wavBytes[8] != (byte)'W'
                || wavBytes[9] != (byte)'A'
                || wavBytes[10] != (byte)'V'
                || wavBytes[11] != (byte)'E')
            {
                return null;
            }

            var offset = 12;
            while (offset + 16 <= wavBytes.Length)
            {
                var chunkSize = wavBytes[offset + 4]
                    | (wavBytes[offset + 5] << 8)
                    | (wavBytes[offset + 6] << 16)
                    | (wavBytes[offset + 7] << 24);
                if (wavBytes[offset] == (byte)'f'
                    && wavBytes[offset + 1] == (byte)'m'
                    && wavBytes[offset + 2] == (byte)'t'
                    && wavBytes[offset + 3] == (byte)' ')
                {
                    var sampleRateOffset = offset + 12;
                    return wavBytes[sampleRateOffset]
                        | (wavBytes[sampleRateOffset + 1] << 8)
                        | (wavBytes[sampleRateOffset + 2] << 16)
                        | (wavBytes[sampleRateOffset + 3] << 24);
                }

                offset += 8 + chunkSize + (chunkSize & 1);
            }

            return null;
        }

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
