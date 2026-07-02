using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YuiPhysicalAI.LocalAI
{
    public sealed class YuiCompositeLocalAiRuntime : IYuiLocalAiRuntime
    {
        private readonly IReadOnlyList<IYuiLocalAiRuntime> runtimes;

        public YuiCompositeLocalAiRuntime(IEnumerable<IYuiLocalAiRuntime> runtimes)
        {
            this.runtimes = (runtimes ?? Array.Empty<IYuiLocalAiRuntime>())
                .Where(runtime => runtime != null)
                .ToArray();
        }

        public string RuntimeName => "composite-local-ai";

        public YuiLocalAiStatus GetStatus()
        {
            var statuses = runtimes.Select(runtime => runtime.GetStatus()).ToArray();
            var capabilities = new HashSet<YuiLocalAiCapability>();
            foreach (var status in statuses)
            {
                if (status.Capabilities == null)
                {
                    continue;
                }

                foreach (var capability in status.Capabilities)
                {
                    capabilities.Add(capability);
                }
            }

            return new YuiLocalAiStatus
            {
                Available = statuses.Any(status => status.Available),
                RuntimeName = RuntimeName,
                Detail = string.Join(
                    " | ",
                    statuses.Select(status => $"{status.RuntimeName}: {status.Detail}")),
                Capabilities = capabilities
            };
        }

        public bool Supports(YuiLocalAiCapability capability)
        {
            return runtimes.Any(runtime => runtime.Supports(capability));
        }

        public Task WarmAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken)
        {
            return Task.WhenAll(runtimes
                .Where(runtime => runtime.Supports(capability))
                .Select(runtime => runtime.WarmAsync(capability, cancellationToken)));
        }

        public Task ReleaseAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken)
        {
            return Task.WhenAll(runtimes
                .Where(runtime => runtime.Supports(capability))
                .Select(runtime => runtime.ReleaseAsync(capability, cancellationToken)));
        }

        public Task<YuiLocalAiChatResponse> ChatAsync(YuiLocalAiChatRequest request, CancellationToken cancellationToken)
        {
            return Invoke(
                YuiLocalAiCapability.Chat,
                runtime => runtime.ChatAsync(request, cancellationToken));
        }

        public Task<YuiLocalAiTranscriptionResponse> TranscribeAsync(YuiLocalAiAudioRequest request, CancellationToken cancellationToken)
        {
            return Invoke(
                YuiLocalAiCapability.Transcription,
                runtime => runtime.TranscribeAsync(request, cancellationToken));
        }

        public Task<YuiLocalAiSpeechResponse> SynthesizeSpeechAsync(YuiLocalAiSpeechRequest request, CancellationToken cancellationToken)
        {
            return Invoke(
                YuiLocalAiCapability.SpeechSynthesis,
                runtime => runtime.SynthesizeSpeechAsync(request, cancellationToken));
        }

        public Task<YuiLocalAiVisionResponse> AnalyzeImageAsync(YuiLocalAiVisionRequest request, CancellationToken cancellationToken)
        {
            return Invoke(
                YuiLocalAiCapability.Vision,
                runtime => runtime.AnalyzeImageAsync(request, cancellationToken));
        }

        private async Task<TResponse> Invoke<TResponse>(
            YuiLocalAiCapability capability,
            Func<IYuiLocalAiRuntime, Task<TResponse>> invoke)
            where TResponse : YuiLocalAiResponse, new()
        {
            var candidates = runtimes
                .Where(candidate => candidate.Supports(capability))
                .ToArray();
            if (candidates.Length == 0)
            {
                return new TResponse
                {
                    Success = false,
                    ErrorCode = "capability_unavailable",
                    ErrorMessage = $"No local AI runtime supports {capability}."
                };
            }

            TResponse lastFailure = null;
            foreach (var runtime in candidates)
            {
                var response = await invoke(runtime);
                if (response == null)
                {
                    lastFailure = new TResponse
                    {
                        Success = false,
                        ErrorCode = "runtime_unavailable",
                        ErrorMessage = $"{runtime.RuntimeName} returned no response."
                    };
                    continue;
                }

                if (response.Success || !CanTryNextRuntime(response))
                {
                    return response;
                }

                lastFailure = response;
            }

            return lastFailure ?? new TResponse
            {
                Success = false,
                ErrorCode = "runtime_unavailable",
                ErrorMessage = $"No local AI runtime could complete {capability}."
            };
        }

        private static bool CanTryNextRuntime(YuiLocalAiResponse response)
        {
            return string.Equals(response.ErrorCode, "runtime_unavailable", StringComparison.OrdinalIgnoreCase)
                || string.Equals(response.ErrorCode, "capability_unavailable", StringComparison.OrdinalIgnoreCase)
                || string.Equals(response.ErrorCode, "model_unavailable", StringComparison.OrdinalIgnoreCase)
                || string.Equals(response.ErrorCode, "model_file_missing", StringComparison.OrdinalIgnoreCase);
        }
    }
}
