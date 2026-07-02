using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public static class YuiSpeechTextUtility
    {
        public static string[] SplitSpeechText(string text, int maxCharacters)
        {
            return SplitSpeechText(text, maxCharacters, 12, 24);
        }

        public static string[] SplitSpeechText(
            string text,
            int maxCharacters,
            int firstBoundaryMinCharacters,
            int nextBoundaryMinCharacters)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            maxCharacters = Mathf.Max(30, maxCharacters);
            firstBoundaryMinCharacters = Mathf.Clamp(firstBoundaryMinCharacters, 1, maxCharacters);
            nextBoundaryMinCharacters = Mathf.Clamp(nextBoundaryMinCharacters, 1, maxCharacters);
            var normalized = Regex.Replace(text.Trim(), @"\s+", " ");
            var chunks = new List<string>();
            var builder = new StringBuilder();
            foreach (var character in normalized)
            {
                builder.Append(character);
                var minBoundaryLength = chunks.Count == 0 ? firstBoundaryMinCharacters : nextBoundaryMinCharacters;
                var shouldBreak = IsSpeechBoundary(character) && builder.Length >= minBoundaryLength;
                if (builder.Length >= maxCharacters || shouldBreak)
                {
                    chunks.Add(builder.ToString().Trim());
                    builder.Length = 0;
                }
            }

            if (builder.Length > 0)
            {
                chunks.Add(builder.ToString().Trim());
            }

            return chunks.ToArray();
        }

        public static string CleanDisplayText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var cleaned = CleanMarkup(text);
            return Regex.Replace(cleaned, @"[ \t]{2,}", " ").Trim();
        }

        public static string CleanSpeechText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var cleaned = Regex.Replace(
                text,
                @"\[([^\]]+)\]\((?:https?://|www\.)[^\s)]+\)",
                "$1",
                RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(
                cleaned,
                @"(?:https?://|www\.)[^\s　)）】」』]+",
                "",
                RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(
                cleaned,
                @"\(\s*\[?\s*(?:[A-Za-z0-9-]+\.)+[A-Za-z]{2,}(?:/[^\]\s)）]*)?\s*\]?\s*\)",
                "",
                RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(
                cleaned,
                @"\[?\s*(?:[A-Za-z0-9-]+\.)+[A-Za-z]{2,}(?:/[^\]\s)）]*)?\s*\]?",
                "",
                RegexOptions.IgnoreCase);
            cleaned = CleanMarkup(cleaned);
            cleaned = Regex.Replace(cleaned, @"^[\s\-・*]+", "", RegexOptions.Multiline);
            cleaned = Regex.Replace(cleaned, @"[【】「」『』（）()]", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            cleaned = NormalizeCharacterNameForSpeech(cleaned);
            return cleaned.Trim();
        }

        private static bool IsSpeechBoundary(char character)
        {
            return character == '。'
                || character == '！'
                || character == '？'
                || character == '\n'
                || character == '.'
                || character == '!'
                || character == '?';
        }

        private static string CleanMarkup(string text)
        {
            var cleaned = Regex.Replace(
                text,
                @"\[[^\]]*(?:face|anim)\s*[:=][^\]]*\]",
                "",
                RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\*\*(.+?)\*\*", "$1");
            cleaned = Regex.Replace(cleaned, @"__(.+?)__", "$1");
            cleaned = Regex.Replace(cleaned, @"`([^`]+)`", "$1");
            cleaned = Regex.Replace(cleaned, @"^\s*[-*]\s+", "", RegexOptions.Multiline);
            return cleaned;
        }

        private static string NormalizeCharacterNameForSpeech(string text)
        {
            return Regex.Replace(
                text,
                @"(?<![A-Za-z])Yui(?![A-Za-z])",
                "ユイ",
                RegexOptions.IgnoreCase);
        }
    }
}
