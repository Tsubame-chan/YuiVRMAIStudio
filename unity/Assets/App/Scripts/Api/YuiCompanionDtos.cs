using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YuiPhysicalAI.Api
{
    // Protocol 2 wire shapes. The existing client does not send these until migration is accepted.
    [Serializable]
    public sealed class YuiCompanionCommitRequest
    {
        [JsonProperty("batch_id")] public string BatchId { get; set; }
        [JsonProperty("operations")] public List<YuiCompanionOperation> Operations { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionOperation
    {
        [JsonProperty("op_id")] public string OpId { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("entity_id")] public string EntityId { get; set; }
        [JsonProperty("expected_revision")] public int ExpectedRevision { get; set; }
        [JsonProperty("payload")] public JObject Payload { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionRecordPayload
    {
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("conversation_id")] public string ConversationId { get; set; }
        [JsonProperty("turn_id")] public string TurnId { get; set; }
        [JsonProperty("in_reply_to_record_id")] public string InReplyToRecordId { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("recorded_at")] public string RecordedAt { get; set; }
        [JsonProperty("realm")] public string Realm { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionBindingPayload
    {
        [JsonProperty("purpose")] public string Purpose { get; set; }
        [JsonProperty("connection_id")] public string ConnectionId { get; set; }
        [JsonProperty("context_policy")] public string ContextPolicy { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionConversationPayload
    {
        [JsonProperty("purpose")] public string Purpose { get; set; }
        [JsonProperty("binding_id")] public string BindingId { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionSourceRef
    {
        [JsonProperty("record_id")] public string RecordId { get; set; }
        [JsonProperty("revision")] public int Revision { get; set; }
        [JsonProperty("quote")] public string Quote { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionMemoryPayload
    {
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("subject")] public string Subject { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("basis")] public string Basis { get; set; }
        [JsonProperty("pinned")] public bool Pinned { get; set; }
        [JsonProperty("source_refs")] public List<YuiCompanionSourceRef> SourceRefs { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionProfilePayload
    {
        [JsonProperty("field")] public string Field { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionChangePage
    {
        [JsonProperty("items")] public List<JObject> Items { get; set; }
        [JsonProperty("next_cursor")] public string NextCursor { get; set; }
        [JsonProperty("privacy_epoch")] public int PrivacyEpoch { get; set; }
        [JsonProperty("head_seq")] public long HeadSeq { get; set; }
        [JsonProperty("has_more")] public bool HasMore { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionSnapshotPage
    {
        [JsonProperty("items")] public List<JObject> Items { get; set; }
        [JsonProperty("next_cursor")] public string NextCursor { get; set; }
        [JsonProperty("privacy_epoch")] public int PrivacyEpoch { get; set; }
        [JsonProperty("head_seq")] public long HeadSeq { get; set; }
        [JsonProperty("change_cursor")] public string ChangeCursor { get; set; }
        [JsonProperty("has_more")] public bool HasMore { get; set; }
        [JsonProperty("expires_in")] public int ExpiresIn { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionContextRequest
    {
        [JsonProperty("conversation_id")] public string ConversationId { get; set; }
        [JsonProperty("purpose")] public string Purpose { get; set; }
        [JsonProperty("realm")] public string Realm { get; set; } = "real";
        [JsonProperty("query")] public string Query { get; set; }
        [JsonProperty("budget_chars")] public int BudgetChars { get; set; } = 4000;
    }

    [Serializable]
    public sealed class YuiCompanionContextPacket
    {
        [JsonProperty("conversation_id")] public string ConversationId { get; set; }
        [JsonProperty("purpose")] public string Purpose { get; set; }
        [JsonProperty("realm")] public string Realm { get; set; }
        [JsonProperty("head_seq")] public long HeadSeq { get; set; }
        [JsonProperty("privacy_epoch")] public int PrivacyEpoch { get; set; }
        [JsonProperty("items")] public List<JObject> Items { get; set; }
        [JsonProperty("truncated")] public bool Truncated { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionActivityCreate
    {
        [JsonProperty("op_id")] public string OpId { get; set; }
        [JsonProperty("activity_id")] public string ActivityId { get; set; }
        [JsonProperty("conversation_id")] public string ConversationId { get; set; }
        [JsonProperty("request")] public string Request { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionActivityCommand
    {
        [JsonProperty("op_id")] public string OpId { get; set; }
        [JsonProperty("expected_revision")] public int ExpectedRevision { get; set; }
        [JsonProperty("request_revision")] public int RequestRevision { get; set; }
        [JsonProperty("command")] public string Command { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("question_op_id")] public string QuestionOpId { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionActivityReceipt
    {
        [JsonProperty("activity_id")] public string ActivityId { get; set; }
        [JsonProperty("revision")] public int Revision { get; set; }
        [JsonProperty("request_revision")] public int RequestRevision { get; set; }
        [JsonProperty("state")] public string State { get; set; }
        [JsonProperty("stop_state")] public string StopState { get; set; }
        [JsonProperty("seq")] public long Seq { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionActivityReport
    {
        [JsonProperty("op_id")] public string OpId { get; set; }
        [JsonProperty("request_revision")] public int RequestRevision { get; set; }
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("spoken_text")] public string SpokenText { get; set; }
        [JsonProperty("applied")] public bool Applied { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionActivityDetails
    {
        [JsonProperty("activity_id")] public string ActivityId { get; set; }
        [JsonProperty("character_id")] public string CharacterId { get; set; }
        [JsonProperty("conversation_id")] public string ConversationId { get; set; }
        [JsonProperty("request")] public string Request { get; set; }
        [JsonProperty("request_revision")] public int RequestRevision { get; set; }
        [JsonProperty("revision")] public int Revision { get; set; }
        [JsonProperty("state")] public string State { get; set; }
        [JsonProperty("completion_basis")] public string CompletionBasis { get; set; }
        [JsonProperty("reports")] public List<YuiCompanionActivityReport> Reports { get; set; }
        [JsonProperty("answers")] public List<YuiCompanionActivityAnswer> Answers { get; set; }

        public YuiCompanionActivityReport LatestPresentableReport()
        {
            if (State == "cancel_requested" || State == "cancelled" || Reports == null)
                return null;
            for (var index = Reports.Count - 1; index >= 0; index--)
            {
                var report = Reports[index];
                if (report != null && report.Applied && report.RequestRevision == RequestRevision
                    && !string.IsNullOrWhiteSpace(report.Text))
                    return report;
            }
            return null;
        }
    }

    [Serializable]
    public sealed class YuiCompanionActivityAnswer
    {
        [JsonProperty("op_id")] public string OpId { get; set; }
        [JsonProperty("question_op_id")] public string QuestionOpId { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionActivityList
    {
        [JsonProperty("items")] public List<YuiCompanionActivityDetails> Items { get; set; }
        [JsonProperty("next_before")] public string NextBefore { get; set; }
    }

    [Serializable]
    public sealed class YuiCompanionSpeechClaim
    {
        [JsonProperty("claimed")] public bool Claimed { get; set; }
        [JsonProperty("claim_token")] public string ClaimToken { get; set; }
        [JsonProperty("reason")] public string Reason { get; set; }
    }
}
