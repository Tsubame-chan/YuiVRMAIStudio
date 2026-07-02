using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiAivisNativeVoiceCatalog
    {
        private const string CatalogFileName = "aivis_voices.json";
        private static string cachedRoot;
        private static YuiAivisNativeCatalog cachedCatalog;

        public static YuiAivisNativeCatalog Load(string rootPath)
        {
            if (cachedCatalog != null && string.Equals(cachedRoot, rootPath, StringComparison.Ordinal))
            {
                return cachedCatalog;
            }

            var catalogPath = Path.Combine(rootPath, CatalogFileName);
            if (!File.Exists(catalogPath))
            {
                Debug.LogWarning($"Aivis native voice catalog was not found: {catalogPath}");
                cachedRoot = rootPath;
                cachedCatalog = new YuiAivisNativeCatalog();
                return cachedCatalog;
            }

            try
            {
                cachedRoot = rootPath;
                cachedCatalog = JsonConvert.DeserializeObject<YuiAivisNativeCatalog>(
                                    File.ReadAllText(catalogPath))
                                ?? new YuiAivisNativeCatalog();
                return cachedCatalog;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load Aivis native voice catalog: {ex.Message}");
                cachedRoot = rootPath;
                cachedCatalog = new YuiAivisNativeCatalog();
                return cachedCatalog;
            }
        }

        public static YuiAivisNativeVoice FindVoice(string rootPath, int voiceId)
        {
            var catalog = Load(rootPath);
            if (catalog.Voices == null || catalog.Voices.Length == 0)
            {
                return null;
            }

            foreach (var voice in catalog.Voices)
            {
                if (voice != null && voice.Id == voiceId)
                {
                    return voice;
                }
            }

            foreach (var voice in catalog.Voices)
            {
                if (voice != null && voice.Id == catalog.DefaultVoiceId)
                {
                    return voice;
                }
            }

            return catalog.Voices[0];
        }
    }

    [Serializable]
    public sealed class YuiAivisNativeCatalog
    {
        [JsonProperty("schema_version")] public string SchemaVersion { get; set; }
        [JsonProperty("default_voice_id")] public int DefaultVoiceId { get; set; } = 1431611904;
        [JsonProperty("voices")] public YuiAivisNativeVoice[] Voices { get; set; } = Array.Empty<YuiAivisNativeVoice>();
    }

    [Serializable]
    public sealed class YuiAivisNativeVoice
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("key")] public string Key { get; set; }
        [JsonProperty("display_name")] public string DisplayName { get; set; }
        [JsonProperty("model_path")] public string ModelPath { get; set; }
        [JsonProperty("hyper_parameters_path")] public string HyperParametersPath { get; set; }
        [JsonProperty("manifest_path")] public string ManifestPath { get; set; }
        [JsonProperty("style_vectors_path")] public string StyleVectorsPath { get; set; }
        [JsonProperty("speaker_id")] public int SpeakerId { get; set; }
        [JsonProperty("voicevox_speaker_id")] public int VoicevoxSpeakerId { get; set; } = 14;
        [JsonProperty("speaker_name")] public string SpeakerName { get; set; }
        [JsonProperty("default_style_id")] public int DefaultStyleId { get; set; }
        [JsonProperty("default_style_name")] public string DefaultStyleName { get; set; }
        [JsonProperty("style_count")] public int StyleCount { get; set; }
        [JsonProperty("sampling_rate")] public int SamplingRate { get; set; } = 44100;
        [JsonProperty("hop_length")] public int HopLength { get; set; } = 512;
        [JsonProperty("version")] public string Version { get; set; }
        [JsonProperty("runtime")] public string Runtime { get; set; }
        [JsonProperty("platforms")] public string[] Platforms { get; set; } = Array.Empty<string>();
    }
}
