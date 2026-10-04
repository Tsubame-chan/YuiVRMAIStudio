using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.Core
{
    // Isolated protocol-2 replica. It is not wired into the current chat UI.
    // A manifest switch is the commit point; incomplete generations are ignored.
    public sealed class YuiCompanionReplicaStore
    {
        public sealed class State
        {
            [JsonProperty("hub_id")] public string HubId { get; set; }
            [JsonProperty("character_id")] public string CharacterId { get; set; }
            [JsonProperty("generation")] public long Generation { get; set; }
            [JsonProperty("head_seq")] public long HeadSeq { get; set; }
            [JsonProperty("privacy_epoch")] public int PrivacyEpoch { get; set; }
            [JsonProperty("cursor")] public string Cursor { get; set; }
            [JsonProperty("items")] public List<JObject> Items { get; set; } = new List<JObject>();
            [JsonProperty("pending")] public List<YuiCompanionOperation> Pending { get; set; } = new List<YuiCompanionOperation>();
        }

        private sealed class Manifest
        {
            [JsonProperty("generation")] public long Generation { get; set; }
            [JsonProperty("file")] public string File { get; set; }
            [JsonProperty("sha256")] public string Sha256 { get; set; }
        }

        private readonly string folder;
        private readonly object stateLock = new object();
        private readonly string hubId;
        private readonly string characterId;
        public string HubId => hubId;
        public string CharacterId => characterId;

        public YuiCompanionReplicaStore(string root, string hubId, string characterId)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Invalid root.");
            ValidateId(hubId); ValidateId(characterId);
            this.hubId = hubId;
            this.characterId = characterId;
            folder = Path.Combine(root, "CompanionV2", hubId, characterId);
        }

        private static void ValidateId(string id)
        {
            if (id == null || id.Length != 32 || id.Any(c => !((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))))
                throw new ArgumentException("Invalid companion ID.");
        }

        private static string Digest(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private string ManifestPath => Path.Combine(folder, "manifest.json");

        public State Load()
        {
            lock (stateLock)
            {
            if (!File.Exists(ManifestPath))
                return new State { HubId = hubId, CharacterId = characterId };
            var manifest = JsonConvert.DeserializeObject<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.Generation < 1 ||
                manifest.File != "state-" + manifest.Generation + ".json" ||
                string.IsNullOrEmpty(manifest.Sha256))
                throw new InvalidDataException("Companion manifest is invalid.");
            var path = Path.Combine(folder, manifest.File);
            var bytes = File.ReadAllBytes(path);
            if (!string.Equals(Digest(bytes), manifest.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException("Companion state checksum mismatch.");
            var state = JsonConvert.DeserializeObject<State>(Encoding.UTF8.GetString(bytes));
            if (state == null || state.HubId != hubId || state.CharacterId != characterId ||
                state.Generation != manifest.Generation || state.HeadSeq < 0 || state.PrivacyEpoch < 0 ||
                state.Items == null || state.Pending == null)
                throw new InvalidDataException("Companion state is invalid.");
            // A process can stop after the manifest switch but before cleanup.
            // Do not retain an older generation containing corrected private text.
            DeleteStaleGenerations(manifest.File);
            return state;
            }
        }

        private void Save(State state)
        {
            state.Generation++;
            var name = "state-" + state.Generation + ".json";
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(state));
            YuiSyncFileTransaction.Write(Path.Combine(folder, name), bytes);
            var manifest = new Manifest { Generation = state.Generation, File = name, Sha256 = Digest(bytes) };
            YuiSyncFileTransaction.Write(ManifestPath, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest)));
            // Old generations can contain corrected or deleted private text.
            // Keep only the generation named by the committed manifest.
            DeleteStaleGenerations(name);
        }

        private void DeleteStaleGenerations(string currentFile)
        {
            foreach (var stale in Directory.GetFiles(folder, "state-*.json"))
                if (!string.Equals(Path.GetFileName(stale), currentFile, StringComparison.Ordinal))
                    File.Delete(stale);
        }

        public void Queue(YuiCompanionOperation operation)
        {
            lock (stateLock)
            {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            ValidateId(operation.OpId); ValidateId(operation.EntityId);
            var state = Load();
            var previous = state.Pending.Find(item => item.OpId == operation.OpId);
            if (previous != null)
            {
                if (!JToken.DeepEquals(JToken.FromObject(previous), JToken.FromObject(operation)))
                    throw new InvalidOperationException("Companion operation ID reused with different content.");
                return;
            }
            state.Pending.Add(operation);
            Save(state);
            }
        }

        public void Acknowledge(IEnumerable<string> acceptedOpIds)
        {
            lock (stateLock)
            {
            if (acceptedOpIds == null) throw new ArgumentNullException(nameof(acceptedOpIds));
            var accepted = new HashSet<string>(acceptedOpIds);
            var state = Load();
            if (state.Pending.RemoveAll(item => accepted.Contains(item.OpId)) > 0) Save(state);
            }
        }

        public void ReplaceSnapshot(IEnumerable<JObject> items, long headSeq, int privacyEpoch, string changeCursor)
        {
            lock (stateLock)
            {
            if (items == null || headSeq < 0 || privacyEpoch < 0 || string.IsNullOrEmpty(changeCursor))
                throw new ArgumentException("Incomplete companion snapshot.");
            var next = Load();
            var copied = items.Select(item => (JObject)item.DeepClone()).ToList();
            if (copied.Any(item => item["entity_type"] == null || item["entity_id"] == null))
                throw new InvalidDataException("Companion snapshot item has no identity.");
            if (copied.Select(Key).Distinct().Count() != copied.Count)
                throw new InvalidDataException("Duplicate companion snapshot item.");
            next.Items = copied;
            next.HeadSeq = headSeq;
            next.PrivacyEpoch = privacyEpoch;
            next.Cursor = changeCursor;
            Save(next);
            }
        }

        public void ApplyChanges(YuiCompanionChangePage page, string requestedCursor)
        {
            lock (stateLock)
            {
            if (page == null || page.Items == null || string.IsNullOrEmpty(page.NextCursor))
                throw new ArgumentException("Incomplete companion change page.");
            var state = Load();
            if (state.Cursor != requestedCursor || state.PrivacyEpoch != page.PrivacyEpoch ||
                page.HeadSeq < state.HeadSeq)
                throw new InvalidOperationException("Companion cursor or privacy epoch changed; load a snapshot.");
            var lookup = state.Items.ToDictionary(Key, item => item);
            long lastSeq = state.HeadSeq;
            foreach (var item in page.Items)
            {
                if (item == null || item["seq"] == null || item["revision"] == null)
                    throw new InvalidDataException("Invalid companion change item.");
                long seq = (long)item["seq"];
                if (seq <= lastSeq || seq > page.HeadSeq)
                    throw new InvalidDataException("Out-of-order companion change item.");
                lastSeq = seq;
                var copy = (JObject)item.DeepClone();
                copy.Remove("seq");
                lookup[Key(copy)] = copy;
            }
            state.Items = lookup.Values.ToList();
            state.HeadSeq = lastSeq;
            state.Cursor = page.NextCursor;
            Save(state);
            }
        }

        private static string Key(JObject item)
        {
            var kind = (string)item["entity_type"];
            var id = (string)item["entity_id"];
            if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(id))
                throw new InvalidDataException("Companion item has no identity.");
            return kind + ":" + id;
        }
    }
}
