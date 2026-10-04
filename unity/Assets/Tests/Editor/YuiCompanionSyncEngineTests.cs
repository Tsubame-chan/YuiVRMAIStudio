using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Tests
{
    public sealed class YuiCompanionSyncEngineTests
    {
        private const string Hub = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Character = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private sealed class FakeRemote : IYuiCompanionRemote
        {
            public bool FailFirstCommit = true;
            public bool WrongReceipt;
            public readonly List<string> BatchIds = new List<string>();
            public Task<JObject> CapabilitiesAsync(CancellationToken cancellation)
                => Task.FromResult(JObject.Parse("{\"hub_id\":\"" + Hub + "\",\"protocol\":2}"));
            public Task<YuiCompanionSnapshotPage> SnapshotAsync(string cursor, CancellationToken cancellation)
                => Task.FromResult(new YuiCompanionSnapshotPage
                {
                    Items = new List<JObject>(), HeadSeq = 0, PrivacyEpoch = 0,
                    ChangeCursor = "c0", HasMore = false
                });
            public Task<YuiCompanionChangePage> ChangesAsync(string cursor, CancellationToken cancellation)
                => Task.FromResult(new YuiCompanionChangePage
                {
                    Items = cursor == "c0" && !FailFirstCommit && BatchIds.Count >= 2
                        ? new List<JObject> { JObject.Parse("{\"seq\":1,\"entity_type\":\"record\",\"entity_id\":\"dddddddddddddddddddddddddddddddd\",\"revision\":1}") }
                        : new List<JObject>(),
                    NextCursor = cursor == "c0" && !FailFirstCommit && BatchIds.Count >= 2 ? "c1" : cursor,
                    HeadSeq = cursor == "c0" && !FailFirstCommit && BatchIds.Count >= 2 ? 1 : 0,
                    PrivacyEpoch = 0, HasMore = false
                });
            public Task<JObject> CommitAsync(YuiCompanionCommitRequest batch, CancellationToken cancellation)
            {
                BatchIds.Add(batch.BatchId);
                if (FailFirstCommit)
                {
                    FailFirstCommit = false;
                    throw new IOException("Lost response after possible server commit");
                }
                return Task.FromResult(JObject.FromObject(new
                {
                    accepted = new[] { new {
                        op_id = batch.Operations[0].OpId,
                        entity_id = WrongReceipt ? "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee" : batch.Operations[0].EntityId,
                        revision = batch.Operations[0].ExpectedRevision + 1,
                        seq = 1
                    } }
                }));
            }
        }

        private sealed class DependencyRemote : IYuiCompanionRemote
        {
            public readonly List<string> Order = new List<string>();
            public Task<JObject> CapabilitiesAsync(CancellationToken cancellation)
                => Task.FromResult(JObject.Parse("{\"hub_id\":\"" + Hub + "\",\"protocol\":2}"));
            public Task<YuiCompanionSnapshotPage> SnapshotAsync(string cursor, CancellationToken cancellation)
                => Task.FromResult(new YuiCompanionSnapshotPage {
                    Items = new List<JObject>(), HeadSeq = 0, PrivacyEpoch = 0,
                    ChangeCursor = "c0", HasMore = false
                });
            public Task<YuiCompanionChangePage> ChangesAsync(string cursor, CancellationToken cancellation)
                => Task.FromResult(new YuiCompanionChangePage {
                    Items = new List<JObject>(), NextCursor = cursor, HeadSeq = 0,
                    PrivacyEpoch = 0, HasMore = false
                });
            public Task<JObject> CommitAsync(YuiCompanionCommitRequest batch, CancellationToken cancellation)
            {
                var operation = batch.Operations[0];
                Order.Add(operation.Type);
                if (operation.Type == "create_conversation" && !Order.Contains("create_binding") ||
                    operation.Type == "append_record" && !Order.Contains("create_conversation") ||
                    operation.Type == "put_memory" && !Order.Contains("append_record"))
                    throw new InvalidOperationException("Dependency was sent after its consumer.");
                return Task.FromResult(JObject.FromObject(new {
                    accepted = new[] { new { op_id = operation.OpId, entity_id = operation.EntityId,
                        revision = operation.ExpectedRevision + 1, seq = Order.Count } }
                }));
            }
        }

        [Test]
        public async Task OfflineDependenciesAreCommittedBeforeTheirConsumers()
        {
            var root = Path.Combine(Path.GetTempPath(), "yui-sync-engine-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new YuiCompanionReplicaStore(root, Hub, Character);
                const string binding = "11111111111111111111111111111111";
                const string conversation = "22222222222222222222222222222222";
                const string record = "33333333333333333333333333333333";
                store.Queue(new YuiCompanionOperation {
                    OpId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Type = "put_memory",
                    EntityId = "44444444444444444444444444444444", ExpectedRevision = 0,
                    Payload = JObject.Parse("{\"source_refs\":[{\"record_id\":\"" + record + "\",\"revision\":1}]}"),
                });
                store.Queue(new YuiCompanionOperation {
                    OpId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", Type = "append_record",
                    EntityId = record, ExpectedRevision = 0,
                    Payload = JObject.Parse("{\"conversation_id\":\"" + conversation + "\"}"),
                });
                store.Queue(new YuiCompanionOperation {
                    OpId = "cccccccccccccccccccccccccccccccc", Type = "create_conversation",
                    EntityId = conversation, ExpectedRevision = 0,
                    Payload = JObject.Parse("{\"binding_id\":\"" + binding + "\"}"),
                });
                store.Queue(new YuiCompanionOperation {
                    OpId = "dddddddddddddddddddddddddddddddd", Type = "create_binding",
                    EntityId = binding, ExpectedRevision = 0, Payload = JObject.Parse("{}"),
                });
                var remote = new DependencyRemote();
                await new YuiCompanionSyncEngine(store, remote).SyncOnceAsync();
                CollectionAssert.AreEqual(new[] { "create_binding", "create_conversation", "append_record", "put_memory" },
                    remote.Order);
                Assert.AreEqual(0, store.Load().Pending.Count);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void WithdrawnPendingSourceKeepsDerivedMemoryUnsent()
        {
            var root = Path.Combine(Path.GetTempPath(), "yui-sync-engine-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new YuiCompanionReplicaStore(root, Hub, Character);
                const string record = "33333333333333333333333333333333";
                store.Queue(new YuiCompanionOperation {
                    OpId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Type = "put_memory",
                    EntityId = "44444444444444444444444444444444", ExpectedRevision = 0,
                    Payload = JObject.Parse("{\"source_refs\":[{\"record_id\":\"" + record + "\",\"revision\":1}]}"),
                });
                store.Queue(new YuiCompanionOperation {
                    OpId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", Type = "delete_record",
                    EntityId = record, ExpectedRevision = 1,
                });
                var remote = new DependencyRemote();
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await new YuiCompanionSyncEngine(store, remote).SyncOnceAsync());
                Assert.AreEqual(0, remote.Order.Count);
                Assert.AreEqual(2, store.Load().Pending.Count);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void ReplyCycleIsRejectedBeforeAnyCommit()
        {
            var root = Path.Combine(Path.GetTempPath(), "yui-sync-engine-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new YuiCompanionReplicaStore(root, Hub, Character);
                const string first = "11111111111111111111111111111111";
                const string second = "22222222222222222222222222222222";
                store.Queue(new YuiCompanionOperation {
                    OpId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Type = "append_record",
                    EntityId = first, ExpectedRevision = 0,
                    Payload = JObject.Parse("{\"in_reply_to_record_id\":\"" + second + "\"}"),
                });
                store.Queue(new YuiCompanionOperation {
                    OpId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", Type = "append_record",
                    EntityId = second, ExpectedRevision = 0,
                    Payload = JObject.Parse("{\"in_reply_to_record_id\":\"" + first + "\"}"),
                });
                var remote = new DependencyRemote();
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await new YuiCompanionSyncEngine(store, remote).SyncOnceAsync());
                Assert.AreEqual(0, remote.Order.Count);
                Assert.AreEqual(2, store.Load().Pending.Count);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void MismatchedCommitReceiptKeepsPendingOperation()
        {
            var root = Path.Combine(Path.GetTempPath(), "yui-sync-engine-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new YuiCompanionReplicaStore(root, Hub, Character);
                store.Queue(new YuiCompanionOperation
                {
                    OpId = "cccccccccccccccccccccccccccccccc",
                    EntityId = "dddddddddddddddddddddddddddddddd",
                    Type = "append_record", ExpectedRevision = 0,
                    Payload = JObject.Parse("{\"kind\":\"user_utterance\",\"text\":\"hello\"}")
                });
                var engine = new YuiCompanionSyncEngine(store, new FakeRemote
                {
                    FailFirstCommit = false, WrongReceipt = true
                });
                Assert.ThrowsAsync<InvalidOperationException>(async () => await engine.SyncOnceAsync());
                Assert.AreEqual(1, store.Load().Pending.Count);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public async Task RetryKeepsIdempotencyAndPendingUntilMatchingReceipt()
        {
            var root = Path.Combine(Path.GetTempPath(), "yui-sync-engine-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new YuiCompanionReplicaStore(root, Hub, Character);
                var operation = new YuiCompanionOperation
                {
                    OpId = "cccccccccccccccccccccccccccccccc",
                    EntityId = "dddddddddddddddddddddddddddddddd",
                    Type = "append_record", ExpectedRevision = 0,
                    Payload = JObject.Parse("{\"kind\":\"user_utterance\",\"text\":\"hello\"}")
                };
                store.Queue(operation);
                var fake = new FakeRemote();
                var engine = new YuiCompanionSyncEngine(store, fake);
                Assert.ThrowsAsync<IOException>(async () => await engine.SyncOnceAsync());
                Assert.AreEqual(1, store.Load().Pending.Count);
                Assert.AreEqual("c0", store.Load().Cursor);
                await engine.SyncOnceAsync();
                Assert.AreEqual(operation.OpId, fake.BatchIds[0]);
                Assert.AreEqual(fake.BatchIds[0], fake.BatchIds[1]);
                Assert.AreEqual(0, store.Load().Pending.Count);
                Assert.AreEqual("c1", store.Load().Cursor);
                Assert.AreEqual(1, store.Load().Items.Count);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
