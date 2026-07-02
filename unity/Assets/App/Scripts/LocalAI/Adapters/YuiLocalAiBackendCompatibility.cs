using System;
using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiLocalAiBackendCompatibility
    {
        public static ChatResponse ToChatResponse(YuiLocalAiChatResponse response)
        {
            response = YuiLocalAiResponseNormalizer.NormalizeChat(response);
            return new ChatResponse
            {
                Text = response.Text ?? string.Empty,
                Face = response.Face,
                Animation = response.Animation,
                VoiceStyle = response.VoiceStyle,
                ShouldTts = response.ShouldTts
            };
        }

        public static SttResponse ToSttResponse(YuiLocalAiTranscriptionResponse response)
        {
            response = response ?? new YuiLocalAiTranscriptionResponse();
            return new SttResponse
            {
                Text = response.Text ?? string.Empty,
                Confidence = response.Confidence
            };
        }

        public static VisionResponse ToVisionResponse(YuiLocalAiVisionResponse response)
        {
            response = response ?? new YuiLocalAiVisionResponse();
            return new VisionResponse
            {
                VisionResultId = string.IsNullOrWhiteSpace(response.VisionResultId)
                    ? Guid.NewGuid().ToString("N")
                    : response.VisionResultId,
                Summary = response.Summary ?? string.Empty,
                Structured = new VisionStructured(),
                CreatedAt = DateTimeOffset.UtcNow.ToString("O")
            };
        }
    }
}
