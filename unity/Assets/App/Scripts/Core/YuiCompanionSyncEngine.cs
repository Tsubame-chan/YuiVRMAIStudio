using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.Core
{
    public interface IYuiCompanionRemote
    {
        Task<JObject> CapabilitiesAsync(CancellationToken cancellation);
        Task<YuiCompanionSnapshotPage> SnapshotAsync(string cursor, CancellationToken cancellation);
        Task<YuiCompanionChangePage> ChangesAsync(string cursor, CancellationToken cancellation);
        Task<JObject> CommitAsync(YuiCompanionCommitRequest batch, CancellationToken cancellation);
    }

    public sealed class YuiCompanionBackendRemote : IYuiCompanionRemote
    {
        private readonly YuiBackendClient client;
        private readonly string characterId;
        private readonly string token;

        public YuiCompanionBackendRemote(YuiBackendClient client, string characterId, string pairedToken)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.characterId = characterId;
            token = pairedToken;
        }

        public Task<JObject> CapabilitiesAsync(CancellationToken cancellation)
            => client.CompanionCapabilitiesAsync(token, cancellation);
        public Task<YuiCompanionSnapshotPage> SnapshotAsync(string cursor, CancellationToken cancellation)
            => client.CompanionSnapshotAsync(characterId, cursor, token, cancellation);
        public Task<YuiCompanionChangePage> ChangesAsync(string cursor, CancellationToken cancellation)
            => client.CompanionChangesAsync(characterId, cursor, token, cancellation);
        public Task<JObject> CommitAsync(YuiCompanionCommitRequest batch, CancellationToken cancellation)
            => client.CompanionCommitAsync(characterId, batch, token, cancellation);
    }

    // One explicit sync pass. The app's current Talk/Work flow does not invoke it yet.
    public sealed class YuiCompanionSyncEngine
    {
        private readonly YuiCompanionReplicaStore local;
        private readonly IYuiCompanionRemote remote;

        public YuiCompanionSyncEngine(YuiCompanionReplicaStore local, IYuiCompanionRemote remote)
        {
            this.local = local ?? throw new ArgumentNullException(nameof(local));
            this.remote = remote ?? throw new ArgumentNullException(nameof(remote));
        }

        public async Task SyncOnceAsync(CancellationToken cancellation = default)
        {
            var capabilities = await remote.CapabilitiesAsync(cancellation);
            if ((string)capabilities["hub_id"] != local.HubId || (int?)capabilities["protocol"] != 2)
                throw new InvalidOperationException("Companion hub or protocol changed.");
            await PullAsync(cancellation);
            var pending = OrderPending(local.Load().Pending);
            foreach (var operation in pending)
            {
                cancellation.ThrowIfCancellationRequested();
                var batch = new YuiCompanionCommitRequest
                {
                    BatchId = operation.OpId,
                    Operations = new List<YuiCompanionOperation> { operation }
                };
                // The operation ID remains stable across retry. Never infer success
                // from a timeout; the server may have committed the first attempt.
                var result = await remote.CommitAsync(batch, cancellation);
                var accepted = result?["accepted"] as JArray;
                if (accepted == null || accepted.Count != 1 ||
                    (string)accepted[0]["op_id"] != operation.OpId ||
                    (string)accepted[0]["entity_id"] != operation.EntityId ||
                    (int?)accepted[0]["revision"] != operation.ExpectedRevision +
                        (operation.Type == "forget_source" ? 0 : 1) ||
                    (long?)accepted[0]["seq"] < 1)
                    throw new InvalidOperationException("Companion commit receipt did not match the pending operation.");
                local.Acknowledge(new[] { operation.OpId });
            }
            await PullAsync(cancellation);
        }

        // Local operations can arrive while offline in an order different from
        // their source and conversation dependencies. Keep their IDs unchanged.
        internal static List<YuiCompanionOperation> OrderPending(List<YuiCompanionOperation> pending)
        {
            var operations = pending ?? throw new ArgumentNullException(nameof(pending));
            var byEntity = operations.GroupBy(op => op.EntityId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var dependencies = new Dictionary<string, HashSet<string>>();
            foreach (var operation in operations)
            {
                var required = new HashSet<string>();
                if (operation.ExpectedRevision > 0 && byEntity.TryGetValue(operation.EntityId, out var own))
                    foreach (var source in own)
                        if (source.OpId != operation.OpId && ProducedRevision(source) == operation.ExpectedRevision)
                            required.Add(source.OpId);
                var payload = operation.Payload;
                if (payload != null)
                {
                    AddCreationDependency(required, byEntity, (string)payload["binding_id"], "create_binding");
                    AddCreationDependency(required, byEntity, (string)payload["conversation_id"], "create_conversation");
                    AddCreationDependency(required, byEntity, (string)payload["in_reply_to_record_id"], "append_record");
                    if (operation.Type == "put_memory" && payload["source_refs"] is JArray refs)
                        foreach (var reference in refs.OfType<JObject>())
                        {
                            var sourceId = (string)reference["record_id"];
                            var revision = (int?)reference["revision"];
                            if (sourceId == null || revision == null || !byEntity.TryGetValue(sourceId, out var sourceOps))
                                continue;
                            if (sourceOps.Any(source => source.Type == "delete_record" ||
                                    source.Type == "forget_source" || ProducedRevision(source) > revision))
                                throw new InvalidOperationException("Pending memory cites a withdrawn or older record revision.");
                            foreach (var source in sourceOps)
                                if (ProducedRevision(source) == revision &&
                                    (source.Type == "append_record" || source.Type == "correct_record"))
                                    required.Add(source.OpId);
                        }
                }
                if (required.Contains(operation.OpId))
                    throw new InvalidOperationException("Companion operation depends on itself.");
                dependencies.Add(operation.OpId, required);
            }
            var ordered = new List<YuiCompanionOperation>(operations.Count);
            var positions = operations.Select((operation, index) => new { operation.OpId, index })
                .ToDictionary(item => item.OpId, item => item.index);
            var dependents = new List<int>[operations.Count];
            var indegree = new int[operations.Count];
            for (var index = 0; index < operations.Count; index++)
            {
                dependents[index] = new List<int>();
                indegree[index] = dependencies[operations[index].OpId].Count;
            }
            for (var index = 0; index < operations.Count; index++)
                foreach (var dependency in dependencies[operations[index].OpId])
                    dependents[positions[dependency]].Add(index);
            var ready = new SortedSet<int>();
            for (var index = 0; index < operations.Count; index++)
                if (indegree[index] == 0) ready.Add(index);
            while (ready.Count > 0)
            {
                var index = ready.Min;
                ready.Remove(index);
                ordered.Add(operations[index]);
                foreach (var dependent in dependents[index])
                    if (--indegree[dependent] == 0) ready.Add(dependent);
            }
            if (ordered.Count != operations.Count)
                throw new InvalidOperationException("Companion pending operations contain a dependency cycle.");
            return ordered;
        }

        private static int ProducedRevision(YuiCompanionOperation operation)
            => operation.ExpectedRevision + (operation.Type == "forget_source" ? 0 : 1);

        private static void AddCreationDependency(HashSet<string> required,
            Dictionary<string, List<YuiCompanionOperation>> byEntity, string entityId, string type)
        {
            if (entityId == null || !byEntity.TryGetValue(entityId, out var operations)) return;
            foreach (var operation in operations)
                if (operation.Type == type) required.Add(operation.OpId);
        }

        private async Task PullAsync(CancellationToken cancellation)
        {
            if (local.Load().Cursor == null)
            {
                await SnapshotAsync(cancellation);
                return;
            }
            for (var pageCount = 0; pageCount < 10000; pageCount++)
            {
                cancellation.ThrowIfCancellationRequested();
                var cursor = local.Load().Cursor;
                YuiCompanionChangePage page;
                try { page = await remote.ChangesAsync(cursor, cancellation); }
                catch (YuiBackendException error) when (error.StatusCode == 410)
                {
                    await SnapshotAsync(cancellation);
                    return;
                }
                local.ApplyChanges(page, cursor);
                if (!page.HasMore) return;
                if (page.NextCursor == cursor)
                    throw new InvalidOperationException("Companion change cursor did not advance.");
            }
            throw new InvalidOperationException("Companion change pagination limit reached.");
        }

        private async Task SnapshotAsync(CancellationToken cancellation)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var collected = new List<JObject>();
                string cursor = null, changeCursor = null;
                long head = -1;
                int epoch = -1;
                try
                {
                    for (var pageCount = 0; pageCount < 10000; pageCount++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var page = await remote.SnapshotAsync(cursor, cancellation);
                        if (page == null || page.Items == null || string.IsNullOrEmpty(page.ChangeCursor))
                            throw new InvalidOperationException("Incomplete companion snapshot page.");
                        if (head < 0) { head = page.HeadSeq; epoch = page.PrivacyEpoch; changeCursor = page.ChangeCursor; }
                        if (page.HeadSeq != head || page.PrivacyEpoch != epoch || page.ChangeCursor != changeCursor)
                            throw new InvalidOperationException("Companion snapshot changed during paging.");
                        collected.AddRange(page.Items);
                        if (!page.HasMore)
                        {
                            local.ReplaceSnapshot(collected, head, epoch, changeCursor);
                            return;
                        }
                        if (string.IsNullOrEmpty(page.NextCursor) || page.NextCursor == cursor)
                            throw new InvalidOperationException("Companion snapshot cursor did not advance.");
                        cursor = page.NextCursor;
                    }
                    throw new InvalidOperationException("Companion snapshot pagination limit reached.");
                }
                catch (YuiBackendException error) when (error.StatusCode == 410 && attempt == 0)
                {
                    // A privacy change can invalidate a snapshot between pages.
                    // Start again, without changing the local cursor or pending work.
                }
            }
        }
    }
}
