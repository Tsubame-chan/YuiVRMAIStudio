using UnityEngine;

namespace YuiPhysicalAI.Core
{
    public static class YuiAvatarSelectionPrefs
    {
        // App relocation or an update must not select a different character.
        public static string Key => YuiPrefsKeys.AvatarSlot + ".app." + (Application.isEditor ? "editor" : Application.identifier);
        public static string Read(string defaultSlot)
        {
            if (PlayerPrefs.HasKey(Key)) return PlayerPrefs.GetString(Key, defaultSlot);
            var source = string.IsNullOrWhiteSpace(Application.dataPath) ? Application.identifier : Application.dataPath;
            var oldKey = YuiPrefsKeys.AvatarSlot + "." + LegacyHash(source ?? "default");
            var value = PlayerPrefs.GetString(oldKey, defaultSlot);
            if (PlayerPrefs.HasKey(oldKey)) { PlayerPrefs.SetString(Key, value); PlayerPrefs.Save(); }
            return value;
        }
        private static string LegacyHash(string value)
        {
            unchecked { uint hash = 2166136261; foreach (var character in value) { hash ^= character; hash *= 16777619; } return hash.ToString("x8"); }
        }
    }
}
