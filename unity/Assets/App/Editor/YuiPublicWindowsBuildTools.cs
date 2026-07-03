using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Editor
{
    public static class YuiPublicWindowsBuildTools
    {
        private const string ScenePath = "Assets/Scenes/YuiChatSceneUGUI.unity";
        private const string IconPath = "Assets/App/Art/Yui_icon.png";
        private const string WindowsExeName = "Yui VRM AI Studio.exe";
        private const string MacAppName = "Yui VRM AI Studio.app";
        private const string DefaultReleaseVersion = "v0.2.0-beta.1";
        private const string PublicProfileDefine = "YUI_PROFILE_PUBLIC";

        [MenuItem("Yui/Build/Build Windows Public Beta", false, 501)]
        public static void BuildWindowsPublicBeta()
        {
            BuildStandalone(
                WindowsPublicBuildDirectory(),
                WindowsExeName,
                BuildTarget.StandaloneWindows64,
                "Windows public beta");
        }

        [MenuItem("Yui/Build/Build macOS Public Beta", false, 502)]
        public static void BuildMacOSPublicBeta()
        {
            BuildStandalone(
                MacPublicBuildDirectory(),
                MacAppName,
                BuildTarget.StandaloneOSX,
                "macOS public beta");
        }

        private static void BuildStandalone(
            string buildDirectory,
            string fileName,
            BuildTarget target,
            string label)
        {
            ConfigureStandalonePlayer();
            ConfigurePublicProfile(BuildPipeline.GetBuildTargetGroup(target));
            EditorSceneManager.OpenScene(ScenePath);
            EditorSceneManager.SaveOpenScenes();
            RemoveLocalAiGeneratedCaches();
            AssetDatabase.SaveAssets();

            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, buildDirectory));
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, fileName);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report;
            using (new LocalAiModelBuildScope(target))
            {
                report = BuildPipeline.BuildPlayer(options);
            }

            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                RemoveBurstDebugInformation(outputDirectory);
                Debug.Log($"Yui build: {label} succeeded: {outputPath} ({summary.totalSize} bytes)");
            }
            else
            {
                Debug.LogError($"Yui build: {label} failed: {summary.result}");
                EditorApplication.Exit(1);
            }
        }

        private static void RemoveBurstDebugInformation(string outputDirectory)
        {
            foreach (var directory in Directory.GetDirectories(outputDirectory, "*_BurstDebugInformation_DoNotShip", SearchOption.TopDirectoryOnly))
            {
                Directory.Delete(directory, true);
                Debug.Log($"Yui build: removed non-shipping Burst debug folder: {directory}");
            }
        }

        private static void RemoveLocalAiGeneratedCaches()
        {
            var modelsDirectory = Path.Combine(Application.dataPath, "StreamingAssets/YuiLocalAI/Models");
            if (!Directory.Exists(modelsDirectory))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(modelsDirectory, "*_mldrift_*_cache.bin*", SearchOption.TopDirectoryOnly))
            {
                File.Delete(file);
                Debug.Log($"Yui build: removed generated local AI runtime cache: {file}");
            }
        }

        private static void ConfigurePublicProfile(BuildTargetGroup group)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group)
                .Split(';')
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value)
                    && !value.StartsWith("YUI_PROFILE_", StringComparison.Ordinal)
                    && value != PublicProfileDefine)
                .ToList();
            defines.Add(PublicProfileDefine);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines));
            Debug.Log($"Yui build: configured public profile define for {group}: {PublicProfileDefine}");
        }

        private static void ConfigureStandalonePlayer()
        {
            PlayerSettings.companyName = "Yui VRM AI Studio";
            PlayerSettings.productName = "Yui VRM AI Studio";
            PlayerSettings.bundleVersion = PublicBuildVersion();
            PlayerSettings.defaultScreenWidth = YuiStandaloneWindowBootstrap.DefaultWindowWidth;
            PlayerSettings.defaultScreenHeight = YuiStandaloneWindowBootstrap.DefaultWindowHeight;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                PlayerSettings.SetIconsForTargetGroup(
                    BuildTargetGroup.Standalone,
                    Enumerable.Repeat(icon, 8).ToArray());
            }
            else
            {
                Debug.LogWarning($"Yui build: application icon asset was not found: {IconPath}");
            }
        }

        private static string PublicBuildVersionTag()
        {
            var value = Environment.GetEnvironmentVariable("YUI_RELEASE_VERSION");
            return string.IsNullOrWhiteSpace(value) ? DefaultReleaseVersion : value.Trim();
        }

        private static string PublicBuildVersion()
        {
            var tag = PublicBuildVersionTag();
            return tag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tag.Substring(1) : tag;
        }

        private static string WindowsPublicBuildDirectory()
        {
            return $"../../builds/YuiVRMAIStudio_WindowsPublicBeta_{PublicBuildVersionTag()}";
        }

        private static string MacPublicBuildDirectory()
        {
            return $"../../builds/YuiVRMAIStudio_MacOSPublicBeta_{PublicBuildVersionTag()}";
        }

        private sealed class LocalAiModelBuildScope : IDisposable
        {
            private readonly string excludedDirectory;
            private readonly List<(string Original, string Temporary)> movedFiles = new List<(string Original, string Temporary)>();
            private readonly List<(string Original, string Temporary)> movedDirectories = new List<(string Original, string Temporary)>();

            public LocalAiModelBuildScope(BuildTarget target)
            {
                var platform = PlatformKey(target);
                if (string.IsNullOrWhiteSpace(platform))
                {
                    return;
                }

                excludedDirectory = Path.Combine(Path.GetTempPath(), "yui-local-ai-build-excluded", platform, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(excludedDirectory);
                if (IsDesktopPlatform(platform))
                {
                    MoveDirectoryOut(Path.Combine(Application.dataPath, "StreamingAssets/YuiLocalAI/Aivis"));
                }

                var modelsDirectory = Path.Combine(Application.dataPath, "StreamingAssets/YuiLocalAI/Models");
                var manifestPath = Path.Combine(Application.dataPath, "StreamingAssets/YuiLocalAI/local_ai_model_packs.json");
                if (!Directory.Exists(modelsDirectory) || !File.Exists(manifestPath))
                {
                    return;
                }

                var allowed = AllowedModelFiles(manifestPath, platform);
                if (allowed.Count == 0)
                {
                    return;
                }

                foreach (var modelPath in Directory.GetFiles(modelsDirectory, "*.litertlm", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileName(modelPath);
                    if (allowed.Contains(fileName))
                    {
                        continue;
                    }

                    MoveOut(modelPath);
                    var metaPath = modelPath + ".meta";
                    if (File.Exists(metaPath))
                    {
                        MoveOut(metaPath);
                    }

                    Debug.Log($"Yui build: excluded local AI model from {platform} build: {fileName}");
                }

                if (movedFiles.Count > 0 || movedDirectories.Count > 0)
                {
                    AssetDatabase.Refresh();
                }
            }

            public void Dispose()
            {
                for (var index = movedFiles.Count - 1; index >= 0; index--)
                {
                    var moved = movedFiles[index];
                    Directory.CreateDirectory(Path.GetDirectoryName(moved.Original));
                    if (File.Exists(moved.Original))
                    {
                        File.Delete(moved.Original);
                    }

                    if (File.Exists(moved.Temporary))
                    {
                        File.Move(moved.Temporary, moved.Original);
                    }
                }

                for (var index = movedDirectories.Count - 1; index >= 0; index--)
                {
                    var moved = movedDirectories[index];
                    if (Directory.Exists(moved.Original))
                    {
                        Directory.Delete(moved.Original, true);
                    }

                    if (Directory.Exists(moved.Temporary))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(moved.Original));
                        Directory.Move(moved.Temporary, moved.Original);
                    }
                }

                if (!string.IsNullOrWhiteSpace(excludedDirectory) && Directory.Exists(excludedDirectory))
                {
                    try
                    {
                        Directory.Delete(excludedDirectory, true);
                    }
                    catch (IOException)
                    {
                        // Best-effort cleanup only; the original files have already been restored.
                    }
                }

                if (movedFiles.Count > 0 || movedDirectories.Count > 0)
                {
                    AssetDatabase.Refresh();
                }
            }

            private void MoveOut(string originalPath)
            {
                var temporaryPath = Path.Combine(excludedDirectory, Path.GetFileName(originalPath));
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                File.Move(originalPath, temporaryPath);
                movedFiles.Add((originalPath, temporaryPath));
            }

            private void MoveDirectoryOut(string originalPath)
            {
                if (!Directory.Exists(originalPath))
                {
                    return;
                }

                var temporaryPath = Path.Combine(excludedDirectory, Path.GetFileName(originalPath));
                if (Directory.Exists(temporaryPath))
                {
                    Directory.Delete(temporaryPath, true);
                }

                Directory.Move(originalPath, temporaryPath);
                movedDirectories.Add((originalPath, temporaryPath));
                Debug.Log($"Yui build: excluded optional local AI/TTS asset directory from desktop build: {Path.GetFileName(originalPath)}");
            }

            private static HashSet<string> AllowedModelFiles(string manifestPath, string platform)
            {
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var manifest = JObject.Parse(File.ReadAllText(manifestPath));
                foreach (var pack in manifest["packs"]?.Children<JObject>() ?? Enumerable.Empty<JObject>())
                {
                    if (pack.Value<bool?>("enabled_by_default") != true)
                    {
                        continue;
                    }

                    if (!SupportsPlatform(pack["platforms"] as JArray, platform))
                    {
                        continue;
                    }

                    var runtimeModelRef = pack.Value<string>("runtime_model_ref");
                    if (string.IsNullOrWhiteSpace(runtimeModelRef))
                    {
                        continue;
                    }

                    var fileName = Path.GetFileName(runtimeModelRef.Replace('\\', '/'));
                    if (fileName.EndsWith(".litertlm", StringComparison.OrdinalIgnoreCase))
                    {
                        allowed.Add(fileName);
                    }
                }

                return allowed;
            }

            private static bool SupportsPlatform(JArray platforms, string platform)
            {
                if (platforms == null || platforms.Count == 0)
                {
                    return true;
                }

                return platforms
                    .Select(value => NormalizePlatform(value.Value<string>()))
                    .Any(value => string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(value, platform, StringComparison.OrdinalIgnoreCase));
            }

            private static string PlatformKey(BuildTarget target)
            {
                switch (target)
                {
                    case BuildTarget.StandaloneOSX:
                        return "macos";
                    case BuildTarget.StandaloneWindows:
                    case BuildTarget.StandaloneWindows64:
                        return "windows";
                    default:
                        return null;
                }
            }

            private static string NormalizePlatform(string platform)
            {
                if (string.IsNullOrWhiteSpace(platform))
                {
                    return "all";
                }

                var value = platform.Trim().Replace("-", "_").ToLowerInvariant();
                switch (value)
                {
                    case "osx":
                    case "mac":
                    case "macosx":
                    case "standaloneosx":
                    case "standalone_osx":
                        return "macos";
                    case "win":
                    case "standalonewindows":
                    case "standalone_windows":
                    case "standalonewindows64":
                    case "standalone_windows_64":
                        return "windows";
                    default:
                        return value;
                }
            }

            private static bool IsDesktopPlatform(string platform)
            {
                return string.Equals(platform, "macos", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(platform, "windows", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
