using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEditor.iOS.Xcode;
using UnityEngine;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Editor
{
    public static class YuiPublicWindowsBuildTools
    {
        private const string ScenePath = "Assets/Scenes/YuiChatSceneUGUI.unity";
        private const string IconPath = "Assets/App/Art/Yui_icon.png";
        private const string PublicBuildDirectory = "../../builds/YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1";
        private const string PersonalBuildDirectory = "../../builds/YuiVRMAIStudio_PersonalAlpha_v0.1.0-alpha.1";
        private const string MacPublicBuildDirectory = "../../builds/YuiVRMAIStudio_MacOSAlpha_v0.1.0-alpha.1";
        private const string MacPersonalBuildDirectory = "../../builds/YuiVRMAIStudio_MacOSPersonalAlpha_v0.1.0-alpha.1";
        private const string IosPersonalBuildDirectory = "../../builds/YuiVRMAIStudio_iOSPersonalAlpha_v0.1.0-alpha.1";
        private const string PublicExeName = "Yui VRM AI Studio.exe";
        private const string PersonalExeName = "Yui VRM AI Studio Personal.exe";
        private const string MacPublicAppName = "Yui VRM AI Studio.app";
        private const string MacPersonalAppName = "Yui VRM AI Studio Personal.app";
        private const string Version = "0.1.0-alpha.1";
        private const string IosBundleVersion = "0.1.0";
        private const string PublicProfileDefine = "YUI_PROFILE_PUBLIC";
        private const string PersonalProfileDefine = "YUI_PROFILE_PERSONAL";

        [MenuItem("Yui/Build/Build Windows Public Alpha", false, 501)]
        public static void BuildWindowsPublicAlpha()
        {
            BuildWindowsAlpha(PublicBuildDirectory, PublicExeName, "Yui VRM AI Studio", PublicProfileDefine);
        }

        [MenuItem("Yui/Build/Build Windows Personal Alpha", false, 502)]
        public static void BuildWindowsPersonalAlpha()
        {
            BuildWindowsAlpha(PersonalBuildDirectory, PersonalExeName, "Yui VRM AI Studio Personal", PersonalProfileDefine);
        }

        [MenuItem("Yui/Build/Build macOS Public Alpha", false, 503)]
        public static void BuildMacOSPublicAlpha()
        {
            BuildStandaloneAlpha(
                MacPublicBuildDirectory,
                MacPublicAppName,
                "Yui VRM AI Studio",
                BuildTarget.StandaloneOSX,
                "macOS public alpha",
                PublicProfileDefine);
        }

        [MenuItem("Yui/Build/Build macOS Personal Alpha", false, 504)]
        public static void BuildMacOSPersonalAlpha()
        {
            BuildStandaloneAlpha(
                MacPersonalBuildDirectory,
                MacPersonalAppName,
                "Yui VRM AI Studio Personal",
                BuildTarget.StandaloneOSX,
                "macOS personal alpha",
                PersonalProfileDefine);
        }

        [MenuItem("Yui/Build/Build iOS Personal Alpha Xcode Project", false, 505)]
        public static void BuildIOSPersonalAlpha()
        {
            ConfigureIOSPlayer("Yui VRM AI Studio Personal");
            ConfigureProfileDefines(BuildTargetGroup.iOS, PersonalProfileDefine);
            BuildPlayerToDirectory(
                IosPersonalBuildDirectory,
                BuildTarget.iOS,
                "iOS personal alpha Xcode project");
        }

        private static void BuildWindowsAlpha(string buildDirectory, string exeName, string productName, string profileDefine)
        {
            BuildStandaloneAlpha(
                buildDirectory,
                exeName,
                productName,
                BuildTarget.StandaloneWindows64,
                "Windows alpha",
                profileDefine);
        }

        private static void BuildStandaloneAlpha(
            string buildDirectory,
            string fileName,
            string productName,
            BuildTarget target,
            string label,
            string profileDefine)
        {
            ConfigureStandalonePlayer(productName);
            ConfigureProfileDefines(BuildPipeline.GetBuildTargetGroup(target), profileDefine);
            EditorSceneManager.OpenScene(ScenePath);
            EditorSceneManager.SaveOpenScenes();
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

            var report = BuildPipeline.BuildPlayer(options);
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

        private static void BuildPlayerToDirectory(
            string buildDirectory,
            BuildTarget target,
            string label)
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, buildDirectory));
            Directory.CreateDirectory(outputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputDirectory,
                target = target,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Yui build: {label} succeeded: {outputDirectory} ({summary.totalSize} bytes)");
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

        private static void ConfigureProfileDefines(BuildTargetGroup group, string profileDefine)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group)
                .Split(';')
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value)
                    && value != PublicProfileDefine
                    && value != PersonalProfileDefine)
                .ToList();
            defines.Add(profileDefine);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines));
            Debug.Log($"Yui build: configured profile define for {group}: {profileDefine}");
        }

        private static void ConfigureStandalonePlayer(string productName)
        {
            PlayerSettings.companyName = "Yui VRM AI Studio";
            PlayerSettings.productName = productName;
            PlayerSettings.bundleVersion = Version;
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

        private static void ConfigureIOSPlayer(string productName)
        {
            PlayerSettings.companyName = "Yui VRM AI Studio";
            PlayerSettings.productName = productName;
            PlayerSettings.bundleVersion = IosBundleVersion;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "jp.tsubamechan.yuivrm.personal");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.cameraUsageDescription = "Yui VRM AI Studio uses the camera only when you ask Yui to look at the current scene.";
            PlayerSettings.iOS.microphoneUsageDescription = "Yui VRM AI Studio uses the microphone to transcribe your voice when you press Rec.";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            var developmentTeam = System.Environment.GetEnvironmentVariable("YUI_IOS_DEVELOPMENT_TEAM");
            if (!string.IsNullOrWhiteSpace(developmentTeam))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = developmentTeam.Trim();
            }
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                PlayerSettings.SetIconsForTargetGroup(
                    BuildTargetGroup.iOS,
                    Enumerable.Repeat(icon, 8).ToArray());
            }
        }

        [PostProcessBuild(100)]
        public static void PostProcessIOSPersonalAlpha(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"Yui build: Info.plist was not found for iOS postprocess: {plistPath}");
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;

            var ats = root.values.TryGetValue("NSAppTransportSecurity", out var existingAts)
                ? existingAts.AsDict()
                : root.CreateDict("NSAppTransportSecurity");
            ats.SetBoolean("NSAllowsArbitraryLoads", true);
            ats.SetBoolean("NSAllowsLocalNetworking", true);

            root.SetString(
                "NSLocalNetworkUsageDescription",
                "Yui VRM AI Studio connects to the companion backend running on this Mac for local testing.");
            root.SetString(
                "NSPhotoLibraryUsageDescription",
                "Yui VRM AI Studio lets you choose photos so Yui can analyze the selected image when you press Img.");

            plist.WriteToFile(plistPath);
            Debug.Log("Yui build: applied iOS local backend networking plist settings.");
        }
    }
}
