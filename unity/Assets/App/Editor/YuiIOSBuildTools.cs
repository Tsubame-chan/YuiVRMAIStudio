using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEditor.iOS.Xcode;
using UnityEngine;
namespace YuiPhysicalAI.Editor
{
    public static class YuiIOSBuildTools
    {
        public static void BuildIOSBeta()
        {
            var output = Environment.GetEnvironmentVariable("YUI_IOS_BUILD_DIRECTORY");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Set YUI_IOS_BUILD_DIRECTORY to a fresh candidate directory.");
            var rebuilding = Environment.GetEnvironmentVariable("YUI_IOS_REBUILD") == "1"
                && Directory.Exists(Path.Combine(output, "Unity-iPhone.xcodeproj"));
            if (!rebuilding && Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("Use a fresh output directory, or YUI_IOS_REBUILD=1 for an existing generated Xcode project: " + output);
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS).Split(';')
                .Where(d => !d.StartsWith("YUI_PROFILE_")).Concat(new[] { "YUI_PROFILE_PUBLIC" });
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS, string.Join(";", defines));
            PlayerSettings.companyName = "Yui VRM AI Studio";
            PlayerSettings.productName = "Yui VRM AI Studio Beta";
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "jp.tsubamechan.yuivrm.beta");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.buildNumber = "20260921";
            PlayerSettings.iOS.targetOSVersionString = "16.2";
            PlayerSettings.iOS.cameraUsageDescription = "カメラで選んだ景色をキャラクターに見せます。";
            PlayerSettings.iOS.microphoneUsageDescription = "キャラクターと話すためにマイクを使います。";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            const string scene = "Assets/Scenes/YuiChatSceneUGUI.unity";
            EditorSceneManager.OpenScene(scene);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scene }, locationPathName = output,
                target = BuildTarget.iOS, options = BuildOptions.Development | BuildOptions.DetailedBuildReport | BuildOptions.CleanBuildCache });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("iOS export failed: " + report.summary.result);
        }
        [PostProcessBuild(100)]
        public static void PostProcessIOS(BuildTarget target, string pathToBuiltProject)
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
            root.SetBoolean("UIStatusBarHidden", false);
            root.SetString("UIStatusBarStyle", "UIStatusBarStyleLightContent");

            var ats = root.values.TryGetValue("NSAppTransportSecurity", out var existingAts)
                ? existingAts.AsDict()
                : root.CreateDict("NSAppTransportSecurity");
            ats.values.Remove("NSAllowsArbitraryLoads");
            ats.SetBoolean("NSAllowsLocalNetworking", true);

            root.SetString(
                "NSLocalNetworkUsageDescription",
                "Yui VRM AI Studio can connect to a companion backend on the local network when Advanced backend mode is enabled.");
            root.SetString(
                "NSPhotoLibraryUsageDescription",
                "Choose a photo to show to your character.");
            root.SetString(
                "NSSpeechRecognitionUsageDescription",
                "Transcribe your voice on this iPhone when you use Mic.");
            root.SetString("NSMicrophoneUsageDescription", "Use the microphone to talk to your character.");
            root.SetString("NSCameraUsageDescription", "Take a photo to show to your character.");
            var languages = root.CreateArray("CFBundleLocalizations");
            languages.AddString("en"); languages.AddString("ja");

            plist.WriteToFile(plistPath);
            AddIOSLiteRTSwiftPackage(pathToBuiltProject);
            Debug.Log("Yui build: applied iOS local backend networking plist settings.");
        }

        private static void AddPermissionLocalizations(PBXProject project, string target, string directory)
        {
            var keys = new[] { "NSMicrophoneUsageDescription", "NSSpeechRecognitionUsageDescription", "NSCameraUsageDescription", "NSPhotoLibraryUsageDescription", "NSLocalNetworkUsageDescription" };
            var english = new[] { "Use the microphone to talk to your character.", "Transcribe your voice on this iPhone when you use Mic.", "Take a photo to show to your character.", "Choose a photo to show to your character.", "Connect to the Backend you configure on your local network." };
            var japanese = new[] { "キャラクターと話すためにマイクを使います。", "Micで入力した声を、このiPhoneで文字に変換します。", "キャラクターに見せる写真を撮影します。", "キャラクターに見せる写真を選びます。", "設定したローカルネットワーク内のBackendに接続します。" };
            foreach (var language in new[] { "en", "ja" })
            {
                var relative = language + ".lproj";
                Directory.CreateDirectory(Path.Combine(directory, relative));
                var values = language == "ja" ? japanese : english;
                var text = string.Join("\n", keys.Select((key, index) => "\"" + key + "\" = \"" + values[index] + "\";"));
                File.WriteAllText(Path.Combine(directory, relative, "InfoPlist.strings"), text, new System.Text.UTF8Encoding(false));
                var guid = project.FindFileGuidByProjectPath(relative);
                if (string.IsNullOrEmpty(guid)) guid = project.AddFolderReference(relative, relative, PBXSourceTree.Source);
                project.AddFileToBuild(target, guid);
            }
        }

        private static void AddIOSLiteRTSwiftPackage(string pathToBuiltProject)
        {
            var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            if (!File.Exists(projectPath))
            {
                Debug.LogWarning($"Yui build: Xcode project was not found for LiteRT-LM package setup: {projectPath}");
                return;
            }

            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var targetGuid = project.GetUnityMainTargetGuid();
            var frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();
            AddPermissionLocalizations(project, targetGuid, pathToBuiltProject);

            project.AddFrameworkToProject(frameworkTargetGuid, "AVFoundation.framework", false);
            project.AddFrameworkToProject(frameworkTargetGuid, "PhotosUI.framework", false);
            project.AddFrameworkToProject(frameworkTargetGuid, "UniformTypeIdentifiers.framework", false);
            project.AddFrameworkToProject(frameworkTargetGuid, "ImageIO.framework", false);
            project.AddFrameworkToProject(frameworkTargetGuid, "Speech.framework", false);
            project.AddFrameworkToProject(frameworkTargetGuid, "Security.framework", false);
            project.AddFrameworkToProject(frameworkTargetGuid, "Vision.framework", false);
            project.SetBuildProperty(targetGuid, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(targetGuid, "CLANG_ENABLE_MODULES", "YES");
            project.SetBuildProperty(targetGuid, "CLANG_CXX_LANGUAGE_STANDARD", "gnu++17");
            project.SetBuildProperty(targetGuid, "CLANG_CXX_LIBRARY", "libc++");
            project.SetBuildProperty(targetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            project.SetBuildProperty(frameworkTargetGuid, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(frameworkTargetGuid, "CLANG_ENABLE_MODULES", "YES");
            project.SetBuildProperty(frameworkTargetGuid, "CLANG_CXX_LANGUAGE_STANDARD", "gnu++17");
            project.SetBuildProperty(frameworkTargetGuid, "CLANG_CXX_LIBRARY", "libc++");
            var gameAssemblyGuid = project.TargetGuidByName("GameAssembly");
            if (!string.IsNullOrEmpty(gameAssemblyGuid))
            {
                project.SetBuildProperty(gameAssemblyGuid, "CLANG_CXX_LANGUAGE_STANDARD", "gnu++17");
                project.SetBuildProperty(gameAssemblyGuid, "CLANG_CXX_LIBRARY", "libc++");
            }

            var addPackage = typeof(PBXProject).GetMethod(
                "AddRemotePackageReferenceAtVersion",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(string) },
                null);
            var addFramework = typeof(PBXProject).GetMethod(
                "AddRemotePackageFrameworkToProject",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(string), typeof(string), typeof(bool) },
                null);

            if (addPackage == null || addFramework == null)
            {
                Debug.LogWarning("Yui build: this Unity version does not expose Swift Package PBXProject APIs; add https://github.com/google-ai-edge/LiteRT-LM to the Xcode project manually.");
                project.WriteToFile(projectPath);
                return;
            }

            var packageGuid = (string)addPackage.Invoke(
                project,
                new object[] { "https://github.com/google-ai-edge/LiteRT-LM", "0.17.0" });
            addFramework.Invoke(
                project,
                new object[] { frameworkTargetGuid, "LiteRTLM", packageGuid, false });

            project.WriteToFile(projectPath);
            Debug.Log("Yui build: added LiteRT-LM Swift package to iOS Xcode project.");
        }

    }
}
