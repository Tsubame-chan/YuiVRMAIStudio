using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace YuiPhysicalAI.Editor
{
    // Also runs for manual Build Player, not only release packaging scripts.
    public sealed class YuiWindowsFilePickerBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 200;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
            var root = Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName;
            var source = Path.Combine(root, "tools/YuiFilePickerHelper/YuiFilePickerHelper.cs");
            if (!File.Exists(source)) throw new BuildFailedException("Missing Windows file picker source: " + source);
            var output = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "YuiFilePickerHelper.exe");
#if UNITY_EDITOR_WIN
            var compiler = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Microsoft.NET\Framework64\v4.0.30319\csc.exe");
            var arguments = "/nologo /target:winexe /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:\"" + output + "\" \"" + source + "\"";
#else
            var compiler = File.Exists("/Library/Frameworks/Mono.framework/Versions/Current/Commands/mcs")
                ? "/Library/Frameworks/Mono.framework/Versions/Current/Commands/mcs" : "mcs";
            var arguments = "-target:winexe -sdk:4.5 -r:System.Windows.Forms -r:System.Drawing -out:\"" + output + "\" \"" + source + "\"";
#endif
            var start = new ProcessStartInfo(compiler, arguments) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardError=true };
            using var process = Process.Start(start);
            var errors = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 || !File.Exists(output)) throw new BuildFailedException("Windows file picker compilation failed: " + errors);
        }
    }
}
