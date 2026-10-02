using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

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
        public static List<string> MissingFiles(string assetsDirectory, bool appleHostedModel = false)
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
            var group = BuildPipeline.GetBuildTargetGroup(report.summary.platform);
            if (report.summary.platform == BuildTarget.iOS && PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(';').Contains("YUI_PROFILE_PUBLIC"))
                ValidatePublicIosPayload(Application.dataPath);
            var appleHosted = report.summary.platform == BuildTarget.iOS
                && Environment.GetEnvironmentVariable("YUI_APPLE_HOSTED_ASSETS") == "1";
            if (appleHosted) ValidateAppleAssetArchive(Environment.GetEnvironmentVariable("YUI_APPLE_ASSET_ARCHIVE"));
            var missing = MissingFiles(Application.dataPath, appleHosted);
            if (missing.Count != 0) throw new BuildFailedException(
                "Mobile builds require bundled local AI/voice data; the desktop downloader cannot restore it. Prepare the standard E2B model, VOICEVOX voice and dictionary before building. Missing or empty:\n" + string.Join("\n", missing));
            var modelDirectory = Path.Combine(Application.dataPath, "StreamingAssets/YuiLocalAI/Models");
            if (appleHosted && Directory.Exists(modelDirectory) && Directory.GetFiles(modelDirectory, "*.litertlm")
                .Any(path => Path.GetFileName(path) != "gemma-4-E2B-it.litertlm"))
                throw new BuildFailedException("Bundle only the standard E2B model. E4B is an optional Apple-hosted download.");
            foreach (var file in Directory.Exists(modelDirectory) ? Directory.GetFiles(modelDirectory) : Array.Empty<string>())
                if (IsGeneratedModelCache(file)) throw new BuildFailedException("Do not bundle machine-generated inference caches. Use the model build scope or move this cache out before building: " + Path.GetFileName(file));
        }
        public static void ValidateAppleAssetArchive(string archivePath)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
                throw new BuildFailedException("Set YUI_APPLE_ASSET_ARCHIVE to the reviewed Apple model archive.");
            var evidencePath = Path.Combine(Path.GetDirectoryName(archivePath), "archive-evidence.json");
            if (!File.Exists(evidencePath)) throw new BuildFailedException("Apple archive integrity evidence is missing.");
            var evidence = JObject.Parse(File.ReadAllText(evidencePath));
            if ((string)evidence["filename"] != Path.GetFileName(archivePath)
                || (long?)evidence["size_bytes"] != new FileInfo(archivePath).Length
                || (string)evidence["asset_pack_id"] != "yui-gemma-e4b-v1"
                || (string)evidence["download_policy"] != "onDemand"
                || (string)evidence["model_sha256"] != "0b2a8980ce155fd97673d8e820b4d29d9c7d99b8fa6806f425d969b145bd52e0")
                throw new BuildFailedException("Apple archive evidence does not match the approved model.");
            using var input = File.OpenRead(archivePath);
            using var sha = SHA256.Create();
            var actual = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
            if (actual != (string)evidence["sha256"]) throw new BuildFailedException("Apple archive checksum mismatch.");
        }
        public static void ValidatePublicIosPayload(string assetsDirectory)
        {
            var aivis = Path.Combine(assetsDirectory, "StreamingAssets/YuiLocalAI/Aivis");
            if (Directory.Exists(aivis) && Directory.EnumerateFiles(aivis, "*", SearchOption.AllDirectories).Any(f => !f.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase)))
                throw new BuildFailedException("The public iOS beta excludes experimental Aivis data. Use a sanitized build tree with only the standard Gemma and VOICEVOX payloads.");
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
