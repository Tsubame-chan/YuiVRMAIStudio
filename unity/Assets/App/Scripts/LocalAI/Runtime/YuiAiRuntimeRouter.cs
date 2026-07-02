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

        public async Task<ChatResponse> SendChatAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            var requiresLocal = PreferLocal || PreferLocalChat;
            if (requiresLocal && localService == null && !FallbackToBackend)
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
                        CharacterName = request?.CharacterName,
                        CustomInstruction = request?.CustomInstruction,
                        ScreenContext = request?.Context?.ScreenContext,
                        Extra = request?.Context?.Extra
                    },
                    cancellationToken);
                if (local.Success)
                {
                    return YuiLocalAiBackendCompatibility.ToChatResponse(local);
                }

                if (!FallbackToBackend)
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
                        CharacterName = request?.CharacterName,
                        CustomInstruction = request?.CustomInstruction,
                        ScreenContext = request?.Context?.ScreenContext,
                        Extra = request?.Context?.Extra
                    },
                    cancellationToken);
                if (local.Success)
                {
                    return YuiLocalAiBackendCompatibility.ToChatResponse(local);
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
            var requiresLocal = PreferLocal || PreferLocalTranscription;
            if (requiresLocal && localService == null && !FallbackToBackendTranscription)
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

                if (!FallbackToBackendTranscription)
                {
                    throw new InvalidOperationException(LocalError(local));
                }

                Debug.LogWarning($"Local AI STT failed; falling back to backend: {LocalError(local)}");
            }

            return await backendTranscribe(wavBytes, filename, durationMs, cancellationToken);
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
            if (PreferLocalVision && localService == null && !FallbackToBackendVision)
            {
                throw new InvalidOperationException("Local AI vision failed: local vision runtime is not available.");
            }

            if (PreferLocalVision && localService != null)
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

                if (!FallbackToBackendVision)
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
                && ex.StatusCode == 0;
        }
    }
}
