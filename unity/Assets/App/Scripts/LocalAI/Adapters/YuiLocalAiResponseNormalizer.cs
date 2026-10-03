using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiLocalAiResponseNormalizer
    {
        private static readonly HashSet<string> Faces = new HashSet<string>(StringComparer.Ordinal)
        {
            "Neutral",
            "Joy",
            "Fun",
            "Angry",
            "Sorrow",
            "Surprised"
        };

        private static readonly HashSet<string> Animations = new HashSet<string>(StringComparer.Ordinal)
        {
            "idle_normal",
            "idle_relaxed",
            "nod_small",
            "nod_big",
            "wave_small",
            "wave_big",
            "thinking",
            "surprised_body",
            "happy_body",
            "troubled_body",
            "proud_pose",
            "tsukkomi_point",
            "look_away",
            "talk_gesture_small"
        };

        public static YuiLocalAiChatResponse NormalizeChat(YuiLocalAiChatResponse response, bool workMode = false)
        {
            response = response ?? new YuiLocalAiChatResponse();
            response.Text = ExtractFinalAnswer(response.Text);
            var parsed = workMode ? null : TryParseEmbeddedChatJson(response.Text);
            if (parsed != null)
            {
                response.Text = parsed.Text;
                response.Face = parsed.Face;
                response.Animation = parsed.Animation;
                response.VoiceStyle = parsed.VoiceStyle;
                response.ShouldTts = parsed.ShouldTts;
            }

            response.Text = ExtractFinalAnswer(response.Text);
            response.Text = workMode ? (response.Text ?? string.Empty).Trim() : CleanSpokenText(response.Text);
            response.Face = NormalizeFace(response.Face);
            response.Animation = NormalizeAnimation(response.Animation);
            response.VoiceStyle = NormalizeVoiceStyle(response.VoiceStyle);
            response.ShouldTts = !string.IsNullOrWhiteSpace(response.Text) && response.ShouldTts;
            return response;
        }

        // LiteRT-LM normally separates the thought channel. Some model/runtime
        // responses still contain Gemma's serialized boundary in ordinary text.
        // Filter before JSON parsing, display, speech and conversation storage.
        public static string ExtractFinalAnswer(string value)
        {
            var text = (value ?? string.Empty).Trim();
            // Preserve literal protocol examples in code and structured answers.
            if (text.StartsWith("```", StringComparison.Ordinal) || text.StartsWith("{", StringComparison.Ordinal))
                return text;

            const string channelEnd = "<channel|>";
            var boundary = text.IndexOf(channelEnd, StringComparison.Ordinal);
            if (boundary >= 0)
            {
                var payload = text.Substring(boundary + channelEnd.Length).Trim();
                if (payload.StartsWith("```", StringComparison.Ordinal) || payload.StartsWith("{", StringComparison.Ordinal))
                    return payload;
            }
            var finalHeaders = Regex.Matches(text, @"<\|channel>(?:final|answer)\b\s*", RegexOptions.IgnoreCase);
            var finalStart = boundary < 0 ? -1 : boundary + channelEnd.Length;
            foreach (Match header in finalHeaders)
                finalStart = Math.Max(finalStart, header.Index + header.Length);
            if (finalStart >= 0)
                text = text.Substring(finalStart).Trim();
            else if (Regex.IsMatch(text, @"<\|channel>(?:thought|analysis)\b", RegexOptions.IgnoreCase))
                return string.Empty; // No completed answer: never speak partial reasoning.

            while (text.StartsWith("<think>", StringComparison.OrdinalIgnoreCase))
            {
                var end = text.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
                if (end < 0) return string.Empty;
                text = text.Substring(end + "</think>".Length).Trim();
            }
            const string turnEnd = "<end_of_turn>";
            if (text.EndsWith(turnEnd, StringComparison.Ordinal))
                text = text.Substring(0, text.Length - turnEnd.Length).TrimEnd();
            return text;
        }

        public static string NormalizeFace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Neutral";
            }

            var trimmed = value.Trim();
            foreach (var face in Faces)
            {
                if (string.Equals(trimmed, face, StringComparison.OrdinalIgnoreCase))
                {
                    return face;
                }
            }

            return "Neutral";
        }

        public static string NormalizeAnimation(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "idle_normal";
            }

            var trimmed = value.Trim();
            if (string.Equals(trimmed, "idle", StringComparison.OrdinalIgnoreCase))
            {
                return "idle_normal";
            }

            if (string.Equals(trimmed, "wave", StringComparison.OrdinalIgnoreCase))
            {
                return "wave_small";
            }

            if (string.Equals(trimmed, "nod", StringComparison.OrdinalIgnoreCase))
            {
                return "nod_small";
            }

            foreach (var animation in Animations)
            {
                if (string.Equals(trimmed, animation, StringComparison.OrdinalIgnoreCase))
                {
                    return animation;
                }
            }

            return "idle_normal";
        }

        public static string NormalizeVoiceStyle(string value)
        {
            var trimmed = (value ?? string.Empty).Trim().ToLowerInvariant();
            return trimmed == "excited" || trimmed == "sad" ? trimmed : "normal";
        }

        public static string CleanSpokenText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var text = value.Trim();
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var jsonStart = text.IndexOf('{');
            if (jsonStart > 0)
            {
                text = text.Substring(0, jsonStart).Trim();
            }
            else if (jsonStart == 0)
            {
                return string.Empty;
            }

            return text
                .Replace("**", string.Empty)
                .Replace("`", string.Empty)
                .Trim();
        }

        private static YuiLocalAiChatResponse TryParseEmbeddedChatJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return null;
            }

            try
            {
                var json = raw.Substring(start, end - start + 1);
                var value = JObject.Parse(json);
                var text = value.Value<string>("text");
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                return new YuiLocalAiChatResponse
                {
                    Success = true,
                    Text = text,
                    Face = value.Value<string>("face"),
                    Animation = value.Value<string>("animation"),
                    VoiceStyle = value.Value<string>("voice_style"),
                    ShouldTts = value.Value<bool?>("should_tts") ?? true
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
