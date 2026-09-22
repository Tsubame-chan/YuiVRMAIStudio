using System.Linq;
using NUnit.Framework;
using YuiPhysicalAI.UI;
namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiChatLinkUtilityTests
    {
        [Test]
        public void Parse_PreservesReferenceUrlAndSurroundingText()
        {
            const string source = "参照URL\nhttps://example.com/Guide?id=123&lang=ja\n を確認してください。";
            var result = YuiChatLinkUtility.Parse(source);
            Assert.That(result.DisplayText, Is.EqualTo(source));
            Assert.That(result.Links.Single().Url, Is.EqualTo("https://example.com/Guide?id=123&lang=ja"));
        }
        [Test]
        public void Parse_KeepsMarkdownTitleAndVisibleTarget()
        {
            var result = YuiChatLinkUtility.Parse("候補は[観光案内](https://example.com/event/)です。");
            Assert.That(result.DisplayText, Is.EqualTo("候補は観光案内 (https://example.com/event/)です。"));
            Assert.That(result.Links.Single().Label, Is.EqualTo("観光案内"));
            Assert.That(result.Links.Single().Url, Is.EqualTo("https://example.com/event/"));
        }
        [Test]
        public void Parse_UsesPageTitleFromAnnotationsAfterInlineDomainCitation()
        {
            var parsed = YuiChatLinkUtility.Parse("Found ([example.com](https://example.com/guide)).\nSources\n[Official guide](https://example.com/guide)");
            Assert.That(parsed.Links, Has.Count.EqualTo(1));
            Assert.That(parsed.Links[0].CompactLabel, Is.EqualTo("Official guide"));
        }
        [Test]
        public void Parse_DoesNotRewriteExternal404OrCaseSensitivePaths()
        {
            const string url = "https://developers.openai.com/ja-JP/api/docs/guides/tools-web-search?utm_source=openai";
            var parsed = YuiChatLinkUtility.Parse(url + "\nhttps://example.com/Page\nhttps://example.com/page");
            Assert.That(parsed.Links.Select(x => x.Url), Is.EqualTo(new[] {url,"https://example.com/Page","https://example.com/page"}));
        }
        [Test]
        public void Parse_PreservesCodeWhitespaceAndDoesNotTurnSampleUrlsIntoSources()
        {
            const string code = "```python\nfor x in items:\n    print('https://example.com/demo')\n\tprint('<Text>')\n```\nUse `https://example.com/test` as a sample.";
            var parsed = YuiChatLinkUtility.Parse(code);
            Assert.That(parsed.DisplayText, Is.EqualTo(code));
            Assert.That(parsed.Links, Is.Empty);
        }
        [TestCase("https://en.wikipedia.org/wiki/Test_(assessment)")]
        [TestCase("https://example.com/page?x=a%20b&y=c#section")]
        public void Parse_PreservesBalancedUrlCharacters(string url)
        {
            Assert.That(YuiChatLinkUtility.Parse("[Source]("+url+")").Links.Single().Url, Is.EqualTo(url));
        }
        [Test]
        public void Parse_DoesNotCreateUnsafeLinks()
        {
            Assert.That(YuiChatLinkUtility.Parse("[bad](javascript:alert(1)) [file](file:///tmp/private)").Links, Is.Empty);
        }
        [Test]
        public void Parse_KeepsDomainCitationAndDeduplicates()
        {
            var result = YuiChatLinkUtility.Parse("([example.com]) https://example.com https://example.com");
            Assert.That(result.Links, Has.Count.EqualTo(1));
            Assert.That(result.DisplayText, Does.Contain("[example.com]"));
        }
    }
}
