using System;
using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiLocalAiBackendCompatibility
    {
        public static ChatResponse ToChatResponse(YuiLocalAiChatResponse response, string mode = null)
        {
            var workMode = string.Equals(mode, "work", StringComparison.OrdinalIgnoreCase);
            response = YuiLocalAiResponseNormalizer.NormalizeChat(response, workMode);
            return new ChatResponse
            {
                Text = response.Text ?? string.Empty,
                SpokenText = workMode ? WorkSpeech(response.Text) : response.Text,
                Face = response.Face,
                Animation = response.Animation,
                VoiceStyle = response.VoiceStyle,
                ShouldTts = response.ShouldTts
            };
        }

        private static string WorkSpeech(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Contains("```") || text.TrimStart().StartsWith("{"))
                return "作業結果を画面にまとめたよ。";
            var firstLine = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
            var end = firstLine.IndexOfAny(new[] { '。', '！', '？', '!', '?' });
            if (end >= 0) firstLine = firstLine.Substring(0, end + 1);
            return firstLine.Length <= 120 ? firstLine : "作業結果を画面にまとめたよ。";
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
