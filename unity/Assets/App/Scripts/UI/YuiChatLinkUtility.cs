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
                    if (Uri.TryCreate(Url, UriKind.Absolute, out var uri) && uri.AbsolutePath != "/")
                        label += " · " + Uri.UnescapeDataString(uri.AbsolutePath.TrimEnd('/').Substring(uri.AbsolutePath.TrimEnd('/').LastIndexOf('/') + 1));
                }

                return label.Length > 76 ? label.Substring(0, 73) + "..." : label;
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
        // Code samples are content, not navigation. Keep their bytes and indentation intact.
        private static readonly Regex CodeRegex = new Regex(@"```[\s\S]*?(?:```|$)|~~~[\s\S]*?(?:~~~|$)|`[^`\r\n]+`");
        private static readonly Regex MarkdownLinkRegex = new Regex(
            @"\[(?<label>[^\]\r\n]+)\]\((?<url>(?:https?://|www\.)(?:[^\s()]+|\([^\s()]*\))+)\)", RegexOptions.IgnoreCase);
        private static readonly Regex RawUrlRegex = new Regex(
            @"(?:https?://|www\.)(?:[^\s　<>""`\[\]()）】」』、。]+|\([^\s()]*\))+", RegexOptions.IgnoreCase);
        private static readonly Regex BracketDomainRegex = new Regex(
            @"\[\s*(?<domain>(?:[A-Za-z0-9-]+\.)+[A-Za-z]{2,}(?:/[^\]\s]*)?)\s*\]", RegexOptions.IgnoreCase);

        public static YuiChatLinkParseResult Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return new YuiChatLinkParseResult(string.Empty, Array.Empty<YuiChatLink>());
            var links = new List<YuiChatLink>();
            var seenUrls = new HashSet<string>(StringComparer.Ordinal);
            var output = new System.Text.StringBuilder();
            var position = 0;
            foreach (Match code in CodeRegex.Matches(text))
            {
                output.Append(ParseProse(text.Substring(position, code.Index - position), links, seenUrls));
                output.Append(code.Value);
                position = code.Index + code.Length;
            }
            output.Append(ParseProse(text.Substring(position), links, seenUrls));
            return new YuiChatLinkParseResult(output.ToString(), links);
        }

        private static string ParseProse(string text, IList<YuiChatLink> links, ISet<string> seenUrls)
        {
            var display = MarkdownLinkRegex.Replace(text, match =>
            {
                var label = match.Groups["label"].Value;
                var url = match.Groups["url"].Value;
                AddLink(links, seenUrls, label, url);
                return label + " (" + url + ")";
            });
            foreach (Match match in RawUrlRegex.Matches(display))
                AddLink(links, seenUrls, ExtractReadableLabel(match.Value), match.Value);
            foreach (Match match in BracketDomainRegex.Matches(display))
                AddLink(links, seenUrls, match.Groups["domain"].Value, match.Groups["domain"].Value);
            // Never apply speech cleanup to screen/copy/save content or remove references.
            return display;
        }

        private static void AddLink(
            IList<YuiChatLink> links,
            ISet<string> seenUrls,
            string label,
            string url)
        {
            var link = new YuiChatLink(label, url);
            if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                || string.IsNullOrEmpty(uri.Host)) return;
            if (!seenUrls.Add(link.Url))
            {
                // A provider annotation may arrive after a domain-only inline citation.
                // Prefer the actual page title while retaining one link per exact URL.
                for (var i = 0; i < links.Count; i++)
                    if (string.Equals(links[i].Url, link.Url, StringComparison.Ordinal)
                        && (YuiChatLink.IsDomainLike(links[i].Label) || links[i].Label == "Link")
                        && !YuiChatLink.IsDomainLike(link.Label) && link.Label != "Link")
                        links[i] = link;
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
