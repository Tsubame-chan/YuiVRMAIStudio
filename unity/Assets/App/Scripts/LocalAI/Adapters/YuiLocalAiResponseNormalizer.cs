using System;
using System.Collections.Generic;
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

        public static YuiLocalAiChatResponse NormalizeChat(YuiLocalAiChatResponse response)
        {
            response = response ?? new YuiLocalAiChatResponse();
            var parsed = TryParseEmbeddedChatJson(response.Text);
            if (parsed != null)
            {
                response.Text = parsed.Text;
                response.Face = parsed.Face;
                response.Animation = parsed.Animation;
                response.VoiceStyle = parsed.VoiceStyle;
                response.ShouldTts = parsed.ShouldTts;
            }

            response.Text = CleanSpokenText(response.Text);
            response.Face = NormalizeFace(response.Face);
            response.Animation = NormalizeAnimation(response.Animation);
            response.VoiceStyle = NormalizeVoiceStyle(response.VoiceStyle);
            response.ShouldTts = !string.IsNullOrWhiteSpace(response.Text) && response.ShouldTts;
            return response;
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
