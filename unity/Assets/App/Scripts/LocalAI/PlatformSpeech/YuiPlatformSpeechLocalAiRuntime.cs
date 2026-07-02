using System;
using System.Threading;
using System.Threading.Tasks;

namespace YuiPhysicalAI.LocalAI
{
    public sealed class YuiPlatformSpeechLocalAiRuntime : IYuiLocalAiRuntime
    {
        public string RuntimeName => "platform-speech";

        public YuiLocalAiStatus GetStatus()
        {
            return new YuiLocalAiStatus
            {
                Available = YuiPlatformSpeechBridge.IsSupported,
                RuntimeName = RuntimeName,
                Detail = YuiPlatformSpeechBridge.IsSupported
                    ? "On-device platform speech bridge is available."
                    : "On-device platform speech bridge is not available.",
                Capabilities = new[]
                {
                    YuiLocalAiCapability.Transcription,
                    YuiLocalAiCapability.SpeechSynthesis
                }
            };
        }

        public bool Supports(YuiLocalAiCapability capability)
        {
            return YuiPlatformSpeechBridge.IsSupported
                && (capability == YuiLocalAiCapability.Transcription
                    || capability == YuiLocalAiCapability.SpeechSynthesis);
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

        public async Task<YuiLocalAiTranscriptionResponse> TranscribeAsync(YuiLocalAiAudioRequest request, CancellationToken cancellationToken)
        {
            return await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = YuiPlatformSpeechBridge.Transcribe(request);
                    return result.Ok
                        ? new YuiLocalAiTranscriptionResponse
                        {
                            Success = true,
                            Text = YuiLocalTranscriptNormalizer.Normalize(result.Text),
                            Confidence = result.Confidence,
                            ModelId = RuntimeName
                        }
                        : new YuiLocalAiTranscriptionResponse
                        {
                            Success = false,
                            ErrorCode = result.ErrorCode,
                            ErrorMessage = result.ErrorMessage,
                            ModelId = RuntimeName
                        };
                },
                cancellationToken);
        }

        public async Task<YuiLocalAiSpeechResponse> SynthesizeSpeechAsync(YuiLocalAiSpeechRequest request, CancellationToken cancellationToken)
        {
            return await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = YuiPlatformSpeechBridge.Synthesize(request);
                    if (!result.Ok)
                    {
                        return new YuiLocalAiSpeechResponse
                        {
                            Success = false,
                            ErrorCode = result.ErrorCode,
                            ErrorMessage = result.ErrorMessage,
                            ModelId = RuntimeName
                        };
                    }

                    var audioBytes = result.AudioBytes();
                    return new YuiLocalAiSpeechResponse
                    {
                        Success = audioBytes.Length > 44,
                        ErrorCode = audioBytes.Length > 44 ? null : "empty_audio",
                        ErrorMessage = audioBytes.Length > 44 ? null : "Platform TTS returned empty audio.",
                        AudioBytes = audioBytes,
                        MimeType = "audio/wav",
                        SampleRate = result.SampleRate,
                        DurationMs = result.DurationMs > 0 ? result.DurationMs : null,
                        ModelId = RuntimeName
                    };
                },
                cancellationToken);
        }

        public Task<YuiLocalAiVisionResponse> AnalyzeImageAsync(YuiLocalAiVisionRequest request, CancellationToken cancellationToken)
        {
            return Unsupported<YuiLocalAiVisionResponse>(YuiLocalAiCapability.Vision);
        }

        private static Task<TResponse> Unsupported<TResponse>(YuiLocalAiCapability capability)
            where TResponse : YuiLocalAiResponse, new()
        {
            return Task.FromResult(new TResponse
            {
                Success = false,
                ErrorCode = "capability_unavailable",
                ErrorMessage = $"Platform speech runtime does not support {capability}."
            });
        }
    }
}
