using System.Text.RegularExpressions;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiLocalTranscriptNormalizer
    {
        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized = Regex.Replace(text.Trim(), @"\s+", "");
            if (HasJapaneseSentencePunctuation(normalized))
            {
                return normalized;
            }

            normalized = Regex.Replace(
                normalized,
                "(しましょう|してください|お願いします|教えてください|見せてください|ですか|でしょうか|ますか|かな)(?=\\S)",
                "$1。");
            normalized = Regex.Replace(normalized, "。+", "。");
            if (!Regex.IsMatch(normalized, "[。！？?]$"))
            {
                normalized += "。";
            }

            return normalized;
        }

        private static bool HasJapaneseSentencePunctuation(string text)
        {
            return Regex.IsMatch(text, "[。！？?]");
        }
    }
}
