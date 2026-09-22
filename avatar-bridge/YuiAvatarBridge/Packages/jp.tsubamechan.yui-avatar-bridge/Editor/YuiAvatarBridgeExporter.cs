using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;

using UnityEngine;
using Object = UnityEngine.Object;

namespace Yui.AvatarBridge.Editor
{
    public sealed class YuiAvatarExportOptions
    {
        public string OutputPath;
        public string DisplayName;
        public bool BuildWindows = true;
        public bool BuildMacOS = true;
        public bool BuildAndroid = true;
        public bool BuildIOS = true;
        public bool RightsAcknowledged;
    }

    public static class YuiAvatarBridgeExporter
    {
        private const string TempAssetRoot = "Assets/__YuiAvatarBridgeTemp";
        private const string BundleName = "avatar";

        public static YuiAvatarBridgeManifest Export(YuiAvatarAnalysis analysis, YuiAvatarExportOptions options)
        {
            if (analysis == null || analysis.Root == null) throw new ArgumentException("Avatar analysis is missing.");
            if (analysis.HasErrors) throw new InvalidOperationException("Resolve the blocking compatibility errors before export.");
            if (options == null || string.IsNullOrWhiteSpace(options.OutputPath)) throw new ArgumentException("Output path is required.");
            if (!options.RightsAcknowledged) throw new InvalidOperationException("Rights acknowledgement is required.");
            if (!options.BuildWindows && !options.BuildMacOS && !options.BuildAndroid && !options.BuildIOS)
                throw new InvalidOperationException("Select at least one target OS.");

            if (!UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(package => package.name == "com.unity.modules.assetbundle"))
                throw new InvalidOperationException("Unity AssetBundle module is disabled. Resolve the Yui Avatar Bridge package dependencies before exporting.");

            // All targets and source preprocessing are checked before cloning/building any payload.
            if (YuiAvatarBridgeAnalyzer.RequiresSourceBake(analysis.Root))
                throw new InvalidOperationException("Modular Avatar/NDMFの加工済みコピーを選択してください。未加工のまま書き出すと改変が失われます。");
            var unavailable = new List<string>();
            if (options.BuildWindows && !IsBuildTargetAvailable(BuildTarget.StandaloneWindows64)) unavailable.Add("Windows");
            if (options.BuildMacOS && !IsBuildTargetAvailable(BuildTarget.StandaloneOSX)) unavailable.Add("macOS");
            if (options.BuildAndroid && !IsBuildTargetAvailable(BuildTarget.Android)) unavailable.Add("Android");
            if (options.BuildIOS && !IsBuildTargetAvailable(BuildTarget.iOS)) unavailable.Add("iOS");
            if (unavailable.Count > 0) throw new InvalidOperationException("Unity Hubで使用中のEditorへ次のBuild Supportを追加してください: " + string.Join(", ", unavailable));

            var exportId = Guid.NewGuid().ToString("N");
            var tempAssetFolder = $"{TempAssetRoot}/{exportId}";
            var tempDiskRoot = Path.Combine(Path.GetTempPath(), "YuiAvatarBridge", exportId);
            var stagingRoot = Path.Combine(tempDiskRoot, "container");
            var payloadRoot = Path.Combine(stagingRoot, "payloads");
            var buildRoot = Path.Combine(tempDiskRoot, "build");
            GameObject clone = null;
            try
            {
                EnsureAssetFolder(tempAssetFolder);
                Directory.CreateDirectory(payloadRoot);

                clone = CreateSanitizedClone(analysis.Root);
                YuiAvatarBasicMaterials.ApplyToClone(clone, tempAssetFolder);
                var prefabPath = $"{tempAssetFolder}/avatar.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);
                if (prefab == null) throw new InvalidOperationException("Failed to create the sanitized temporary avatar prefab.");

                var payloads = new List<YuiAvatarBundlePayload>();
                if (options.BuildWindows)
                {
                    payloads.Add(BuildPayload(analysis, prefabPath, payloadRoot, buildRoot, BuildTarget.StandaloneWindows64, "windows", "avatar_windows.bundle"));
                }
                if (options.BuildMacOS)
                {
                    payloads.Add(BuildPayload(analysis, prefabPath, payloadRoot, buildRoot, BuildTarget.StandaloneOSX, "macos", "avatar_macos.bundle"));
                }
                if (options.BuildAndroid)
                {
                    payloads.Add(BuildPayload(analysis, prefabPath, payloadRoot, buildRoot, BuildTarget.Android, "android", "avatar_android.bundle"));
                }
                if (options.BuildIOS)
                {
                    payloads.Add(BuildPayload(analysis, prefabPath, payloadRoot, buildRoot, BuildTarget.iOS, "ios", "avatar_ios.bundle"));
                }

                var manifest = new YuiAvatarBridgeManifest
                {
                    avatarId = YuiAvatarBridgeAnalyzer.StableAvatarId(analysis.Root),
                    displayName = string.IsNullOrWhiteSpace(options.DisplayName) ? analysis.Root.name : options.DisplayName.Trim(),
                    unityVersion = Application.unityVersion,
                    createdUtc = DateTime.UtcNow.ToString("O"),
                    rightsAcknowledged = true,
                    payloads = payloads.ToArray(),
                    diagnostics = analysis.Diagnostics,
                };

                File.WriteAllText(Path.Combine(stagingRoot, "manifest.json"), JsonUtility.ToJson(manifest, true));
                File.WriteAllText(Path.Combine(stagingRoot, "FORMAT.txt"), FormatHelpText());
                WriteContainer(options.OutputPath, stagingRoot);
                return manifest;
            }
            finally
            {
                if (clone != null) Object.DestroyImmediate(clone);
                if (AssetDatabase.IsValidFolder(tempAssetFolder)) AssetDatabase.DeleteAsset(tempAssetFolder);
                DeleteEmptyTempAssetRoot();
                if (Directory.Exists(tempDiskRoot)) Directory.Delete(tempDiskRoot, true);
                AssetDatabase.Refresh();
            }
        }

        public static GameObject CreateSanitizedClone(GameObject source)
        {
            if (source == null) return null;
            var clone = Object.Instantiate(source);
            clone.name = source.name;
            foreach (var animator in clone.GetComponentsInChildren<Animator>(true))
            {
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            var components = clone.GetComponentsInChildren<Component>(true)
                .Where(component => component != null)
                .OrderByDescending(component => HierarchyDepth(component.transform))
                .ToArray();
            foreach (var component in components)
            {
                if (IsAllowedRuntimeComponent(component)) continue;
                Object.DestroyImmediate(component);
            }
            return clone;
        }

        public static bool IsAllowedRuntimeComponent(Component component)
        {
            return component is Transform
                || component is Animator
                || component is SkinnedMeshRenderer
                || component is MeshRenderer
                || component is MeshFilter
                || component is LODGroup;
        }

        private static YuiAvatarBundlePayload BuildPayload(
            YuiAvatarAnalysis analysis,
            string prefabPath,
            string payloadRoot,
            string buildRoot,
            BuildTarget target,
            string platform,
            string filename)
        {
            if (!IsBuildTargetAvailable(target))
            {
                throw new InvalidOperationException($"Unity build support for {platform} is not installed on this computer.");
            }
            var platformBuildRoot = Path.Combine(buildRoot, platform);
            Directory.CreateDirectory(platformBuildRoot);
            var assetNames = new List<string> { prefabPath };
            var addresses = new List<string> { "avatar/prefab" };
            for (var index = 0; index < analysis.AnimationClips.Count; index++)
            {
                var clipPath = AssetDatabase.GetAssetPath(analysis.AnimationClips[index]);
                if (string.IsNullOrWhiteSpace(clipPath) || assetNames.Contains(clipPath)) continue;
                assetNames.Add(clipPath);
                addresses.Add(analysis.Diagnostics.expressionClips[index].address);
            }

            var builds = new[]
            {
                new AssetBundleBuild
                {
                    assetBundleName = BundleName,
                    assetNames = assetNames.ToArray(),
                    addressableNames = addresses.ToArray(),
                },
            };
            var result = BuildPipeline.BuildAssetBundles(
                platformBuildRoot,
                builds,
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
                target);
            if (result == null) throw new InvalidOperationException($"AssetBundle build failed for {platform}. Install the matching Unity build support module and check the Console.");

            var builtPath = Path.Combine(platformBuildRoot, BundleName);
            if (!File.Exists(builtPath)) throw new FileNotFoundException($"Built AssetBundle was not found for {platform}.", builtPath);
            // A non-null build result and matching ZIP hash do not prove runtime compatibility.
            // Check the producer host payload while it is still staged; built-player validation remains required.
            if ((Application.platform == RuntimePlatform.OSXEditor && target == BuildTarget.StandaloneOSX)
                || (Application.platform == RuntimePlatform.WindowsEditor && target == BuildTarget.StandaloneWindows64))
            {
                var verificationBundle = AssetBundle.LoadFromFile(builtPath);
                if (verificationBundle == null)
                    throw new InvalidOperationException($"Generated {platform} payload failed the Editor load check. Check required Unity modules and rebuild; no ZIP was published.");
                try
                {
                    if (verificationBundle.LoadAsset<GameObject>("avatar/prefab") == null)
                        throw new InvalidOperationException("Generated payload is missing avatar/prefab; no ZIP was published.");
                }
                finally { verificationBundle.Unload(true); }
            }
            var destination = Path.Combine(payloadRoot, filename);
            File.Copy(builtPath, destination, true);
            return new YuiAvatarBundlePayload
            {
                platform = platform,
                filename = $"payloads/{filename}",
                sizeBytes = new FileInfo(destination).Length,
                sha256 = Sha256(destination),
            };
        }

        private static void WriteContainer(string outputPath, string stagingRoot)
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath));
            var temporaryOutput = fullOutputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temporaryOutput, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
                {
                    foreach (var source in Directory.GetFiles(stagingRoot, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
                    {
                        var relative = source.Substring(stagingRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
                        var entry = archive.CreateEntry(relative, System.IO.Compression.CompressionLevel.Optimal);
                        using var input = File.OpenRead(source);
                        using var output = entry.Open();
                        input.CopyTo(output);
                    }
                }
                // Never delete a previously working package before the replacement is ready.
                if (File.Exists(fullOutputPath)) File.Replace(temporaryOutput, fullOutputPath, null);
                else File.Move(temporaryOutput, fullOutputPath);
            }
            finally
            {
                if (File.Exists(temporaryOutput)) File.Delete(temporaryOutput);
            }
        }

        private static string Sha256(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static string FormatHelpText()
        {
            return "Yui Unity Avatar Package ZIP, schema 1\n"
                + "\n"
                + "This is a standard ZIP containing manifest.json and platform-specific Unity AssetBundles.\n"
                + "Advanced users may extract, edit manifest.json, and create a new ZIP.\n"
                + "Display names and expression/emotion mappings may be edited without changing payload hashes.\n"
                + "If a payload .bundle is replaced, update its sizeBytes and SHA-256 in manifest.json.\n"
                + "Do not add scripts, DLLs, executables, absolute paths, or ../ path traversal entries.\n"
                + "AssetBundles are platform-specific and should be rebuilt with a compatible Unity editor.\n";
        }

        public static bool IsBuildTargetAvailable(BuildTarget target)
        {
            return BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target);
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(TempAssetRoot)) AssetDatabase.CreateFolder("Assets", "__YuiAvatarBridgeTemp");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(TempAssetRoot, Path.GetFileName(folder));
        }

        private static void DeleteEmptyTempAssetRoot()
        {
            if (!AssetDatabase.IsValidFolder(TempAssetRoot)) return;
            var diskPath = Path.GetFullPath(TempAssetRoot);
            if (!Directory.EnumerateFileSystemEntries(diskPath).Any(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)))
            {
                AssetDatabase.DeleteAsset(TempAssetRoot);
            }
        }

        private static int HierarchyDepth(Transform transform)
        {
            var depth = 0;
            for (var current = transform; current != null; current = current.parent) depth++;
            return depth;
        }
    }
}
