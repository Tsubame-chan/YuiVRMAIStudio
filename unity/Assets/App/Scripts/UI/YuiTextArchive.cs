using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace YuiPhysicalAI.UI
{
    // Durable text history, separate from the bounded context sent to an LLM.
    // Appends never rewrite older records; browsing reads backwards in bounded blocks.
    public sealed class YuiTextArchive
    {
        public sealed class Entry
        {
            public string Id, CreatedUtc, Speaker, Text, Mode, Metadata;
            public bool Deleted;
        }
        public sealed class Page
        {
            public readonly List<Entry> Items = new List<Entry>();
            public bool HasOlder;
            public int DamagedLines;
        }
        private static readonly object WriteGate = new object();
        private readonly string path;
        public YuiTextArchive(string path) { this.path = path; }
        public long Length => File.Exists(path) ? new FileInfo(path).Length : 0;

        public void Remove(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Entry ID required.");
            lock (WriteGate)
            {
                if (!File.Exists(path)) return;
                var temp = path + ".rewrite-" + Guid.NewGuid().ToString("N");
                try
                {
                    using (var output = new StreamWriter(temp, false, new UTF8Encoding(false)))
                        foreach (var line in File.ReadLines(path))
                        {
                            Entry item = null;
                            try { item = JsonConvert.DeserializeObject<Entry>(line); } catch (JsonException) { }
                            if (item?.Id != id) output.WriteLine(line);
                        }
                    using (var output = new FileStream(temp, FileMode.Open, FileAccess.Write)) output.Flush(true);
                    File.Replace(temp, path, null);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        }

        public void Append(Entry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Id)) throw new ArgumentException("Archive entry needs an ID.");
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entry, Formatting.None) + "\n");
            lock (WriteGate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                // Retain an interrupted final record, but do not join the next record to it.
                if (stream.Length > 0)
                {
                    stream.Position = stream.Length - 1;
                    if (stream.ReadByte() != 10) { stream.Position = stream.Length; stream.WriteByte(10); }
                }
                stream.Position = stream.Length;
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public Page ReadPage(int offset, int limit, string mode = null, long snapshotLength = -1)
        {
            var page = new Page();
            if (!File.Exists(path)) return page;
            offset = Math.Max(0, offset); limit = Math.Max(1, limit);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in ReverseLines(path, snapshotLength))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                Entry item;
                try { item = JsonConvert.DeserializeObject<Entry>(line); }
                catch (JsonException) { page.DamagedLines++; continue; }
                if (item == null || string.IsNullOrEmpty(item.Id)) { page.DamagedLines++; continue; }
                if (!seen.Add(item.Id) || item.Deleted || (mode != null && item.Mode != mode)) continue;
                if (offset > 0) { offset--; continue; }
                if (page.Items.Count == limit) { page.HasOlder = true; break; }
                page.Items.Add(item);
            }
            return page;
        }

        private static IEnumerable<string> ReverseLines(string file, long snapshotLength)
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long position = snapshotLength < 0 ? stream.Length : Math.Min(snapshotLength, stream.Length);
            var block = new byte[4096];
            var line = new List<byte>();
            while (position > 0)
            {
                var count = (int)Math.Min(block.Length, position); position -= count;
                stream.Position = position;
                var read = 0;
                while (read < count)
                {
                    var n = stream.Read(block, read, count - read);
                    if (n == 0) throw new EndOfStreamException("Archive changed while reading.");
                    read += n;
                }
                for (var i = count - 1; i >= 0; i--)
                {
                    if (block[i] != 10) { line.Add(block[i]); continue; }
                    if (line.Count == 0) continue;
                    line.Reverse(); yield return Encoding.UTF8.GetString(line.ToArray()); line.Clear();
                }
            }
            if (line.Count > 0) { line.Reverse(); yield return Encoding.UTF8.GetString(line.ToArray()); }
        }
    }
}
