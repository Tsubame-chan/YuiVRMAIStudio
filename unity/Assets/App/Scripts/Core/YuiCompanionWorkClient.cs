using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.Core
{
    // A Work request is an Activity, not a synchronous chat completion. The
    // caller keeps the request ID across retries and presents reports later.
    public sealed class YuiCompanionWorkClient
    {
        private readonly YuiBackendClient client;
        private readonly string characterId;
        private readonly string connectionId;
        private readonly string pairedToken;

        public YuiCompanionWorkClient(YuiBackendClient client, string characterId,
            string connectionId, string pairedToken)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.characterId = ValidateId(characterId, nameof(characterId));
            this.connectionId = ValidateId(connectionId, nameof(connectionId));
            this.pairedToken = !string.IsNullOrWhiteSpace(pairedToken)
                ? pairedToken : throw new ArgumentException("Paired device token required.", nameof(pairedToken));
        }

        private static string ValidateId(string value, string name)
        {
            if (value == null || value.Length != 32) throw new ArgumentException("Invalid ID.", name);
            foreach (var c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    throw new ArgumentException("Invalid ID.", name);
            return value;
        }

        public static string StableId(string requestId, string role)
        {
            ValidateId(requestId, nameof(requestId));
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes("yui-companion-work-v1:" + requestId + ":" + role));
            return BitConverter.ToString(bytes, 0, 16).Replace("-", "").ToLowerInvariant();
        }

        public async Task<YuiCompanionActivityReceipt> DispatchAsync(string requestId, string request,
            CancellationToken cancellation = default)
        {
            ValidateId(requestId, nameof(requestId));
            if (string.IsNullOrWhiteSpace(request) || request.Length > 16000)
                throw new ArgumentException("Work request must contain 1–16000 characters.", nameof(request));
            var bindingId = StableId(requestId, "binding");
            var conversationId = StableId(requestId, "conversation");
            var batch = new YuiCompanionCommitRequest
            {
                BatchId = StableId(requestId, "batch"),
                Operations = new List<YuiCompanionOperation>
                {
                    new YuiCompanionOperation
                    {
                        OpId = StableId(requestId, "binding-op"), Type = "create_binding",
                        EntityId = bindingId, ExpectedRevision = 0,
                        Payload = JObject.FromObject(new YuiCompanionBindingPayload
                        {
                            Purpose = "work", ConnectionId = connectionId,
                            ContextPolicy = "private_only"
                        })
                    },
                    new YuiCompanionOperation
                    {
                        OpId = StableId(requestId, "conversation-op"), Type = "create_conversation",
                        EntityId = conversationId, ExpectedRevision = 0,
                        Payload = JObject.FromObject(new YuiCompanionConversationPayload
                        {
                            Purpose = "work", BindingId = bindingId
                        })
                    }
                }
            };
            // A repeated call with the same request ID has exactly the same
            // batch and Activity IDs. A timeout never triggers a new task.
            await client.CompanionCommitAsync(characterId, batch, pairedToken, cancellation);
            return await client.CompanionCreateActivityAsync(characterId,
                new YuiCompanionActivityCreate
                {
                    OpId = StableId(requestId, "activity-op"),
                    ActivityId = StableId(requestId, "activity"),
                    ConversationId = conversationId,
                    Request = request
                }, pairedToken, cancellation);
        }

        public Task<YuiCompanionActivityDetails> ReadAsync(string requestId,
            CancellationToken cancellation = default)
            => client.CompanionActivityDetailsAsync(characterId, StableId(requestId, "activity"),
                pairedToken, cancellation);

        public async Task<YuiCompanionActivityList> ListAsync(CancellationToken cancellation = default,
            bool fullScan = false)
        {
            var first = await client.CompanionActivitiesAsync(characterId, connectionId,
                pairedToken, cancellation);
            if (!fullScan || first?.Items == null) return first;
            var cursor = first.NextBefore;
            var pages = 1;
            while (!string.IsNullOrEmpty(cursor))
            {
                if (++pages > 100)
                    throw new InvalidOperationException("Work activity history exceeds the safe catch-up limit.");
                cancellation.ThrowIfCancellationRequested();
                var page = await client.CompanionActivitiesAsync(characterId, connectionId,
                    pairedToken, cancellation, cursor);
                if (page?.Items == null || page.Items.Count == 0)
                    throw new InvalidOperationException("Work activity pagination ended unexpectedly.");
                first.Items.AddRange(page.Items);
                cursor = page.NextBefore;
            }
            first.NextBefore = null;
            return first;
        }
    }
}
