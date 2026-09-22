using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace YuiPhysicalAI.Avatar
{
    // Bounded recent dialogue, separate from the backend's curated long-term memories.
    public sealed class YuiCharacterDialogueStore
    {
        public const string ContextKey = "recent_character_dialogue";
        public sealed class Turn { public string User, Assistant; }
        private readonly string directory;
        public YuiCharacterDialogueStore(string directory) { this.directory = directory; }
        private string PathFor(string character, string mode)
        {
            if (string.IsNullOrWhiteSpace(character)) throw new ArgumentException("Character required");
            using var sha = SHA256.Create();
            var key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(character + "\n" + mode))).Replace("-", "").ToLowerInvariant();
            return Path.Combine(directory, key + ".json");
        }
        public List<Turn> Read(string character, string mode)
        {
            var file = PathFor(character, mode);
            if (!File.Exists(file)) return new List<Turn>();
            var turns = JsonConvert.DeserializeObject<List<Turn>>(File.ReadAllText(file));
            if (turns == null || turns.Any(t => t == null)) throw new InvalidDataException("Recent dialogue is damaged; the original is retained.");
            return turns.Skip(Math.Max(0, turns.Count - 4)).Select(t => new Turn { User = Limit(t.User), Assistant = Limit(t.Assistant) }).ToList();
        }
        private static string Limit(string text) => string.IsNullOrEmpty(text) ? "" : text.Substring(0, Math.Min(text.Length, 600));
        public string Context(string character, string mode) => JsonConvert.SerializeObject(Read(character, mode));
        public void Append(string character, string mode, string user, string assistant)
        {
            var turns = Read(character, mode);
            turns.Add(new Turn { User = Limit(user), Assistant = Limit(assistant) });
            if (turns.Count > 4) turns.RemoveAt(0);
            Directory.CreateDirectory(directory);
            var file = PathFor(character, mode); var temp = file + ".tmp";
            try {
                File.WriteAllText(temp, JsonConvert.SerializeObject(turns));
                if (File.Exists(file)) File.Replace(temp, file, null); else File.Move(temp, file);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public void Clear(string character, string mode) { var file = PathFor(character, mode); if (File.Exists(file)) File.Delete(file); }
        public static string FromExtra(IDictionary<string, object> extra)
        {
            if (extra == null || !extra.TryGetValue(ContextKey, out var value)) return "";
            var text = value as string ?? "";
            return text.Length > 6000 ? text.Substring(0, 6000) : text;
        }
    }
}
