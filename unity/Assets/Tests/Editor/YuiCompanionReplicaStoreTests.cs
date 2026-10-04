using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Tests
{
    public sealed class YuiCompanionReplicaStoreTests
    {
        private string root;
        private YuiCompanionReplicaStore store;
        private const string Hub = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Character = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "yui-companion-" + Guid.NewGuid().ToString("N"));
            store = new YuiCompanionReplicaStore(root, Hub, Character);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void PendingOperationSurvivesSnapshotAndPrivacyResetDoesNotAdvanceCursor()
        {
            var operation = new YuiCompanionOperation
            {
                OpId = "cccccccccccccccccccccccccccccccc",
                EntityId = "dddddddddddddddddddddddddddddddd",
                Type = "append_record", ExpectedRevision = 0,
                Payload = JObject.Parse("{\"kind\":\"user_utterance\",\"text\":\"private pending\"}")
            };
            store.Queue(operation);
            store.Queue(operation);
            Assert.AreEqual(1, new YuiCompanionReplicaStore(root, Hub, Character).Load().Pending.Count);
            Assert.Throws<InvalidOperationException>(() => store.Queue(new YuiCompanionOperation
            {
                OpId = operation.OpId, EntityId = operation.EntityId,
                Type = operation.Type, Payload = JObject.Parse("{\"text\":\"changed\"}")
            }));
            store.ReplaceSnapshot(new[] { JObject.Parse("{\"entity_type\":\"record\",\"entity_id\":\"eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee\",\"revision\":1}") },
                                  2, 0, "cursor-2");
            Assert.AreEqual(1, store.Load().Pending.Count);
            var page = new YuiCompanionChangePage
            {
                Items = new List<JObject> { JObject.Parse("{\"seq\":3,\"entity_type\":\"record\",\"entity_id\":\"eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee\",\"revision\":2}") },
                NextCursor = "cursor-3", HeadSeq = 3, PrivacyEpoch = 0
            };
            store.ApplyChanges(page, "cursor-2");
            Assert.AreEqual(2, (int)store.Load().Items[0]["revision"]);
            Assert.Throws<InvalidOperationException>(() => store.ApplyChanges(new YuiCompanionChangePage
            {
                Items = new List<JObject>(), NextCursor = "wrong", HeadSeq = 3, PrivacyEpoch = 1
            }, "cursor-3"));
            Assert.AreEqual("cursor-3", store.Load().Cursor);
            Assert.AreEqual(1, store.Load().Pending.Count);
            store.Acknowledge(new[] { operation.OpId });
            Assert.AreEqual(0, store.Load().Pending.Count);
            Assert.AreEqual(1, Directory.GetFiles(Path.Combine(root, "CompanionV2", Hub, Character),
                                                    "state-*.json").Length);
        }

        [Test]
        public void CorruptManifestTargetFailsClosedWithoutUsingOlderGeneration()
        {
            store.ReplaceSnapshot(new JObject[0], 0, 0, "cursor-0");
            var folder = Path.Combine(root, "CompanionV2", Hub, Character);
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")));
            var statePath = Path.Combine(folder, (string)manifest["file"]);
            File.WriteAllText(statePath, "tampered");
            Assert.Throws<InvalidDataException>(() => store.Load());
        }

        [Test]
        public void LoadingCommittedManifestRemovesGenerationLeftByInterruptedCleanup()
        {
            store.ReplaceSnapshot(new JObject[0], 0, 0, "cursor-0");
            var folder = Path.Combine(root, "CompanionV2", Hub, Character);
            var stale = Path.Combine(folder, "state-0.json");
            File.WriteAllText(stale, "old private text");
            Assert.AreEqual("cursor-0", store.Load().Cursor);
            Assert.IsFalse(File.Exists(stale));
        }

        [Test]
        public void ConcurrentQueuesOnOneReplicaRetainEveryPendingOperation()
        {
            Parallel.For(0, 20, index => store.Queue(new YuiCompanionOperation
            {
                OpId = index.ToString("x32"),
                EntityId = (index + 100).ToString("x32"),
                Type = "append_record", ExpectedRevision = 0,
                Payload = JObject.Parse("{\"kind\":\"user_utterance\",\"text\":\"pending\"}")
            }));
            Assert.AreEqual(20, store.Load().Pending.Count);
        }
    }
}
