using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YuiPhysicalAI.LocalAI
{
    public sealed class YuiPlatformVisionLocalAiRuntime : IYuiLocalAiRuntime
    {
        public string RuntimeName => "platform-vision";

        public YuiLocalAiStatus GetStatus()
        {
            return new YuiLocalAiStatus
            {
                Available = YuiPlatformVisionBridge.IsSupported,
                RuntimeName = RuntimeName,
                Detail = YuiPlatformVisionBridge.IsSupported
                    ? "On-device platform image classification and OCR are available."
                    : "On-device platform image recognition is not available.",
                Capabilities = new[] { YuiLocalAiCapability.Vision }
            };
        }

        public bool Supports(YuiLocalAiCapability capability)
        {
            return YuiPlatformVisionBridge.IsSupported && capability == YuiLocalAiCapability.Vision;
        }

        public Task WarmAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<YuiLocalAiChatResponse> ChatAsync(YuiLocalAiChatRequest request, CancellationToken cancellationToken)
        {
            return Unsupported<YuiLocalAiChatResponse>(YuiLocalAiCapability.Chat);
        }

        public Task<YuiLocalAiTranscriptionResponse> TranscribeAsync(YuiLocalAiAudioRequest request, CancellationToken cancellationToken)
        {
            return Unsupported<YuiLocalAiTranscriptionResponse>(YuiLocalAiCapability.Transcription);
        }

        public Task<YuiLocalAiSpeechResponse> SynthesizeSpeechAsync(YuiLocalAiSpeechRequest request, CancellationToken cancellationToken)
        {
            return Unsupported<YuiLocalAiSpeechResponse>(YuiLocalAiCapability.SpeechSynthesis);
        }

        public async Task<YuiLocalAiVisionResponse> AnalyzeImageAsync(YuiLocalAiVisionRequest request, CancellationToken cancellationToken)
        {
            var result = await YuiPlatformVisionBridge.AnalyzeAsync(request, cancellationToken);
            return new YuiLocalAiVisionResponse
            {
                Success = result.Ok, ErrorCode = result.ErrorCode, ErrorMessage = result.ErrorMessage,
                ModelId = RuntimeName, VisionResultId = result.Ok ? Guid.NewGuid().ToString("N") : null,
                Summary = result.Summary ?? string.Empty,
                Structured = new Dictionary<string, object>
                {
                    ["labels"] = result.Labels ?? Array.Empty<string>(),
                    ["recognized_text"] = result.RecognizedText ?? string.Empty
                }
            };
        }

        private static Task<TResponse> Unsupported<TResponse>(YuiLocalAiCapability capability)
            where TResponse : YuiLocalAiResponse, new()
        {
            return Task.FromResult(new TResponse
            {
                Success = false,
                ErrorCode = "capability_unavailable",
                ErrorMessage = $"Platform vision runtime does not support {capability}."
            });
        }
    }
}
