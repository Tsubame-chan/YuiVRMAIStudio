using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if UNITY_IOS
using UnityEditor.Callbacks;
#endif
namespace YuiPhysicalAI.Editor
{
    public sealed class YuiKokoroBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -94;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.StandaloneOSX) BuildMac();
            if (report.summary.platform == BuildTarget.Android && !File.Exists("Assets/Plugins/Android/Kokoro/arm64-v8a/libYuiKokoroBridge.so"))
                throw new BuildFailedException("Build the Kokoro Android wrapper with scripts/build_kokoro_native.py --platform android --ndk <NDK path> first.");
        }
        [MenuItem("Yui/Build/Kokoro macOS bridge")]
        public static void BuildMac()
        {
#if UNITY_EDITOR_OSX
            const string target = "Assets/Plugins/macOS/libYuiKokoroBridge.dylib";
            var start = new ProcessStartInfo("/usr/bin/xcrun", "--sdk macosx clang++ -std=c++17 -O2 -dynamiclib -fvisibility=hidden -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 Assets/Plugins/iOS/YuiKokoroBridge.cpp -o " + target)
            { UseShellExecute = false, RedirectStandardError = true, WorkingDirectory = Directory.GetParent(UnityEngine.Application.dataPath).FullName };
            using var process = Process.Start(start); var error = process.StandardError.ReadToEnd(); process.WaitForExit();
            if (process.ExitCode != 0) throw new BuildFailedException(error);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            var importer = (PluginImporter)AssetImporter.GetAtPath(target);
            importer.SetCompatibleWithAnyPlatform(false); importer.SetCompatibleWithEditor(true);
            importer.SetEditorData("CPU", "AnyCPU"); importer.SetEditorData("OS", "OSX");
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
            importer.SetPlatformData(BuildTarget.StandaloneOSX, "CPU", "AnyCPU"); importer.SaveAndReimport();
#else
            throw new BuildFailedException("Build the Kokoro macOS bridge on macOS.");
#endif
        }
#if UNITY_IOS
        [PostProcessBuild(45)]
        public static void CopyHeader(BuildTarget target, string output)
        {
            if (target != BuildTarget.iOS) return;
            var folder = Path.Combine(output, "Libraries/Plugins/iOS/Kokoro"); Directory.CreateDirectory(folder);
            File.Copy("Assets/Plugins/iOS/Kokoro/onnxruntime_c_api.h", Path.Combine(folder, "onnxruntime_c_api.h"), true);
        }
#endif
    }
}
