using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace YuiPhysicalAI.Editor
{
    public sealed class YuiMacFilePickerPostbuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 210;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneOSX) return;
            var plugin = Path.Combine(report.summary.outputPath, "Contents/Plugins/libYuiMacFilePicker.dylib");
            if (!File.Exists(plugin)) throw new BuildFailedException("The macOS Player is missing its file picker: " + plugin);
        }
    }

    public sealed class YuiMacFilePickerPrebuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.StandaloneOSX) YuiMacFilePickerBuild.EnsureBuilt();
        }
    }

    public static class YuiMacFilePickerBuild
    {
        private const string Source = "Assets/App/Editor/Native/YuiMacFilePicker.mm.txt";
        private const string Plugin = "Assets/Plugins/macOS/libYuiMacFilePicker.dylib";

        public static void EnsureBuilt()
        {
#if UNITY_EDITOR_OSX
            var project = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            var sourcePath = Path.Combine(project, Source);
            var pluginPath = Path.Combine(project, Plugin);
            if (!File.Exists(pluginPath) || File.GetLastWriteTimeUtc(sourcePath) > File.GetLastWriteTimeUtc(pluginPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pluginPath));
                var start = new ProcessStartInfo("/usr/bin/xcrun",
                    "--sdk macosx clang++ -dynamiclib -fobjc-arc -arch arm64 -arch x86_64 " +
                    "-mmacosx-version-min=11.0 -framework Cocoa -framework ImageIO -framework CoreGraphics -x objective-c++ " + Source + " -o " + Plugin)
                { UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = project };
                using var process = Process.Start(start);
                var errors = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException("macOS file picker build failed: " + errors);
                AssetDatabase.ImportAsset(Plugin, ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = AssetImporter.GetAtPath(Plugin) as PluginImporter;
            if (importer == null) throw new InvalidOperationException("macOS file picker plugin is missing.");
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(false);
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
            importer.SetPlatformData(BuildTarget.StandaloneOSX, "CPU", "AnyCPU");
            importer.SaveAndReimport();
#else
            if (!File.Exists(Plugin)) throw new InvalidOperationException("Build the macOS file picker on a Mac before creating a macOS Player.");
#endif
        }
    }
}
