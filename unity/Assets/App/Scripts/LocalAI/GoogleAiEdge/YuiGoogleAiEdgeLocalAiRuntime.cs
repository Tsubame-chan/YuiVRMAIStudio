using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace YuiPhysicalAI.LocalAI
{
    public sealed class YuiGoogleAiEdgeLocalAiRuntime : IYuiLocalAiRuntime
    {
        private readonly YuiLocalAiModelRegistry registry;

        public YuiGoogleAiEdgeLocalAiRuntime(YuiLocalAiModelRegistry registry)
        {
            this.registry = registry ?? new YuiLocalAiModelRegistry(Array.Empty<YuiLocalAiModelPack>());
        }

        public string RuntimeName => "litert-lm";

        public YuiLocalAiStatus GetStatus()
        {
            var capabilities = new HashSet<YuiLocalAiCapability>();
            foreach (var pack in registry.Packs)
            {
                if (!IsGoogleAiEdgePack(pack) || pack.Capabilities == null)
                {
                    continue;
                }

                foreach (var capability in pack.Capabilities)
                {
                    capabilities.Add(capability);
                }
            }

            return new YuiLocalAiStatus
            {
                Available = YuiGoogleAiEdgeBridge.IsSupported && capabilities.Count > 0,
                RuntimeName = RuntimeName,
                Detail = YuiGoogleAiEdgeBridge.IsSupported
                    ? "LiteRT-LM bridge is available for this player platform."
                    : "LiteRT-LM bridge is not available in this runtime.",
                Capabilities = capabilities
            };
        }

        public bool Supports(YuiLocalAiCapability capability)
        {
            return CandidatePacks(capability).Any();
        }

        public Task WarmAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken)
        {
            return InvokeControlAsync("warm", capability, cancellationToken);
        }

        public Task ReleaseAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken)
        {
            return InvokeControlAsync("release", capability, cancellationToken);
        }

        public Task<YuiLocalAiChatResponse> ChatAsync(YuiLocalAiChatRequest request, CancellationToken cancellationToken)
        {
            return InvokeAsync<YuiLocalAiChatRequest, YuiLocalAiChatResponse>(
                YuiLocalAiCapability.Chat,
                YuiLocalAiPromptBuilder.PrepareChatRequest(request, compactSystemInstruction: true),
                cancellationToken);
        }

        public Task<YuiLocalAiTranscriptionResponse> TranscribeAsync(YuiLocalAiAudioRequest request, CancellationToken cancellationToken)
        {
            return Unsupported<YuiLocalAiTranscriptionResponse>(YuiLocalAiCapability.Transcription);
        }

        public Task<YuiLocalAiSpeechResponse> SynthesizeSpeechAsync(YuiLocalAiSpeechRequest request, CancellationToken cancellationToken)
        {
            return Unsupported<YuiLocalAiSpeechResponse>(YuiLocalAiCapability.SpeechSynthesis);
        }

        public Task<YuiLocalAiVisionResponse> AnalyzeImageAsync(YuiLocalAiVisionRequest request, CancellationToken cancellationToken)
        {
            return InvokeAsync<YuiLocalAiVisionRequest, YuiLocalAiVisionResponse>(
                YuiLocalAiCapability.Vision,
                request,
                cancellationToken);
        }

        private Task InvokeControlAsync(string action, YuiLocalAiCapability capability, CancellationToken cancellationToken)
        {
            return InvokeAsync<object, YuiLocalAiResponseEnvelope>(
                capability,
                new { action },
                cancellationToken);
        }

        private async Task<TResponse> InvokeAsync<TRequest, TResponse>(
            YuiLocalAiCapability capability,
            TRequest request,
            CancellationToken cancellationToken)
            where TResponse : YuiLocalAiResponse, new()
        {
            var packs = CandidatePacks(capability).ToArray();
            if (packs.Length == 0)
            {
                return await Unsupported<TResponse>(capability);
            }

            TResponse lastFailure = null;
            foreach (var pack in packs)
            {
                string modelPath;
                string cacheDirectory;
                try
                {
                    modelPath = await YuiLocalAiModelPathResolver.EnsureLocalFileAsync(pack, cancellationToken);
                    cacheDirectory = YuiLocalAiModelPathResolver.RuntimeCacheDirectory(pack);
                    Directory.CreateDirectory(cacheDirectory);
                    YuiLocalAiRuntimeCachePruner.PruneForActivePack(pack, cacheDirectory);
                }
                catch (FileNotFoundException ex)
                {
                    lastFailure = new TResponse
                    {
                        Success = false,
                        ErrorCode = "model_file_missing",
                        ErrorMessage = ex.Message,
                        ModelId = pack.ModelId
                    };
                    continue;
                }

                var response = await Task.Run(
                    () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var chatRequest = request as YuiLocalAiChatRequest;
                        var bridgeResponse = YuiGoogleAiEdgeBridge.Invoke(new YuiGoogleAiEdgeBridgeRequest
                        {
                            Capability = capability.ToString(),
                            ModelPackId = pack.Id,
                            ModelPath = modelPath,
                            CacheDirectory = cacheDirectory,
                            RuntimeModelRef = pack.RuntimeModelRef,
                            SystemInstruction = chatRequest?.SystemInstruction,
                            PayloadJson = JsonConvert.SerializeObject(request)
                        });

                        if (!bridgeResponse.Ok)
                        {
                            var errorCode = bridgeResponse.ErrorCode;
                            if (capability == YuiLocalAiCapability.Vision
                                && IsRecoverableVisionBridgeFailure(errorCode))
                            {
                                errorCode = "runtime_unavailable";
                            }

                            return new TResponse
                            {
                                Success = false,
                                ErrorCode = errorCode,
                                ErrorMessage = bridgeResponse.ErrorMessage,
                                ModelId = pack.ModelId
                            };
                        }

                        var success = JsonConvert.DeserializeObject<TResponse>(bridgeResponse.PayloadJson) ?? new TResponse();
                        success.Success = true;
                        success.ModelId = string.IsNullOrWhiteSpace(bridgeResponse.ModelId) ? pack.ModelId : bridgeResponse.ModelId;
                        return success;
                    },
                    cancellationToken);

                if (response.Success || !CanTryNextModelPack(response))
                {
                    return response;
                }

                lastFailure = response;
            }

            return lastFailure ?? await Unsupported<TResponse>(capability);
        }

        private static Task<TResponse> Unsupported<TResponse>(YuiLocalAiCapability capability)
            where TResponse : YuiLocalAiResponse, new()
        {
            return Task.FromResult(new TResponse
            {
                Success = false,
                ErrorCode = "model_pack_unavailable",
                ErrorMessage = $"No enabled LiteRT-LM local model pack supports {capability}."
            });
        }

        private static bool IsGoogleAiEdgePack(YuiLocalAiModelPack pack)
        {
            if (!YuiLocalAiRuntimeFactory.IsOnDeviceEmbeddedPack(pack))
            {
                return false;
            }

            return string.Equals(pack.Provider, "google", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pack.Provider, "google-litert-lm", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pack.Format, "mobile-transformers", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pack.Format, "litert", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pack.Format, "litert-lm", StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<YuiLocalAiModelPack> CandidatePacks(YuiLocalAiCapability capability)
        {
            return registry.EnabledFor(capability).Where(IsGoogleAiEdgePack);
        }

        private static bool CanTryNextModelPack(YuiLocalAiResponse response)
        {
            return string.Equals(response.ErrorCode, "model_file_missing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(response.ErrorCode, "model_unavailable", StringComparison.OrdinalIgnoreCase)
                || string.Equals(response.ErrorCode, "runtime_unavailable", StringComparison.OrdinalIgnoreCase)
                || string.Equals(response.ErrorCode, "runtime_error", StringComparison.OrdinalIgnoreCase)
                || string.Equals(response.ErrorCode, "runtime_timeout", StringComparison.OrdinalIgnoreCase)
                || string.Equals(response.ErrorCode, "litert_lm_error", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRecoverableVisionBridgeFailure(string errorCode)
        {
            return string.Equals(errorCode, "capability_unsupported", StringComparison.OrdinalIgnoreCase)
                || string.Equals(errorCode, "vision_model_unavailable", StringComparison.OrdinalIgnoreCase)
                || string.Equals(errorCode, "litert_lm_error", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class YuiLocalAiResponseEnvelope : YuiLocalAiResponse
        {
        }
    }
}
