using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace YuiPhysicalAI.Api
{
    public static class YuiWebSearchPolicy
    {
        private static readonly string[] Triggers = {
            "検索", "調べ", "今日", "明日", "現在", "最新", "直近", "今月", "来月", "ニュース",
            "天気", "雨", "気温", "台風", "地図", "場所", "近く", "行き方", "営業時間", "価格",
            "株価", "為替", "発売日", "スケジュール", "祭り", "花火", "イベント", "開催",
            "search", "look up", "lookup", "weather", "forecast", "news", "latest", "current",
            "upcoming", "event", "festival", "near me", "map", "directions", "price"
        };

        public static bool ShouldOffer(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;
            foreach (var trigger in Triggers)
                if (message.IndexOf(trigger, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // Preserve provider citations outside the model's JSON. The existing chat
        // link renderer makes these sources clickable; spoken_text stays unchanged.
        public static string AppendCitations(string text, JObject response)
        {
            var sources = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in response.SelectTokens("output[*].content[*].annotations[*]"))
            {
                if ((string)token["type"] != "url_citation") continue;
                var url = (string)token["url"];
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    || (uri.Scheme != "http" && uri.Scheme != "https") || !seen.Add(url)) continue;
                var title = ((string)token["title"] ?? uri.Host).Replace("\n", " ").Replace("\r", " ");
                title = title.Replace("[", "(").Replace("]", ")");
                sources.Add("[" + title + "](" + url.Replace("(", "%28").Replace(")", "%29") + ")");
            }
            return sources.Count == 0 ? text : text + "\n\nSources\n" + string.Join("\n\n", sources);
        }
    }
}
