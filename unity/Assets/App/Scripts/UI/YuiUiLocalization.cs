using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    // Presentation only: do not use translated labels as IDs, device names or prompt text.
    public static class YuiUiLocalization
    {
        [Serializable] private sealed class Entry { public string en; public string ja; public string[] aliases; }
        [Serializable] private sealed class Catalog { public Entry[] entries; }
        private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private sealed class Format { public Regex pattern; public Entry entry; }
        private static readonly List<Format> formats = new List<Format>();
        private sealed class Prefix { public string source; public Entry entry; }
        private static readonly List<Prefix> prefixes = new List<Prefix>();
        private static string language;
        public static event Action Changed;
        public static string ResolveLanguage(string saved, SystemLanguage system) => saved == "ja" || saved == "en"
            ? saved : system == SystemLanguage.Japanese ? "ja" : "en";
        public static string Language => language ?? (language = ResolveLanguage(
            PlayerPrefs.GetString(YuiPrefsKeys.UiLanguage, ""), Application.systemLanguage));
        public static void SetLanguage(string value)
        {
            if (value != "ja" && value != "en") throw new ArgumentException("Unsupported UI language", nameof(value));
            language = value;
            PlayerPrefs.SetString(YuiPrefsKeys.UiLanguage, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
        private static void EnsureCatalog()
        {
            if (entries.Count > 0) return;
            var asset = Resources.Load<TextAsset>("YuiLocalization/UiStrings");
            if (asset == null) return;
            foreach (var entry in JsonUtility.FromJson<Catalog>(asset.text).entries)
            {
                entries[entry.en] = entry;
                // Accept legacy Japanese captions at the view boundary too.
                if (!entries.ContainsKey(entry.ja)) entries[entry.ja] = entry;
                foreach (var alias in entry.aliases ?? Array.Empty<string>()) if (!entries.ContainsKey(alias)) entries[alias] = entry;
                // Legacy copy is accepted only at presentation boundaries. Aliases
                // must also match formatted messages after editorial revisions.
                var variants = new HashSet<string>(entry.aliases ?? Array.Empty<string>(), StringComparer.Ordinal) { entry.en, entry.ja };
                foreach (var pattern in variants)
                {
                    if (pattern.Contains("{0}"))
                    {
                        var escaped = Regex.Escape(pattern);
                        for (var i = 0; i < 8; i++) escaped = escaped.Replace(Regex.Escape("{" + i + "}"), "(.*?)");
                        formats.Add(new Format { pattern = new Regex("^" + escaped + "$", RegexOptions.Singleline), entry = entry });
                    }
                    if (pattern.EndsWith(": ", StringComparison.Ordinal)) prefixes.Add(new Prefix { source = pattern, entry = entry });
                }
            }
        }
        public static string Text(string source) => Translate(source, Language);
        public static string Translate(string source, string locale)
        {
            if (string.IsNullOrEmpty(source)) return source ?? "";
            EnsureCatalog();
            if (entries.TryGetValue(source, out var entry)) return locale == "ja" ? entry.ja : entry.en;
            foreach (var format in formats)
            {
                var match = format.pattern.Match(source);
                if (!match.Success) continue;
                var args = new object[match.Groups.Count - 1];
                for (var i = 1; i < match.Groups.Count; i++) args[i - 1] = match.Groups[i].Value;
                return string.Format(locale == "ja" ? format.entry.ja : format.entry.en, args);
            }
            foreach (var prefix in prefixes)
                if (source.StartsWith(prefix.source, StringComparison.Ordinal))
                    return (locale == "ja" ? prefix.entry.ja : prefix.entry.en) + source.Substring(prefix.source.Length);
            // These delimiters join app-owned labels; arbitrary names are never passed here.
            foreach (var delimiter in new[] { "\n", " · " })
                if (source.Contains(delimiter))
                {
                    var parts = source.Split(new[] { delimiter }, StringSplitOptions.None);
                    for (var i = 0; i < parts.Length; i++) parts[i] = Translate(parts[i], locale);
                    return string.Join(delimiter, parts);
                }
            return source;
        }
        public static void Set(Text label, string source, bool japaneseParagraph = false)
        {
            if (label == null) return;
            var binding = label.GetComponent<YuiLocalizedLabel>() ?? label.gameObject.AddComponent<YuiLocalizedLabel>();
            binding.SetSource(source, japaneseParagraph);
        }
        public static void BindKnownLabels(Transform root)
        {
            if (root == null) return;
            EnsureCatalog();
            foreach (var label in root.GetComponentsInChildren<Text>(true))
            {
                if (label.GetComponentInParent<Dropdown>(true) != null) continue;
                var input = label.GetComponentInParent<InputField>(true);
                if (input != null && label != input.placeholder) continue;
                if (label.GetComponent<YuiLocalizedLabel>() == null && entries.ContainsKey(label.text) && label.GetComponentInParent<YuiChatMessageBubble>(true) == null)
                    Set(label, label.text);
            }
        }
    }
}
