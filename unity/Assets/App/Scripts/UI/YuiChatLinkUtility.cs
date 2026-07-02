using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiChatLink
    {
        public YuiChatLink(string label, string url)
        {
            Label = string.IsNullOrWhiteSpace(label) ? "Link" : label.Trim();
            Url = NormalizeUrl(url);
        }

        public string Label { get; }
        public string Url { get; }

        public string CompactLabel
        {
            get
            {
                var label = Label;
                if (string.IsNullOrWhiteSpace(label) || IsDomainLike(label))
                {
                    label = ExtractDomain(Url);
                }

                return label.Length > 32 ? label.Substring(0, 29) + "..." : label;
            }
        }

        private static string NormalizeUrl(string url)
        {
            var trimmed = (url ?? string.Empty).Trim().TrimEnd('.', '。', ',', '、');
            if (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                return "https://" + trimmed;
            }

            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "https://" + trimmed;
            }

            return trimmed;
        }

        private static string ExtractDomain(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                    ? uri.Host.Substring(4)
                    : uri.Host;
            }

            return url;
        }

        internal static bool IsDomainLike(string value)
        {
            return Regex.IsMatch(value ?? string.Empty, @"^(?:[A-Za-z0-9-]+\.)+[A-Za-z]{2,}(?:/.*)?$");
        }
    }

    public sealed class YuiChatLinkParseResult
    {
        public YuiChatLinkParseResult(string displayText, IReadOnlyList<YuiChatLink> links)
        {
            DisplayText = displayText ?? string.Empty;
            Links = links ?? Array.Empty<YuiChatLink>();
        }

        public string DisplayText { get; }
        public IReadOnlyList<YuiChatLink> Links { get; }
    }

    public static class YuiChatLinkUtility
    {
        private static readonly Regex ParenthesizedMarkdownCitationRegex = new Regex(
            @"\(\s*\[(?<label>[^\]]+)\]\((?<url>(?:https?://|www\.)[^\s)]+)\)\s*\)",
            RegexOptions.IgnoreCase);

        private static readonly Regex MarkdownLinkRegex = new Regex(
            @"\[(?<label>[^\]]+)\]\((?<url>(?:https?://|www\.)[^\s)]+)\)",
            RegexOptions.IgnoreCase);

        private static readonly Regex RawUrlRegex = new Regex(
            @"(?:https?://|www\.)[^\s　)）】」』]+",
            RegexOptions.IgnoreCase);

        private static readonly Regex DomainCitationRegex = new Regex(
            @"\(\s*\[?\s*(?<domain>(?:[A-Za-z0-9-]+\.)+[A-Za-z]{2,}(?:/[^\]\s)）]*)?)\s*\]?\s*\)",
            RegexOptions.IgnoreCase);

        private static readonly Regex BracketDomainRegex = new Regex(
            @"\[\s*(?<domain>(?:[A-Za-z0-9-]+\.)+[A-Za-z]{2,}(?:/[^\]\s)）]*)?)\s*\]",
            RegexOptions.IgnoreCase);

        public static YuiChatLinkParseResult Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new YuiChatLinkParseResult(string.Empty, Array.Empty<YuiChatLink>());
            }

            var links = new List<YuiChatLink>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var display = text;

            display = ParenthesizedMarkdownCitationRegex.Replace(display, match =>
            {
                AddLink(links, seenUrls, match.Groups["label"].Value, match.Groups["url"].Value);
                return string.Empty;
            });

            display = MarkdownLinkRegex.Replace(display, match =>
            {
                AddLink(links, seenUrls, match.Groups["label"].Value, match.Groups["url"].Value);
                var label = match.Groups["label"].Value;
                return YuiChatLink.IsDomainLike(label) ? string.Empty : label;
            });

            display = DomainCitationRegex.Replace(display, match =>
            {
                AddLink(links, seenUrls, match.Groups["domain"].Value, match.Groups["domain"].Value);
                return string.Empty;
            });

            display = BracketDomainRegex.Replace(display, match =>
            {
                AddLink(links, seenUrls, match.Groups["domain"].Value, match.Groups["domain"].Value);
                return string.Empty;
            });

            display = RawUrlRegex.Replace(display, match =>
            {
                AddLink(links, seenUrls, ExtractReadableLabel(match.Value), match.Value);
                return string.Empty;
            });

            display = YuiSpeechTextUtility.CleanDisplayText(display);
            display = Regex.Replace(display, @"[ \t]{2,}", " ").Trim();
            return new YuiChatLinkParseResult(display, links);
        }

        private static void AddLink(
            ICollection<YuiChatLink> links,
            ISet<string> seenUrls,
            string label,
            string url)
        {
            var link = new YuiChatLink(label, url);
            if (string.IsNullOrWhiteSpace(link.Url) || !seenUrls.Add(link.Url))
            {
                return;
            }

            links.Add(link);
        }

        private static string ExtractReadableLabel(string url)
        {
            if (Uri.TryCreate(new YuiChatLink("Link", url).Url, UriKind.Absolute, out var uri)
                && !string.IsNullOrWhiteSpace(uri.Host))
            {
                return uri.Host;
            }

            return "Link";
        }
    }
}
