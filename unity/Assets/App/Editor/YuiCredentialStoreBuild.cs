using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace YuiPhysicalAI.Editor
{
    public sealed class YuiCredentialStoreBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => -95;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.StandaloneOSX) EnsureBuilt();
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.StandaloneOSX &&
                !File.Exists(Path.Combine(report.summary.outputPath, "Contents/Plugins/libYuiCredentialStore.dylib")))
                throw new BuildFailedException("macOS credential store is missing from the Player.");
        }
        public static void EnsureBuilt()
        {
            const string source = "Assets/Plugins/iOS/YuiCredentialStore.mm";
            const string plugin = "Assets/Plugins/macOS/libYuiCredentialStore.dylib";
#if UNITY_EDITOR_OSX
            if (!File.Exists(plugin) || File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(plugin))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(plugin));
                var start = new ProcessStartInfo("/usr/bin/xcrun", "--sdk macosx clang++ -dynamiclib -fobjc-arc -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 -framework Foundation -framework Security " + source + " -o " + plugin)
                { UseShellExecute = false, RedirectStandardError = true, WorkingDirectory = Directory.GetParent(UnityEngine.Application.dataPath).FullName };
                using var process = Process.Start(start);
                var error = process.StandardError.ReadToEnd(); process.WaitForExit();
                if (process.ExitCode != 0) throw new BuildFailedException(error);
                AssetDatabase.ImportAsset(plugin, ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = AssetImporter.GetAtPath(plugin) as PluginImporter;
            if (importer == null) throw new BuildFailedException("macOS credential store was not imported.");
            importer.SetCompatibleWithAnyPlatform(false); importer.SetCompatibleWithEditor(false);
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
            importer.SetPlatformData(BuildTarget.StandaloneOSX, "CPU", "AnyCPU"); importer.SaveAndReimport();
#else
            if (!File.Exists(plugin)) throw new BuildFailedException("Build the macOS credential store on a Mac first.");
#endif
        }
    }
}
