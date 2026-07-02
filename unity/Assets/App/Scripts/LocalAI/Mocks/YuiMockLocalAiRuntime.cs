using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YuiPhysicalAI.LocalAI
{
    public sealed class YuiMockLocalAiRuntime : IYuiLocalAiRuntime
    {
        private readonly HashSet<YuiLocalAiCapability> capabilities;

        public YuiMockLocalAiRuntime(params YuiLocalAiCapability[] capabilities)
        {
            this.capabilities = new HashSet<YuiLocalAiCapability>(capabilities ?? Array.Empty<YuiLocalAiCapability>());
        }

        public string RuntimeName => "mock-local-ai";

        public YuiLocalAiStatus GetStatus()
        {
            return new YuiLocalAiStatus
            {
                Available = capabilities.Count > 0,
                RuntimeName = RuntimeName,
                Detail = "Mock runtime for local AI foundation tests and offline app wiring checks.",
                Capabilities = capabilities.ToArray()
            };
        }

        public bool Supports(YuiLocalAiCapability capability)
        {
            return capabilities.Contains(capability);
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
            return Task.FromResult(new YuiLocalAiChatResponse
            {
                Success = true,
                ModelId = "mock-text",
                Text = $"[local mock] {request?.Message ?? string.Empty}",
                Face = "neutral",
                Animation = "idle",
                VoiceStyle = "normal",
                ShouldTts = true
            });
        }

        public Task<YuiLocalAiTranscriptionResponse> TranscribeAsync(YuiLocalAiAudioRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new YuiLocalAiTranscriptionResponse
            {
                Success = true,
                ModelId = "mock-audio",
                Text = "ローカル音声認識のテスト結果",
                Confidence = 0.5f
            });
        }

        public Task<YuiLocalAiSpeechResponse> SynthesizeSpeechAsync(YuiLocalAiSpeechRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new YuiLocalAiSpeechResponse
            {
                Success = true,
                ModelId = "mock-audio",
                AudioBytes = Array.Empty<byte>(),
                MimeType = "audio/wav",
                SampleRate = 24000
            });
        }

        public Task<YuiLocalAiVisionResponse> AnalyzeImageAsync(YuiLocalAiVisionRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new YuiLocalAiVisionResponse
            {
                Success = true,
                ModelId = "mock-vision",
                VisionResultId = Guid.NewGuid().ToString("N"),
                Summary = "ローカル画像認識のテスト結果"
            });
        }
    }
}
