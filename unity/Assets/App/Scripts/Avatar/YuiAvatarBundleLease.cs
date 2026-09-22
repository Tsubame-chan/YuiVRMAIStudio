using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace YuiPhysicalAI.Avatar
{
    // Unity objects and this cache are accessed on the main thread. Every instantiated
    // avatar owns one lease; replacing one avatar must not unload another's materials.
    public sealed class YuiAvatarBundleLease : MonoBehaviour
    {
        private sealed class Entry
        {
            public Task<AssetBundle> Loading;
            public AssetBundle Bundle;
            public int References;
        }
        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private string key;

        public static async Task<AssetBundle> AcquireAsync(string path)
        {
            if (!Entries.TryGetValue(path, out var entry))
            {
                entry = new Entry();
                Entries.Add(path, entry);
                entry.Loading = LoadAsync(path, entry);
            }
            entry.References++;
            try { return await entry.Loading; }
            catch { Release(path); throw; }
        }

        private static async Task<AssetBundle> LoadAsync(string path, Entry entry)
        {
            var request = AssetBundle.LoadFromFileAsync(path);
            var completion = new TaskCompletionSource<bool>();
            if (!request.isDone) { request.completed += _ => completion.TrySetResult(true); await completion.Task; }
            entry.Bundle = request.assetBundle;
            if (entry.Bundle == null) throw new System.IO.InvalidDataException("この端末でアバターを読み込めません。端末用の出力先OSとUnity版を確認して再書出ししてください。");
            return entry.Bundle;
        }

        public static bool IsInUse(string path) => Entries.ContainsKey(path);

        public void Own(string path) { key = path; }
        private void OnDestroy() { ReleaseOwner(); }
        public void ReleaseOwner() { if (key != null) { Release(key); key = null; } }
        public static void Release(string path)
        {
            if (!Entries.TryGetValue(path, out var entry)) return;
            if (--entry.References > 0) return;
            Entries.Remove(path);
            if (entry.Bundle != null) entry.Bundle.Unload(true);
        }
    }
}
