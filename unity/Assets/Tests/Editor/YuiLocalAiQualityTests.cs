using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiLocalAiQualityTests
    {
        [TestCase("Thinking Process: determine a concise response.<channel|>こんにちは。", "こんにちは。")]
        [TestCase("<|channel>thought\nprivate reasoning<channel|>こんにちは。<end_of_turn>", "こんにちは。")]
        [TestCase("<|channel>analysis\nprivate reasoning<|channel>final\nこんにちは。", "こんにちは。")]
        [TestCase("<think>private reasoning</think>こんにちは。", "こんにちは。")]
        [TestCase("こんにちは。", "こんにちは。")]
        public void ReasoningNeverReachesDisplayedSpokenOrStoredAnswer(string raw, string expected)
        {
            foreach (var mode in new[] { "talk", "work" })
            {
                var result = YuiLocalAiBackendCompatibility.ToChatResponse(
                    new YuiLocalAiChatResponse { Success = true, Text = raw, ShouldTts = true }, mode);
                Assert.AreEqual(expected, result.Text, mode);
                Assert.AreEqual(expected, result.SpokenText, mode);
            }
        }

        [TestCase("<|channel>thought\nunfinished reasoning")]
        [TestCase("private reasoning<channel|>")]
        [TestCase("<think>unfinished reasoning")]
        public void IncompleteReasoningIsAnActionableFailureInsteadOfAnEmptySuccess(string raw)
        {
            var error = Assert.Throws<InvalidOperationException>(() => YuiLocalAiBackendCompatibility.ToChatResponse(
                new YuiLocalAiChatResponse { Success = true, Text = raw, ShouldTts = true }));
            StringAssert.Contains("初期値", error.Message);
        }

        [Test]
        public void WorkCodeAndStructuredFinalAnswerSurviveReasoningRemoval()
        {
            var code = "```python\nprint('<channel|>')\n```";
            var work = YuiLocalAiBackendCompatibility.ToChatResponse(
                new YuiLocalAiChatResponse { Text = "reasoning<channel|>" + code }, "work");
            // The inner code marker is literal content, not another channel.
            Assert.AreEqual(code, work.Text);
            var structured = YuiLocalAiBackendCompatibility.ToChatResponse(new YuiLocalAiChatResponse {
                Text = "reasoning<channel|>{\"text\":\"こんにちは。\",\"face\":\"Joy\"}" });
            Assert.AreEqual("こんにちは。", structured.Text);
            Assert.AreEqual("Joy", structured.Face);
        }

        [Test]
        public void ChatAndVisionSharingAFileShareCompiledModelCache()
        {
            var chat = new YuiLocalAiModelPack { Id = "chat", RuntimeModelRef = "gemma-4-E4B-it.litertlm" };
            var vision = new YuiLocalAiModelPack { Id = "vision", RuntimeModelRef = chat.RuntimeModelRef };
            Assert.AreEqual(YuiLocalAiModelPathResolver.RuntimeCacheDirectory(chat), YuiLocalAiModelPathResolver.RuntimeCacheDirectory(vision));
            vision.RuntimeModelRef = "gemma-4-E2B-it.litertlm";
            Assert.AreNotEqual(YuiLocalAiModelPathResolver.RuntimeCacheDirectory(chat), YuiLocalAiModelPathResolver.RuntimeCacheDirectory(vision));
        }
        [Test]
        public void CachePruningPreservesMainDraftAndUnknownComponents()
        {
            var dir = Path.Combine(Path.GetTempPath(), "yui-cache-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var old = Path.Combine(dir, "gemma.litertlm_100_200_mldrift_weight_cache.bin");
                File.WriteAllText(old, "old"); File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-1));
                var keep = new[] { "gemma.litertlm_101_200_mldrift_weight_cache.bin",
                    "gemma.litertlm_101_200_mldrift_program_cache.bin",
                    "gemma.litertlm_101_200.mtp_drafter_mldrift_weight_cache.bin",
                    "gemma.litertlm_101_200.mtp_drafter_mldrift_program_cache.bin", "audio-cache.bin" };
                foreach (var name in keep) File.WriteAllText(Path.Combine(dir, name), "required");
                typeof(YuiLocalAiRuntimeCachePruner).GetMethod("PruneActiveDirectory", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { dir });
                Assert.IsFalse(File.Exists(old));
                foreach (var name in keep) Assert.IsTrue(File.Exists(Path.Combine(dir, name)), name);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void StructuredDialoguePreservesRolesWithoutRepeatingHistoryInCurrentInput()
        {
            var source = new YuiLocalAiChatRequest {
                Message = "どこに行く予定だっけ？", CharacterName = "ユイ",
                Extra = new Dictionary<string, object> { [YuiCharacterDialogueStore.ContextKey] =
                    "[{\"User\":\"明日は図書館に行く\",\"Assistant\":\"本を探すのが楽しみだね\"}]" }
            };
            var request = YuiLocalAiPromptBuilder.PrepareChatRequest(source, true);
            Assert.AreEqual(source.Message, request.Input);
            Assert.AreEqual(2, request.History.Count);
            Assert.AreEqual("user", request.History[0].Role);
            Assert.AreEqual("assistant", request.History[1].Role);
            StringAssert.Contains("図書館", request.History[0].Content);
            var json = JsonConvert.SerializeObject(request);
            StringAssert.Contains("\"role\":\"assistant\"", json);
            Assert.IsNull(source.Input); // Preparing a request does not mutate stored user data.
        }

        [Test]
        public void ContextWindowKeepsRecentCompleteTurnsAndNeverRewritesSavedHistory()
        {
            var turns = new List<YuiCharacterDialogueStore.Turn>();
            for (var i = 0; i < 4; i++) turns.Add(new YuiCharacterDialogueStore.Turn {
                User = i + new string('あ', 599), Assistant = new string('い', 600) });
            var json = JsonConvert.SerializeObject(turns);
            var request = new YuiLocalAiChatRequest { Message = "続けて", Extra = new Dictionary<string, object> {
                [YuiCharacterDialogueStore.ContextKey] = json } };
            var prepared = YuiLocalAiPromptBuilder.PrepareChatRequest(request, true);
            Assert.AreEqual(4, prepared.History.Count);
            StringAssert.StartsWith("2", prepared.History[0].Content);
            StringAssert.StartsWith("3", prepared.History[2].Content);
            Assert.AreEqual(json, request.Extra[YuiCharacterDialogueStore.ContextKey]);
            request.Extra[YuiCharacterDialogueStore.ContextKey] = "{broken";
            Assert.IsEmpty(YuiLocalAiPromptBuilder.PrepareChatRequest(request, true).History);
        }

        [Test]
        public void ExportActualPreparedRequestsForOptInModelEvaluation()
        {
            var path = Environment.GetEnvironmentVariable("YUI_LOCAL_AI_QA_FIXTURES");
            if (string.IsNullOrWhiteSpace(path)) Assert.Ignore("Fixture export is opt-in.");
            var cases = new[] {
                ("succession", "talk", "徳川家康の次の征夷大将軍は誰ですか？"),
                ("arithmetic", "talk", "4 + 4 - 3 はいくつですか？"),
                ("bus", "talk", "バスに4人乗っています。次の停留所で4人乗り、3人降りました。乗客は何人ですか？"),
                ("precedence", "talk", "7 + 13 × 4 を計算してください。"),
                ("rank", "talk", "競走で2位の人を追い抜きました。今、何位ですか？"),
                ("logic", "talk", "赤い箱は青い箱より重く、青い箱は緑の箱より重い。一番軽い箱は何色ですか？"),
                ("change", "talk", "120円のりんごを3個買い、500円払いました。おつりはいくら？"),
                ("units", "talk", "3メートル40センチは何センチですか？"),
                ("fraction", "talk", "12個のクッキーを3人で同じ数ずつ分けると、一人何個？"),
                ("weekday", "talk", "月曜日の3日後は何曜日？"),
                ("format", "talk", "雨の日に家でできる遊びを3つ、短く教えて。"),
                ("greeting", "talk", "おはよう！今日はいい天気だね。"),
                ("work_math", "work", "4 + 4 - 3 はいくつですか？"),
                ("work_summary", "work", "次の文を一文で要約して。『会議は木曜日に予定されていたが、担当者の都合で金曜日の午後3時に変更された。場所は第一会議室のまま。』"),
                ("work_code", "work", "Pythonで2つの数を足すadd関数だけを書いてください。"),
                ("history", "talk", "どこに行く予定だっけ？")
            };
            var fixtures = new List<object>();
            foreach (var (id, mode, message) in cases)
            {
                var request = new YuiLocalAiChatRequest { Mode = mode, Message = message, CharacterName = "Yui" };
                if (id == "history") request.Extra[YuiCharacterDialogueStore.ContextKey] =
                    "[{\"User\":\"明日は図書館に行く\",\"Assistant\":\"本を探すのが楽しみだね\"}]";
                fixtures.Add(new { id, request = YuiLocalAiPromptBuilder.PrepareChatRequest(request, true) });
            }
            File.WriteAllText(path, JsonConvert.SerializeObject(fixtures, Formatting.Indented));
            Assert.AreEqual(16, fixtures.Count);
        }
    }
}
