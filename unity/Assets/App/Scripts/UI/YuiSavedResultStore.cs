using System;
using System.IO;
using System.Linq;

namespace YuiPhysicalAI.UI
{
    // One append-only collection. Legacy Markdown and sidecars remain recoverable.
    public sealed class YuiSavedResultStore
    {
        private static readonly object MigrationGate = new object();
        private readonly string directory;
        private YuiTextArchive ArchiveStore => new YuiTextArchive(Path.Combine(directory, "answers.jsonl"));
        public YuiSavedResultStore(string directory) { this.directory = directory; }
        public YuiTextArchive.Page Page(int offset, int limit) { MigrateLegacy(); return ArchiveStore.ReadPage(offset, limit); }
        public string[] List() => Page(0, int.MaxValue).Items.Select(x => x.Id).ToArray();
        public string Read(string id) => Find(id).Text;
        public string Metadata(string id) => Find(id).Metadata;
        private YuiTextArchive.Entry Find(string id)
        {
            Validate(id);
            return Page(0, int.MaxValue).Items.FirstOrDefault(x => x.Id == id) ?? throw new FileNotFoundException("Saved answer not found.");
        }
        private static void Validate(string id)
        {
            if (string.IsNullOrEmpty(id) || id != Path.GetFileName(id) || id.Contains("\\") || id.Contains(":"))
                throw new ArgumentException("Invalid saved answer ID.");
        }
        public string Save(string text, string metadata)
        {
            MigrateLegacy();
            var id = "answer_" + Guid.NewGuid().ToString("N");
            ArchiveStore.Append(new YuiTextArchive.Entry { Id = id, CreatedUtc = DateTime.UtcNow.ToString("o"), Text = text ?? "", Metadata = metadata });
            return id;
        }
        public void Remove(string id)
        {
            Validate(id); MigrateLegacy(); ArchiveStore.Remove(id);
            var original=Path.Combine(directory,id);
            if(File.Exists(original)) File.Delete(original);
            if(File.Exists(original+".json")) File.Delete(original+".json");
        }
        public void Archive(string id)
        {
            var item = Find(id); item.Deleted = true; ArchiveStore.Append(item);
            var original = Path.Combine(directory, id);
            if (!File.Exists(original)) return;
            var trash = Path.Combine(directory, "Archived", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(trash); File.Move(original, Path.Combine(trash, id));
            if (File.Exists(original + ".json")) File.Move(original + ".json", Path.Combine(trash, id + ".json"));
        }
        private void MigrateLegacy()
        {
            var marker = Path.Combine(directory, ".markdown-imported-v1");
            lock (MigrationGate)
            {
                if (File.Exists(marker)) return;
                Directory.CreateDirectory(directory);
                foreach (var file in Directory.GetFiles(directory, "*.md").OrderBy(x => x, StringComparer.Ordinal))
                    ArchiveStore.Append(new YuiTextArchive.Entry { Id = Path.GetFileName(file),
                        CreatedUtc = File.GetLastWriteTimeUtc(file).ToString("o"), Text = File.ReadAllText(file),
                        Metadata = File.Exists(file + ".json") ? File.ReadAllText(file + ".json") : null });
                File.WriteAllText(marker, "Imported into answers.jsonl; original Markdown files retained.\n");
            }
        }
    }
}
