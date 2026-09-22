using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace YuiPhysicalAI.EditorTools
{
    // A public profile changes runtime defaults; it does not remove serialized/private assets.
    public sealed class YuiPublicBuildPrivacyGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue;
        private static readonly string[] PublicRoots = {
            "Assets/App/", "Assets/ChatdollKit/", "Assets/UnityChan/", "Assets/TextMesh Pro/",
            "Assets/lilToon/", "Assets/Plugins/", "Assets/Scenes/", "Assets/StreamingAssets/", "Assets/Tests/"
        };
        public static bool IsApprovedAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)) return true;
            return PublicRoots.Any(root => path.StartsWith(root, StringComparison.Ordinal));
        }
        private static bool IsPublicBuild(BuildReport report)
        {
            var group = BuildPipeline.GetBuildTargetGroup(report.summary.platform);
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(';').Contains("YUI_PROFILE_PUBLIC");
        }
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!IsPublicBuild(report)) return;
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            // Inspect Resources too: Unity includes them even when no scene references them.
            var resources = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)
                && path.Contains("/Resources/") && !AssetDatabase.IsValidFolder(path)).ToArray();
            var inputs = scenes.Concat(resources).ToArray();
            var unexpected = inputs.Length == 0 ? Array.Empty<string>() : AssetDatabase.GetDependencies(inputs, true)
                .Where(path => !IsApprovedAssetPath(path)).Distinct().ToArray();
            if (unexpected.Length > 0)
                throw new BuildFailedException("Public build contains assets outside approved source roots. Generate a fresh sanitized public build tree from canonical sources; never publish this private-workspace build. First blocked asset: " + unexpected[0]);
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if (!IsPublicBuild(report)) return;
            var paths = report.packedAssets.SelectMany(pack => pack.contents).Select(item => item.sourceAssetPath)
                .Where(path => !string.IsNullOrEmpty(path)).Distinct().OrderBy(path => path).ToArray();
            var unexpected = paths.Where(path => !IsApprovedAssetPath(path)).ToArray();
            var evidence = new Evidence { unityVersion = Application.unityVersion, result = paths.Length == 0 ? "NO_EVIDENCE" : unexpected.Length == 0 ? "PASS" : "FAIL",
                assetCount = paths.Length, unexpectedAssets = unexpected, packedAssets = paths };
            var output = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "yui-public-asset-audit.json");
            File.WriteAllText(output, JsonUtility.ToJson(evidence, true));
            if (paths.Length == 0)
                throw new BuildFailedException("Packed asset evidence is empty. Rebuild with DetailedBuildReport and CleanBuildCache before distribution. See " + output);
            if (unexpected.Length > 0)
                throw new BuildFailedException("Public Player contains unapproved assets. Do not distribute. See " + output);
        }
        [Serializable] private sealed class Evidence
        {
            public string unityVersion;
            public string result;
            public int assetCount;
            public string[] unexpectedAssets;
            public string[] packedAssets;
        }
    }
}
