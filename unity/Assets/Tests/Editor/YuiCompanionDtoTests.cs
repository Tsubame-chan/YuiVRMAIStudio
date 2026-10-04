using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Tests
{
    public sealed class YuiCompanionDtoTests
    {
        [Test]
        public void PairedChatKeepsSharedIdentityAndCanonicalReceipt()
        {
            var request = new ChatRequest { RequestId = "request-one", CharacterId = "local-avatar",
                SharedCharacterId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", SessionId = "local-session",
                Message = "こんにちは", Mode = "talk" };
            var json = JObject.FromObject(request);
            Assert.AreEqual("local-avatar", (string)json["character_id"]);
            Assert.AreEqual(request.SharedCharacterId, (string)json["shared_character_id"]);
            Assert.AreEqual("talk", (string)json["mode"]);
            var response = JsonConvert.DeserializeObject<ChatResponse>("{\"text\":\"返答\",\"shared_canonical\":true}");
            Assert.IsTrue(response.SharedCanonical);
            var client = new YuiBackendClient("http://127.0.0.1:8000");
            Assert.Throws<System.ArgumentException>(() => client.SharedChatAsync(request, "").GetAwaiter().GetResult());
        }

        [Test]
        public void CompanionTransportRejectsUnpairedOrEscapingRoutes()
        {
            var client = new YuiBackendClient("http://127.0.0.1:8000");
            Assert.Throws<System.ArgumentException>(() => client.CompanionAsync("/sync/characters", null, "token"));
            Assert.Throws<System.ArgumentException>(() => client.CompanionAsync("/companion/v2/../admin", null, "token"));
            Assert.Throws<System.ArgumentException>(() => client.CompanionAsync("/companion/v2/capabilities", null, ""));
            Assert.Throws<System.ArgumentException>(() => client.CompanionCommitAsync("INVALID", new YuiCompanionCommitRequest(), "token"));
        }

        [Test]
        public void ContextRequestKeepsConversationAndPurposeExplicit()
        {
            var request = new YuiCompanionContextRequest
            {
                ConversationId = "44444444444444448444444444444444",
                Purpose = "talk",
                Query = "マンゴー"
            };
            var json = JObject.FromObject(request);
            Assert.AreEqual(request.ConversationId, (string)json["conversation_id"]);
            Assert.AreEqual("talk", (string)json["purpose"]);
            Assert.AreEqual("real", (string)json["realm"]);
            Assert.AreEqual(4000, (int)json["budget_chars"]);
        }

        [Test]
        public void ActivityCommandKeepsBothRevisionsAndRejectsEscapingPath()
        {
            var command = new YuiCompanionActivityCommand
            {
                OpId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                ExpectedRevision = 4,
                RequestRevision = 2,
                Command = "update_request",
                Text = "new task"
            };
            var json = JObject.FromObject(command);
            Assert.AreEqual(4, (int)json["expected_revision"]);
            Assert.AreEqual(2, (int)json["request_revision"]);
            Assert.AreEqual("new task", (string)json["text"]);
            var client = new YuiBackendClient("http://127.0.0.1:8000");
            Assert.Throws<System.ArgumentException>(() => client.CompanionActivityAsync(
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "../admin", "token"));
        }

        [Test]
        public void WorkActivityResultKeepsFullTextSeparateFromSpeech()
        {
            var json = JObject.Parse("{\"activity_id\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"state\":\"completed\",\"request_revision\":1,\"completion_basis\":\"external_report\",\"reports\":[{\"request_revision\":1,\"kind\":\"result\",\"text\":\"Detailed result with sources.\",\"spoken_text\":\"結果が出たよ。詳しくは画面を見てね。\",\"applied\":true}]}");
            var activity = json.ToObject<YuiCompanionActivityDetails>();
            Assert.AreEqual("completed", activity.State);
            Assert.AreEqual("external_report", activity.CompletionBasis);
            Assert.AreEqual("Detailed result with sources.", activity.Reports[0].Text);
            Assert.AreEqual("結果が出たよ。詳しくは画面を見てね。", activity.Reports[0].SpokenText);
            Assert.IsTrue(activity.Reports[0].Applied);
            Assert.AreSame(activity.Reports[0], activity.LatestPresentableReport());
        }

        [Test]
        public void LateOrSupersededWorkReportsAreNeverPresentable()
        {
            var activity = new YuiCompanionActivityDetails
            {
                State = "completed",
                RequestRevision = 2,
                Reports = new System.Collections.Generic.List<YuiCompanionActivityReport>
                {
                    new YuiCompanionActivityReport { RequestRevision = 1, Kind = "result", Text = "old", Applied = true },
                    new YuiCompanionActivityReport { RequestRevision = 2, Kind = "result", Text = "late", Applied = false }
                }
            };
            Assert.IsNull(activity.LatestPresentableReport());
            activity.Reports.Add(new YuiCompanionActivityReport
            {
                RequestRevision = 2, Kind = "result", Text = "current full answer",
                SpokenText = "結果が出たよ。", Applied = true
            });
            Assert.AreEqual("current full answer", activity.LatestPresentableReport().Text);
            activity.State = "cancel_requested";
            Assert.IsNull(activity.LatestPresentableReport());
        }

        [Test]
        public void WorkDispatchUsesStableIndependentIds()
        {
            const string request = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var first = YuiCompanionWorkClient.StableId(request, "activity");
            Assert.AreEqual(first, YuiCompanionWorkClient.StableId(request, "activity"));
            Assert.AreEqual(32, first.Length);
            Assert.AreNotEqual(first, YuiCompanionWorkClient.StableId(request, "conversation"));
            Assert.AreNotEqual(first, YuiCompanionWorkClient.StableId(
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "activity"));
            Assert.Throws<System.ArgumentException>(() => YuiCompanionWorkClient.StableId("bad", "activity"));
        }

        [Test]
        public void WorkActivityPageCarriesContinuationCursor()
        {
            const string cursor = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var page = JsonConvert.DeserializeObject<YuiCompanionActivityList>(
                "{\"items\":[{\"activity_id\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}],\"next_before\":\"" + cursor + "\"}");
            Assert.AreEqual(1, page.Items.Count);
            Assert.AreEqual(cursor, page.NextBefore);
        }

        [Test]
        public void WorkQuestionAnswerKeepsQuestionReferenceAndOriginalRequest()
        {
            var command = new YuiCompanionActivityCommand {
                OpId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", ExpectedRevision = 2,
                RequestRevision = 1, Command = "answer_question", Text = "Tokyo",
                QuestionOpId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
            };
            var wire = JsonConvert.SerializeObject(command);
            Assert.AreEqual(command.QuestionOpId,
                (string)Newtonsoft.Json.Linq.JObject.Parse(wire)["question_op_id"]);
            var detail = JsonConvert.DeserializeObject<YuiCompanionActivityDetails>(
                "{\"request\":\"Find restaurants\",\"answers\":[{\"op_id\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"question_op_id\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\",\"text\":\"Tokyo\"}]}");
            Assert.AreEqual("Find restaurants", detail.Request);
            Assert.AreEqual("Tokyo", detail.Answers[0].Text);
        }

        [Test]
        public void WorkSpeechReceiptCannotEscapeItsReportRoute()
        {
            var client = new YuiBackendClient("http://127.0.0.1:8000");
            Assert.ThrowsAsync<System.ArgumentException>(async () => await client.CompanionClaimSpeechAsync(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "../admin", "paired"));
            Assert.Throws<System.ArgumentException>(() => client.CompanionSpeechReceiptAsync(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "cccccccccccccccccccccccccccccccc", "unknown", "claim", "paired"));
        }

        [Test]
        public void SharedCommitFixturePreservesRecordAndMemorySource()
        {
            var path = Path.Combine(Application.dataPath, "Tests", "Fixtures", "companion_v2_commit.json");
            var source = File.ReadAllText(path);
            var request = JsonConvert.DeserializeObject<YuiCompanionCommitRequest>(source);
            Assert.AreEqual(4, request.Operations.Count);
            var binding = request.Operations[0].Payload.ToObject<YuiCompanionBindingPayload>();
            var conversation = request.Operations[1].Payload.ToObject<YuiCompanionConversationPayload>();
            var record = request.Operations[2].Payload.ToObject<YuiCompanionRecordPayload>();
            var memory = request.Operations[3].Payload.ToObject<YuiCompanionMemoryPayload>();
            Assert.AreEqual("talk", binding.Purpose);
            Assert.AreEqual("private_only", binding.ContextPolicy);
            Assert.AreEqual(request.Operations[0].EntityId, conversation.BindingId);
            Assert.AreEqual(request.Operations[1].EntityId, record.ConversationId);
            Assert.AreEqual("aaaaaaaaaaaa4aaa8aaaaaaaaaaaaaaa", record.TurnId);
            Assert.AreEqual("user_utterance", record.Kind);
            Assert.AreEqual("マンゴーも生姜もおいしかった。", record.Text);
            Assert.AreEqual(request.Operations[2].EntityId, memory.SourceRefs[0].RecordId);
            Assert.AreEqual(1, memory.SourceRefs[0].Revision);
            Assert.IsFalse(memory.Pinned);
            Assert.IsTrue(JToken.DeepEquals(JToken.Parse(source), JToken.Parse(JsonConvert.SerializeObject(request))));
        }
    }
}
