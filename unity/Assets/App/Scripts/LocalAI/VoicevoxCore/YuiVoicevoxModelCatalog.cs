using System.IO;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiVoicevoxModelCatalog
    {
        public static string FileName(int styleId)
        {
            switch (styleId)
            {
                case 14: return "meimei_himari_1.vvm";
                case 2: case 3: return "metan_zundamon_0.vvm";
                case 16: return "kyushu_sora_2.vvm";
                case 46: return "sayo_15.vvm";
                default: return null;
            }
        }
        public static bool IsAvailable(int styleId)
        {
            var file = FileName(styleId);
            if (file == null) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
            // These files are required by the mobile build asset guard.
            if (Application.streamingAssetsPath.StartsWith("jar:")) return true;
#endif
            return File.Exists(ModelPath(styleId));
        }
        public static string ModelPath(int styleId)
        {
            var file = FileName(styleId);
            if (file == null) throw new System.ArgumentOutOfRangeException(nameof(styleId), "Unsupported on-device voice.");
            var downloaded = Path.Combine(YuiLocalAiPathResolver.VoicevoxRootPath(), "Models", file);
            if (File.Exists(downloaded)) return downloaded;
            // An older downloaded voice directory must not hide newly bundled voices.
            var bundled = Path.Combine(Application.streamingAssetsPath, "YuiLocalAI", "Voicevox", "Models", file);
            return File.Exists(bundled) ? bundled : downloaded;
        }
    }
}
