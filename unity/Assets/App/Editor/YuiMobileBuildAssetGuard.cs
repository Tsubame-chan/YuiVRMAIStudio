using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace YuiPhysicalAI.Editor
{
    // Mobile does not use the desktop first-run downloader. A successful build
    // without bundled standard data would leave a new user unable to converse.
    public sealed class YuiMobileBuildAssetGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => -200;
        public static readonly string[] RequiredFiles = {
            "StreamingAssets/YuiLocalAI/Models/gemma-4-E2B-it.litertlm",
            "StreamingAssets/YuiLocalAI/Voicevox/Models/meimei_himari_1.vvm",
            "StreamingAssets/YuiLocalAI/Voicevox/Models/metan_zundamon_0.vvm",
            "StreamingAssets/YuiLocalAI/Voicevox/Models/kyushu_sora_2.vvm",
            "StreamingAssets/YuiLocalAI/Voicevox/Models/sayo_15.vvm",
            "StreamingAssets/YuiLocalAI/Voicevox/open_jtalk_dic_utf_8-1.11/sys.dic"
        };
        public static List<string> MissingFiles(string assetsDirectory)
        {
            var missing = new List<string>();
            foreach (var relative in RequiredFiles)
            {
                var file = new FileInfo(Path.Combine(assetsDirectory, relative));
                if (!file.Exists || file.Length == 0) missing.Add(relative);
            }
            return missing;
        }
        public static bool IsGeneratedModelCache(string path)
        {
            var name = Path.GetFileName(path);
            if (name.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 5);
            return name.EndsWith(".xnnpack_cache", System.StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".task_cache", System.StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".litert_cache", System.StringComparison.OrdinalIgnoreCase)
                || (name.Contains("_mldrift_") && name.EndsWith("_cache.bin", System.StringComparison.OrdinalIgnoreCase));
        }
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS && report.summary.platform != BuildTarget.Android) return;
            var missing = MissingFiles(Application.dataPath);
            if (missing.Count != 0) throw new BuildFailedException(
                "Mobile builds require bundled local AI/voice data; the desktop downloader cannot restore it. Prepare the standard E2B model, VOICEVOX voice and dictionary before building. Missing or empty:\n" + string.Join("\n", missing));
            foreach (var file in Directory.GetFiles(Path.Combine(Application.dataPath, "StreamingAssets/YuiLocalAI/Models")))
                if (IsGeneratedModelCache(file)) throw new BuildFailedException("Do not bundle machine-generated inference caches. Use the model build scope or move this cache out before building: " + Path.GetFileName(file));
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            // An incremental Xcode export can retain old files no longer in the
            // source payload. Remove only recognized generated cache files.
            var directory = Path.Combine(report.summary.outputPath, "Data/Raw/YuiLocalAI/Models");
            if (!Directory.Exists(directory)) return;
            foreach (var file in Directory.GetFiles(directory))
                if (IsGeneratedModelCache(file)) File.Delete(file);
        }
    }
}
