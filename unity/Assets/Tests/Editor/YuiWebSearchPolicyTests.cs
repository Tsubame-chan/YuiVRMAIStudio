using NUnit.Framework;
using Newtonsoft.Json.Linq;
using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.Tests.Editor
{
    public class YuiWebSearchPolicyTests
    {
        [TestCase("今日の東京の天気を調べて",true)]
        [TestCase("Search the web for Unity release notes",true)]
        [TestCase("こんにちは",false)]
        public void SearchToolsAreOfferedForCurrentInformation(string message,bool enabled)
        {
            var payload=YuiDirectOpenAiClient.BuildResponsesPayload(new ChatRequest{Message=message},"gpt-test");
            Assert.AreEqual(enabled,payload["tools"]!=null);
            Assert.AreEqual(false,payload["store"].Value<bool>());
        }
        [Test]
        public void SourceAnnotationsRemainClickableButAreNotSpoken()
        {
            var annotation=new JObject{["type"]="url_citation",["title"]="Official source",["url"]="https://example.com/news"};
            var response=new JObject{["output"]=new JArray(new JObject{["content"]=new JArray(new JObject{
                ["type"]="output_text",["text"]=new JObject{["text"]="調べた結果です。"}.ToString(),
                ["annotations"]=new JArray(annotation,annotation.DeepClone(),new JObject{["type"]="url_citation",["url"]="file:///private/data"})})})};
            var parsed=YuiDirectOpenAiClient.ParseChatResponse(response.ToString());
            StringAssert.Contains("https://example.com/news",parsed.Text);
            Assert.AreEqual(1,parsed.Text.Split(new[]{"https://example.com/news"},System.StringSplitOptions.None).Length-1);
            StringAssert.DoesNotContain("file:",parsed.Text);
            Assert.AreEqual("調べた結果です。",parsed.SpokenText);
        }
    }
}
