using System.Linq;
using NUnit.Framework;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiChatLinkUtilityTests
    {
        [Test]
        public void Parse_CollapsesRawUrlsFromDisplayText()
        {
            var parsed = YuiChatLinkUtility.Parse(
                "詳細はこちらです。https://example.com/events?id=123 を確認してください。");

            Assert.That(parsed.DisplayText, Does.Not.Contain("https://"));
            Assert.That(parsed.DisplayText, Is.EqualTo("詳細はこちらです。 を確認してください。"));
            Assert.That(parsed.Links.Select(link => link.Url), Is.EquivalentTo(new[] { "https://example.com/events?id=123" }));
        }

        [Test]
        public void Parse_KeepsMarkdownLabelAndExtractsLink()
        {
            var parsed = YuiChatLinkUtility.Parse(
                "候補は[中央区観光協会](https://www.chuo-kanko.or.jp/event/)です。");

            Assert.That(parsed.DisplayText, Is.EqualTo("候補は中央区観光協会です。"));
            Assert.That(parsed.Links, Has.Count.EqualTo(1));
            Assert.That(parsed.Links[0].Label, Is.EqualTo("中央区観光協会"));
            Assert.That(parsed.Links[0].Url, Is.EqualTo("https://www.chuo-kanko.or.jp/event/"));
        }

        [Test]
        public void Parse_ConvertsDomainOnlyCitationToCompactLink()
        {
            var parsed = YuiChatLinkUtility.Parse(
                "中央区で開催予定です。([chuo-kanko.or.jp])");

            Assert.That(parsed.DisplayText, Is.EqualTo("中央区で開催予定です。"));
            Assert.That(parsed.Links, Has.Count.EqualTo(1));
            Assert.That(parsed.Links[0].Label, Is.EqualTo("chuo-kanko.or.jp"));
            Assert.That(parsed.Links[0].Url, Is.EqualTo("https://chuo-kanko.or.jp"));
        }

        [Test]
        public void Parse_DeduplicatesLinks()
        {
            var parsed = YuiChatLinkUtility.Parse(
                "https://example.com を見てください。もう一度 https://example.com です。");

            Assert.That(parsed.Links, Has.Count.EqualTo(1));
        }

        [Test]
        public void Parse_TreatsParenthesizedMarkdownDomainCitationAsOneLink()
        {
            var parsed = YuiChatLinkUtility.Parse(
                "案内されています。([chuo-kanko.or.jp](https://www.chuo-kanko.or.jp/pages/seasonal_events?utm_source=openai))");

            Assert.That(parsed.DisplayText, Is.EqualTo("案内されています。"));
            Assert.That(parsed.Links, Has.Count.EqualTo(1));
            Assert.That(parsed.Links[0].Url, Is.EqualTo("https://www.chuo-kanko.or.jp/pages/seasonal_events?utm_source=openai"));
        }
    }
}
