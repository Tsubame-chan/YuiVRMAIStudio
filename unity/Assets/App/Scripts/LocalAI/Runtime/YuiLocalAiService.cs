using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace YuiPhysicalAI.LocalAI
{
    public sealed class YuiLocalAiService
    {
        private readonly IYuiLocalAiRuntime runtime;

        public YuiLocalAiService(IYuiLocalAiRuntime runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public YuiLocalAiStatus GetStatus()
        {
            return runtime.GetStatus();
        }

        public bool Supports(YuiLocalAiCapability capability)
        {
            return runtime.Supports(capability);
        }

        public Task WarmAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken)
        {
            if (!runtime.Supports(capability))
            {
                return Task.CompletedTask;
            }

            return runtime.WarmAsync(capability, cancellationToken);
        }

        public async Task<YuiLocalAiChatResponse> ChatAsync(YuiLocalAiChatRequest request, CancellationToken cancellationToken)
        {
            if (!runtime.Supports(YuiLocalAiCapability.Chat))
            {
                return Unavailable<YuiLocalAiChatResponse>();
            }

            return await WithLatency(runtime.ChatAsync(request, cancellationToken));
        }

        public async Task<YuiLocalAiTranscriptionResponse> TranscribeAsync(YuiLocalAiAudioRequest request, CancellationToken cancellationToken)
        {
            if (!runtime.Supports(YuiLocalAiCapability.Transcription))
            {
                return Unavailable<YuiLocalAiTranscriptionResponse>();
            }

            return await WithLatency(runtime.TranscribeAsync(request, cancellationToken));
        }

        public async Task<YuiLocalAiSpeechResponse> SynthesizeSpeechAsync(YuiLocalAiSpeechRequest request, CancellationToken cancellationToken)
        {
            if (!runtime.Supports(YuiLocalAiCapability.SpeechSynthesis))
            {
                return Unavailable<YuiLocalAiSpeechResponse>();
            }

            return await WithLatency(runtime.SynthesizeSpeechAsync(request, cancellationToken));
        }

        public async Task<YuiLocalAiVisionResponse> AnalyzeImageAsync(YuiLocalAiVisionRequest request, CancellationToken cancellationToken)
        {
            if (!runtime.Supports(YuiLocalAiCapability.Vision))
            {
                return Unavailable<YuiLocalAiVisionResponse>();
            }

            return await WithLatency(runtime.AnalyzeImageAsync(request, cancellationToken));
        }

        private static async Task<T> WithLatency<T>(Task<T> task)
            where T : YuiLocalAiResponse
        {
            var timer = Stopwatch.StartNew();
            var response = await task;
            if (response != null)
            {
                response.LatencyMs = timer.ElapsedMilliseconds;
            }

            return response;
        }

        private static T Unavailable<T>()
            where T : YuiLocalAiResponse, new()
        {
            return new T
            {
                Success = false,
                ErrorCode = "capability_unavailable",
                ErrorMessage = "This local AI runtime does not support the requested capability."
            };
        }
    }
}
