using System.IO;
using System.Linq;
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
        private const string PublicBuildDirectory = "../../builds/YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1";
        private const string PersonalBuildDirectory = "../../builds/YuiVRMAIStudio_PersonalAlpha_v0.1.0-alpha.1";
        private const string PublicExeName = "Yui VRM AI Studio.exe";
        private const string PersonalExeName = "Yui VRM AI Studio Personal.exe";
        private const string Version = "0.1.0-alpha.1";

        [MenuItem("Yui/Build/Build Windows Public Alpha", false, 501)]
        public static void BuildWindowsPublicAlpha()
        {
            BuildWindowsAlpha(PublicBuildDirectory, PublicExeName, "Yui VRM AI Studio");
        }

        [MenuItem("Yui/Build/Build Windows Personal Alpha", false, 502)]
        public static void BuildWindowsPersonalAlpha()
        {
            BuildWindowsAlpha(PersonalBuildDirectory, PersonalExeName, "Yui VRM AI Studio Personal");
        }

        private static void BuildWindowsAlpha(string buildDirectory, string exeName, string productName)
        {
            ConfigureStandalonePlayer(productName);
            EditorSceneManager.OpenScene(ScenePath);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, buildDirectory));
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, exeName);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                RemoveBurstDebugInformation(outputDirectory);
                Debug.Log($"Yui build: Windows alpha succeeded: {outputPath} ({summary.totalSize} bytes)");
            }
            else
            {
                Debug.LogError($"Yui build: Windows alpha failed: {summary.result}");
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
    }
}


