using Newtonsoft.Json.Linq;
using NUnit.Framework;
using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiDirectOpenAiClientTests
    {
        [Test]
        public void BuildResponsesPayload_IncludesStructuredSchemaAndAttachedImage()
        {
            var request = new ChatRequest
            {
                Message = "これは何ですか？",
                CharacterName = "Yui",
                CustomInstruction = "少し明るめに話す",
                Context = new RequestContext
                {
                    ScreenContext = "前回の画像には猫が写っていました。",
                    Extra =
                    {
                        ["image_data_url"] = "data:image/jpeg;base64,AAAA",
                        ["image_detail"] = "high"
                    }
                }
            };

            var payload = YuiDirectOpenAiClient.BuildResponsesPayload(request, "gpt-test");

            Assert.AreEqual("gpt-test", payload["model"]?.Value<string>());
            Assert.AreEqual("json_schema", payload["text"]?["format"]?["type"]?.Value<string>());
            Assert.AreEqual(false, payload["text"]?["format"]?["schema"]?["additionalProperties"]?.Value<bool>());
            var content = payload["input"]?[0]?["content"] as JArray;
            Assert.IsNotNull(content);
            Assert.AreEqual("input_text", content[0]?["type"]?.Value<string>());
            Assert.AreEqual("input_image", content[1]?["type"]?.Value<string>());
            Assert.AreEqual("high", content[1]?["detail"]?.Value<string>());
        }

        [Test]
        public void ParseChatResponse_ReadsResponsesOutputTextJson()
        {
            var raw = new JObject
            {
                ["output_text"] = new JObject
                {
                    ["text"] = "猫ちゃんですね。ふわふわで可愛いです。",
                    ["face"] = "Joy",
                    ["animation"] = "happy",
                    ["voice_style"] = "normal",
                    ["should_use_vision"] = false,
                    ["memory_action"] = "none",
                    ["should_tts"] = true
                }.ToString(Newtonsoft.Json.Formatting.None)
            }.ToString(Newtonsoft.Json.Formatting.None);

            var parsed = YuiDirectOpenAiClient.ParseChatResponse(raw);

            Assert.AreEqual("猫ちゃんですね。ふわふわで可愛いです。", parsed.Text);
            Assert.AreEqual("Joy", parsed.Face);
            Assert.IsTrue(parsed.ShouldTts);
        }
    }
}
