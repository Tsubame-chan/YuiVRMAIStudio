using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace YuiPhysicalAI.Avatar
{
    [Serializable]
    public sealed class YuiCharacterProfile
    {
        public string Name = "Yui", Instruction = "", TtsMode = "server", VoiceGender = "female", VoiceInstruction = "";
        public int SpeakerId = 14;
        public float Speed = 1, Pitch, Intonation = 1, SynthesisVolume = 1, PrePhoneme = .1f, PostPhoneme = .1f;
    }
    public sealed class YuiCharacterProfileStore
    {
        private readonly string directory;
        public YuiCharacterProfileStore(string directory) { this.directory = directory; }
        private string FileFor(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Character ID required");
            using var sha = SHA256.Create();
            var hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-", "").ToLowerInvariant();
            return Path.Combine(directory, hash + ".json");
        }
        public YuiCharacterProfile Read(string id)
        {
            var file = FileFor(id);
            if (!File.Exists(file)) return null;
            return JsonConvert.DeserializeObject<YuiCharacterProfile>(File.ReadAllText(file))
                ?? throw new InvalidDataException("Character profile is invalid; the original file has been preserved.");
        }
        public void Save(string id, YuiCharacterProfile profile)
        {
            Directory.CreateDirectory(directory);
            var file = FileFor(id); var temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temp, JsonConvert.SerializeObject(profile, Formatting.Indented));
                if (File.Exists(file)) File.Replace(temp, file, file + ".bak"); else File.Move(temp, file);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
