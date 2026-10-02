using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiLocalModelSelection
    {
        public const string StandardId = "core_text_e2b", QualityId = "core_text";
        public const string PreferenceKey = "yui.local-model.selected";
        public static string SelectedId => PlayerPrefs.GetString(PreferenceKey, StandardId);
        public static bool Installed(YuiLocalAiModelPack pack) => pack != null &&
            (File.Exists(YuiLocalAiModelPathResolver.PersistentModelPath(pack)) || File.Exists(YuiLocalAiModelPathResolver.StreamingAssetsModelPath(pack)));
        public static YuiLocalAiModelPack Find(string id) => YuiLocalAiModelRegistry.FromStreamingAssetsOrDefault().Packs.FirstOrDefault(p => p.Id == id);
        public static IEnumerable<YuiLocalAiModelPack> Available =>
            YuiLocalAiModelRegistry.FromStreamingAssetsOrDefault().EnabledFor(YuiLocalAiCapability.Chat)
                .Where(p => p.DeploymentKind == YuiLocalAiDeploymentKind.OnDeviceEmbedded
                    && string.Equals(p.Format, "litert-lm", System.StringComparison.OrdinalIgnoreCase));
        public static string Name(YuiLocalAiModelPack pack) => pack == null ? "—" :
            pack.Id == StandardId ? "Gemma 4 E2B" : pack.Id == QualityId ? "Gemma 4 E4B" : pack.DisplayName;
        public static void MigrateInstalledChoice()
        {
            if (PlayerPrefs.HasKey(PreferenceKey)) return;
            // Keep existing E4B-only installations usable without another forced download.
            Select(Installed(Find(StandardId)) || !Installed(Find(QualityId)) ? StandardId : QualityId);
        }
        public static void Select(string id) { PlayerPrefs.SetString(PreferenceKey, id); PlayerPrefs.Save(); }
        public static IEnumerable<YuiLocalAiModelPack> Order(IEnumerable<YuiLocalAiModelPack> packs, string selected) =>
            packs.OrderBy(p => p.Id == selected ? 0 : 1).ThenBy(p => p.Priority);
    }
}
