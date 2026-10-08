using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;
namespace YuiPhysicalAI.Editor
{
    public static class YuiIOSBuildTools
    {
        public static void BuildIOSBeta()
        {
            BuildIOSCandidate();
        }
        private static void BuildIOSCandidate()
        {
            RequireSceneLifecycleEditor(Application.unityVersion);
            var output = Environment.GetEnvironmentVariable("YUI_IOS_BUILD_DIRECTORY");
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output)) throw new InvalidOperationException("Set YUI_IOS_BUILD_DIRECTORY to a fresh absolute candidate directory.");
            var rebuilding = Environment.GetEnvironmentVariable("YUI_IOS_REBUILD") == "1"
                && Directory.Exists(Path.Combine(output, "Unity-iPhone.xcodeproj"));
            if (!rebuilding && Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("Use a fresh output directory, or YUI_IOS_REBUILD=1 for an existing generated Xcode project: " + output);
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS).Split(';')
                .Where(d => !d.StartsWith("YUI_PROFILE_")).Concat(new[] { "YUI_PROFILE_PUBLIC" });
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS, string.Join(";", defines));
            PlayerSettings.companyName = "Yui VRM AI Studio";
            PlayerSettings.productName = "Yui VRM AI Studio";
            if (Environment.GetEnvironmentVariable("YUI_VALIDATION_PROFILE") == "1")
                PlayerSettings.productName = "Yui VRM AI Studio Validation";
#if UNITY_6000_0_OR_NEWER
            // Unity 6 Personal permits disabling the engine splash screen.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
#endif
            PlayerSettings.bundleVersion = Environment.GetEnvironmentVariable("YUI_IOS_VERSION") ?? "0.2.4";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS,
                Environment.GetEnvironmentVariable("YUI_IOS_BUNDLE_ID") ?? "jp.tsubamechan.yuivrm.beta");
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/App/Art/Yui_icon.png");
            if (icon == null) throw new InvalidOperationException("Build icon is missing.");
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.iOS, Enumerable.Repeat(icon, 8).ToArray());
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.sdkVersion = Environment.GetEnvironmentVariable("YUI_IOS_SIMULATOR") == "1"
                ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
            #if UNITY_6000_0_OR_NEWER
            if (PlayerSettings.iOS.sdkVersion == iOSSdkVersion.SimulatorSDK)
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
#endif
            PlayerSettings.iOS.buildNumber = Environment.GetEnvironmentVariable("YUI_IOS_BUILD_NUMBER") ?? "20260924";
            PlayerSettings.iOS.targetOSVersionString = "26.0";
            PlayerSettings.iOS.cameraUsageDescription = "カメラで選んだ景色をキャラクターに見せます。";
            PlayerSettings.iOS.microphoneUsageDescription = "キャラクターと話すためにマイクを使います。";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            // Private VPN backends use HTTP in release players too; ATS limits IP ranges below.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            const string scene = "Assets/Scenes/YuiChatSceneUGUI.unity";
            EditorSceneManager.OpenScene(scene);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scene }, locationPathName = output,
                target = BuildTarget.iOS, options = BuildOptions.DetailedBuildReport | BuildOptions.CleanBuildCache });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("iOS export failed: " + report.summary.result);
        }
        public static void RequireSceneLifecycleEditor(string version)
        {
            var match = Regex.Match(version ?? "", @"^(\d+)\.(\d+)\.(\d+)f\d+");
            if (match.Success)
            {
                var major = int.Parse(match.Groups[1].Value);
                var minor = int.Parse(match.Groups[2].Value);
                var patch = int.Parse(match.Groups[3].Value);
                if ((major == 2022 && minor == 3 && patch >= 72)
                    || (major == 6000 && ((minor == 0 && patch >= 68)
                        || (minor == 3 && patch >= 8) || minor >= 4)) || major > 6000)
                    return;
            }
            throw new UnityEditor.Build.BuildFailedException("iOS 27 requires UIScene lifecycle support. Use Unity 2022.3.72f1+, 6000.0.68f1+, 6000.3.8f1+, or a newer supported stable Editor. Current: " + version);
        }
        [PostProcessBuild(100)]
        public static void PostProcessIOS(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            RequireSceneLifecycleEditor(Application.unityVersion);

            var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            if (!File.Exists(plistPath))
            {
                throw new UnityEditor.Build.BuildFailedException($"Yui build: Info.plist was not found for iOS postprocess: {plistPath}");
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            if (!root.values.ContainsKey("UIApplicationSceneManifest")
                || !File.Exists(Path.Combine(pathToBuiltProject, "Classes/UI/UnityScene.mm")))
                throw new UnityEditor.Build.BuildFailedException("The exported iOS project lacks UIScene lifecycle support. Do not archive or distribute this candidate.");
            // Keep the Home Screen label readable for every distribution profile.
            root.SetString("CFBundleDisplayName", "Yui VRM AI");
            root.SetBoolean("UIStatusBarHidden", false);
            root.SetString("UIStatusBarStyle", "UIStatusBarStyleLightContent");

            ConfigureBackendNetworking(root);

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
            AddModelVirtualAddressSpace(pathToBuiltProject);
            AddAppleHostedAssets(pathToBuiltProject);
            Debug.Log("Yui build: applied iOS local backend networking plist settings.");
        }

        public static void ConfigureBackendNetworking(PlistElementDict root)
        {
            var ats = root.values.TryGetValue("NSAppTransportSecurity", out var existingAts)
                ? existingAts.AsDict()
                : root.CreateDict("NSAppTransportSecurity");
            ats.values.Remove("NSAllowsArbitraryLoads");
            ats.SetBoolean("NSAllowsLocalNetworking", true);
            // iOS 17+ applies ATS to IP addresses, including Tailscale's CGNAT range.
            // Permit HTTP only for private companion backends; public hosts retain ATS.
            var exceptions = ats.values.TryGetValue("NSExceptionDomains", out var existingExceptions)
                ? existingExceptions.AsDict() : ats.CreateDict("NSExceptionDomains");
            foreach (var range in new[] { "100.64.0.0/10", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" })
            {
                var entry = exceptions.values.TryGetValue(range, out var existingEntry)
                    ? existingEntry.AsDict() : exceptions.CreateDict(range);
                entry.SetBoolean("NSExceptionAllowsInsecureHTTPLoads", true);
                entry.SetBoolean("NSIncludesSubdomains", false);
            }
        }

        private static void AddBrandedLaunchScreen(string directory)
        {
            var projectPath = PBXProject.GetPBXProjectPath(directory);
            var project = new PBXProject(); project.ReadFromFile(projectPath);
            var main = project.GetUnityMainTargetGuid();
            foreach (var name in new[] { "YuiLaunchScreen.storyboard", "YuiLaunchCards.png" })
            {
                var source = name.EndsWith(".png") ? "Assets/App/Resources/YuiBrand/Cards.png" : "Assets/App/Branding/" + name;
                if (!File.Exists(source)) throw new InvalidOperationException("Missing public startup artwork: " + source);
                File.Copy(source, Path.Combine(directory, name), true);
                project.AddFileToBuild(main, project.AddFile(name, name, PBXSourceTree.Source));
            }
            project.WriteToFile(projectPath);
            var plistPath = Path.Combine(directory, "Info.plist");
            var plist = new PlistDocument(); plist.ReadFromFile(plistPath);
            plist.root.SetString("UILaunchStoryboardName", "YuiLaunchScreen");
            plist.root.SetString("UILaunchStoryboardName~ipad", "YuiLaunchScreen");
            plist.WriteToFile(plistPath);
        }
        private static void AddModelVirtualAddressSpace(string directory)
        {
            var projectPath = PBXProject.GetPBXProjectPath(directory);
            var project = new PBXProject(); project.ReadFromFile(projectPath);
            var target = project.GetUnityMainTargetGuid();
            var relative = project.GetBuildPropertyForAnyConfig(target, "CODE_SIGN_ENTITLEMENTS");
            if (string.IsNullOrWhiteSpace(relative)) relative = "YuiModel.entitlements";
            var entitlements = new PlistDocument();
            var path = Path.Combine(directory, relative);
            if (File.Exists(path)) entitlements.ReadFromFile(path);
            // Large read-only model and compiled-weight mappings need address
            // space, independently of their resident physical memory footprint.
            entitlements.root.SetBoolean("com.apple.developer.kernel.extended-virtual-addressing", true);
            entitlements.WriteToFile(path);
            project.SetBuildProperty(target, "CODE_SIGN_ENTITLEMENTS", relative);
            project.WriteToFile(projectPath);
        }

        private static void AddAppleHostedAssets(string directory)
        {
            // Opt in only for a sanitized candidate with separately packaged assets.
            if (Environment.GetEnvironmentVariable("YUI_APPLE_HOSTED_ASSETS") != "1") return;
            var projectPath = PBXProject.GetPBXProjectPath(directory);
            var project = new PBXProject(); project.ReadFromFile(projectPath);
            var main = project.GetUnityMainTargetGuid();
            var framework = project.GetUnityFrameworkTargetGuid();
            var bundle = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
            var group = "group." + bundle + ".assets";
            var plist = new PlistDocument(); plist.ReadFromFile(Path.Combine(directory, "Info.plist"));
            plist.root.SetString("BAAppGroupID", group);
            plist.root.SetBoolean("BAHasManagedAssetPacks", true);
            plist.root.SetBoolean("BAUsesAppleHosting", true);
            plist.WriteToFile(Path.Combine(directory, "Info.plist"));
            const string folder = "YuiAssetDownloader";
            Directory.CreateDirectory(Path.Combine(directory, folder));
            var extensionPlist = new PlistDocument();
            extensionPlist.root.SetString("CFBundleIdentifier", "$(PRODUCT_BUNDLE_IDENTIFIER)");
            extensionPlist.root.SetString("CFBundleExecutable", "$(EXECUTABLE_NAME)");
            extensionPlist.root.SetString("CFBundleName", "$(PRODUCT_NAME)");
            extensionPlist.root.SetString("CFBundleDisplayName", "Yui Model Download");
            extensionPlist.root.SetString("CFBundlePackageType", "XPC!");
            extensionPlist.root.SetString("CFBundleShortVersionString", PlayerSettings.bundleVersion);
            extensionPlist.root.SetString("CFBundleVersion", PlayerSettings.iOS.buildNumber);
            extensionPlist.root.CreateDict("EXAppExtensionAttributes").SetString("EXExtensionPointIdentifier", "com.apple.background-asset-downloader-extension");
            extensionPlist.WriteToFile(Path.Combine(directory, folder, "Info.plist"));
            var extension = project.TargetGuidByName(folder);
            if (string.IsNullOrEmpty(extension))
                extension = project.AddAppExtension(main, folder, bundle + ".assetdownloader", folder + "/Info.plist");
            File.WriteAllText(Path.Combine(directory, folder, "Downloader.swift"),
                "import BackgroundAssets\nimport ExtensionFoundation\nimport StoreKit\n@main struct YuiAssetDownloaderExtension: StoreDownloaderExtension {}\n");
            project.AddFileToBuild(extension, project.AddFile(folder + "/Downloader.swift", folder + "/Downloader.swift"));
            foreach (var target in new[] { main, extension })
            {
                var relative = target == main ? "YuiAssets.entitlements" : folder + "/YuiAssets.entitlements";
                var entitlements = new PlistDocument();
                // Preserve existing app entitlements rather than dropping capabilities.
                var existing = project.GetBuildPropertyForAnyConfig(target, "CODE_SIGN_ENTITLEMENTS");
                if (!string.IsNullOrWhiteSpace(existing) && File.Exists(Path.Combine(directory, existing)))
                    entitlements.ReadFromFile(Path.Combine(directory, existing));
                var groups = entitlements.root.values.TryGetValue("com.apple.security.application-groups", out var existingGroups)
                    ? existingGroups.AsArray() : entitlements.root.CreateArray("com.apple.security.application-groups");
                if (!groups.values.Any(value => value.AsString() == group)) groups.AddString(group);
                entitlements.WriteToFile(Path.Combine(directory, relative));
                project.SetBuildProperty(target, "CODE_SIGN_ENTITLEMENTS", relative);
                project.SetBuildProperty(target, "IPHONEOS_DEPLOYMENT_TARGET", "26.0");
                project.SetBuildProperty(target, "CODE_SIGN_STYLE", "Automatic");
            }
            project.SetBuildProperty(extension, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(extension, "APPLICATION_EXTENSION_API_ONLY", "YES");
            project.SetBuildProperty(extension, "SKIP_INSTALL", "YES");
            project.AddFrameworkToProject(extension, "BackgroundAssets.framework", false);
            project.AddFrameworkToProject(extension, "StoreKit.framework", false);
            project.AddFrameworkToProject(extension, "ExtensionFoundation.framework", false);
            project.AddFrameworkToProject(framework, "BackgroundAssets.framework", true);
            // Unity 2022's AddAppExtension creates an NSExtension. Apple's managed
            // downloader is an ExtensionKit product and must be embedded in Extensions.
            var phase = project.GetCopyFilesBuildPhaseByTarget(main, "Embed App Extensions", "", "13");
            var serialized = project.WriteToString();
            serialized = RewriteExtensionObject(serialized, extension,
                "productType = \"com.apple.product-type.app-extension\";",
                "productType = \"com.apple.product-type.extensionkit-extension\";");
            serialized = RewriteExtensionObject(serialized, project.GetTargetProductFileRef(extension),
                "\"wrapper.app-extension\"", "\"wrapper.extensionkit-extension\"");
            serialized = RewriteExtensionObject(serialized, phase,
                "dstPath = \"\";", "dstPath = \"$(EXTENSIONS_FOLDER_PATH)\";");
            serialized = RewriteExtensionObject(serialized, phase,
                "dstSubfolderSpec = 13;", "dstSubfolderSpec = 16;");
            File.WriteAllText(projectPath, serialized);
        }

        private static string RewriteExtensionObject(string text, string guid, string before, string after)
        {
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Missing downloader project object.");
            var pattern = @"(?m)^\t\t" + Regex.Escape(guid) + @" /\*[^\r\n]*?\*/ = \{[\s\S]*?\};";
            var matches = Regex.Matches(text, pattern);
            if (matches.Count != 1 || !matches[0].Value.Contains(before))
                throw new InvalidOperationException("Unexpected downloader project layout: " + guid);
            return Regex.Replace(text, pattern, match => match.Value.Replace(before, after));
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
            project.AddFrameworkToProject(frameworkTargetGuid, "CoreML.framework", false);
            project.AddFrameworkToProject(frameworkTargetGuid, "Accelerate.framework", false);
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
