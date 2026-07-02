using System.Threading;
using System.Threading.Tasks;

namespace YuiPhysicalAI.LocalAI
{
    public interface IYuiLocalAiRuntime
    {
        string RuntimeName { get; }
        YuiLocalAiStatus GetStatus();
        bool Supports(YuiLocalAiCapability capability);
        Task WarmAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken);
        Task ReleaseAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken);
        Task<YuiLocalAiChatResponse> ChatAsync(YuiLocalAiChatRequest request, CancellationToken cancellationToken);
        Task<YuiLocalAiTranscriptionResponse> TranscribeAsync(YuiLocalAiAudioRequest request, CancellationToken cancellationToken);
        Task<YuiLocalAiSpeechResponse> SynthesizeSpeechAsync(YuiLocalAiSpeechRequest request, CancellationToken cancellationToken);
        Task<YuiLocalAiVisionResponse> AnalyzeImageAsync(YuiLocalAiVisionRequest request, CancellationToken cancellationToken);
    }
}
