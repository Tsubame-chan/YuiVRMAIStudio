using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace YuiPhysicalAI.Avatar
{
    // File I/O is independent of Unity. Identity belongs to the library entry;
    // file/hash identify an appearance revision. An update is always explicit.
    public sealed class YuiAvatarLibraryStore
    {
        private sealed class Index { public List<YuiAvatarLibrary.Entry> entries; }
        private readonly string directory;
        private static readonly object Gate = new object();
        public YuiAvatarLibraryStore(string directory) { this.directory = directory; }
        public static bool SafeName(string value) => !string.IsNullOrEmpty(value) && value != "." && value != ".."
            && value.IndexOfAny(new [] { '/', '\\', ':' }) < 0 && value == Path.GetFileName(value);
        public string Resolve(YuiAvatarLibrary.Entry entry)
        {
            if (entry == null || !SafeName(entry.file)) throw new ArgumentException("Invalid avatar library entry");
            return Path.Combine(directory, entry.file);
        }
        public List<YuiAvatarLibrary.Entry> Read()
        {
            lock (Gate)
            {
                var path = Path.Combine(directory, "index.json");
                if (!File.Exists(path)) return new List<YuiAvatarLibrary.Entry>();
                var index = JsonConvert.DeserializeObject<Index>(File.ReadAllText(path));
                if (index?.entries == null || index.entries.Any(e => e == null || !SafeName(e.id) || !SafeName(e.file))
                    || index.entries.Select(e => e.id).Distinct().Count() != index.entries.Count)
                    throw new InvalidDataException("アバター一覧が破損しています。index.jsonとindex.json.bakを保持して復元してください。");
                foreach (var entry in index.entries)
                {
                    if (entry.appearances == null) entry.appearances = new List<YuiAvatarLibrary.Appearance>();
                    if (entry.appearances.Any(a => a == null || !SafeName(a.file)))
                        throw new InvalidDataException("保存済みの外見一覧が破損しています。");
                    if (!entry.appearances.Any(a => a.file == entry.file))
                        entry.appearances.Add(new YuiAvatarLibrary.Appearance { name = entry.name, file = entry.file });
                }
                return index.entries;
            }
        }
        public YuiAvatarLibrary.Entry Register(string source, string replaceId = null, System.Threading.CancellationToken cancellationToken = default)
        {
            lock (Gate)
            {
                var rows = Read(); // A corrupt index must never be silently replaced with an empty one.
                var old = replaceId == null ? null : rows.SingleOrDefault(e => e.id == replaceId)
                    ?? throw new InvalidDataException("更新先のキャラクターが見つかりません。");
                Directory.CreateDirectory(directory);
                var extension = Path.GetExtension(source).ToLowerInvariant();
                if (extension != ".vrm" && extension != ".zip") throw new ArgumentException("VRMまたはBridge ZIPを選んでください。");
                var stage = Path.Combine(directory, ".import-" + Guid.NewGuid().ToString("N"));
                try
                {
                    // Hash the staged copy, so the indexed revision is the actual saved bytes.
                    using (var input = File.OpenRead(source))
                    using (var output = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[1024 * 1024]; int count;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0) { cancellationToken.ThrowIfCancellationRequested(); output.Write(buffer, 0, count); }
                    }
                    string hash;
                    using (var sha = SHA256.Create()) using (var stream = File.OpenRead(stage))
                        hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    var filename = hash + extension;
                    var entry = new YuiAvatarLibrary.Entry {
                        id = old?.id ?? (rows.Any(e => e.id == hash) ? Guid.NewGuid().ToString("N") : hash),
                        name = old?.name ?? Path.GetFileNameWithoutExtension(source), file = filename,
                        appearances = old?.appearances ?? new List<YuiAvatarLibrary.Appearance>() };
                    if (!entry.appearances.Any(a => a.file == filename))
                    {
                        // Reusing an appearance from another character must not rename it to its content hash.
                        var known = rows.SelectMany(e => e.appearances).FirstOrDefault(a => a.file == filename);
                        entry.appearances.Add(new YuiAvatarLibrary.Appearance { name = known?.name ?? Path.GetFileNameWithoutExtension(source), file = filename });
                    }
                    var target = Resolve(entry);
                    if (!File.Exists(target)) File.Move(stage, target);
                    cancellationToken.ThrowIfCancellationRequested();
                    rows.RemoveAll(e => e.id == entry.id); rows.Add(entry); Write(rows);
                    return entry;
                }
                finally { if (File.Exists(stage)) File.Delete(stage); }
            }
        }
        public void RestoreAppearance(YuiAvatarLibrary.Entry previous, string expectedFile)
        {
            lock (Gate) {
                var rows = Read(); var current = rows.SingleOrDefault(e => e.id == previous.id);
                if (current == null || current.file != expectedFile) return;
                current.file = previous.file; Write(rows);
            }
        }
        public void Rename(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            lock (Gate) { var rows = Read(); var row = rows.SingleOrDefault(e => e.id == id); if (row != null) { row.name = name.Trim(); Write(rows); } }
        }
        public void RenameAppearance(string id, string file, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            lock (Gate) {
                var rows = Read();
                var appearance = rows.SingleOrDefault(e => e.id == id)?.appearances.Find(a => a.file == file);
                if (appearance != null) { appearance.name = name.Trim(); Write(rows); }
            }
        }
        public void Remove(string id) { lock (Gate) { var rows = Read(); rows.RemoveAll(e => e.id == id); Write(rows); } }
        private void Write(List<YuiAvatarLibrary.Entry> rows)
        {
            var target = Path.Combine(directory, "index.json");
            var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temp, JsonConvert.SerializeObject(new Index { entries = rows }, Formatting.Indented));
                if (File.Exists(target)) File.Replace(temp, target, target + ".bak"); else File.Move(temp, target);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
