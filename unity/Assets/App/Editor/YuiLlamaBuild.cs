using System;
using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
#endif
namespace YuiPhysicalAI.Editor
{
    public sealed class YuiLlamaBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 215;
        public void OnPreprocessBuild(BuildReport report) {
            if(report.summary.platform!=BuildTarget.StandaloneOSX && report.summary.platform!=BuildTarget.iOS)return;
            var root=Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName;
            var runtime=Path.Combine(root,"tools/llama-runtime/apple/llama.xcframework");
            if(!Directory.Exists(runtime))throw new BuildFailedException("Prepare the GGUF runtime with scripts/prepare_llama_runtime_macos.py before building.");
            if(report.summary.platform==BuildTarget.StandaloneOSX && !File.Exists("Assets/Plugins/macOS/YuiLlama.bundle/Contents/MacOS/YuiLlama"))throw new BuildFailedException("The GGUF macOS plugin is missing. Run scripts/prepare_llama_runtime_macos.py.");
        }
        public void OnPostprocessBuild(BuildReport report) {
            if(report.summary.platform==BuildTarget.StandaloneOSX) {
                RestoreMacFramework(report.summary.outputPath);
                return;
            }
#if UNITY_IOS
            if(report.summary.platform!=BuildTarget.iOS)return;
            var root=Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName;
            var output=report.summary.outputPath;
            var dest=Path.Combine(output,"Frameworks/llama.xcframework");
            Copy(Path.Combine(root,"tools/llama-runtime/apple/llama.xcframework"),dest);
            // Unity exports .cpp/.h plugins but omits nested .hpp dependencies.
            var jinjaTarget=Path.Combine(output,"Libraries/Plugins/iOS/YuiLlamaJinja");
            if(Directory.Exists(jinjaTarget))Directory.Delete(jinjaTarget,true);
            Copy(Path.Combine(root,"unity/Assets/Plugins/iOS/YuiLlamaJinja"),jinjaTarget);
            var projectPath=PBXProject.GetPBXProjectPath(output);var project=new PBXProject();project.ReadFromFile(projectPath);
            var framework=project.GetUnityFrameworkTargetGuid();var main=project.GetUnityMainTargetGuid();
            var guid=project.AddFile("Frameworks/llama.xcframework","Frameworks/llama.xcframework",PBXSourceTree.Source);
            project.AddFileToBuild(framework,guid);project.AddFileToEmbedFrameworks(main,guid);
            // Public C ABI headers are identical across Apple slices. Append to the
            // normal setting; an SDK-qualified setting replaces Unity's IL2CPP paths.
            project.AddBuildProperty(framework,"HEADER_SEARCH_PATHS","\"$(PROJECT_DIR)/Frameworks/llama.xcframework/ios-arm64/llama.framework/Headers\"");
            project.AddBuildProperty(framework,"HEADER_SEARCH_PATHS","\"$(PROJECT_DIR)/Libraries/Plugins/iOS/YuiLlamaJinja\"");
            project.SetBuildProperty(framework,"GCC_ENABLE_CPP_RTTI","YES");
            project.AddBuildProperty(framework,"LD_RUNPATH_SEARCH_PATHS","@executable_path/Frameworks");
            project.AddBuildProperty(main,"LD_RUNPATH_SEARCH_PATHS","@executable_path/Frameworks");
            project.WriteToFile(projectPath);
#endif
        }
        static void RestoreMacFramework(string app) {
            // Unity expands nested framework symlinks and leaves their resource seal invalid.
            var root=Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName;
            var bundle=Path.Combine(app,"Contents/PlugIns/YuiLlama.bundle");
            var target=Path.Combine(bundle,"Contents/Frameworks/llama.framework");
            if(Directory.Exists(target))Directory.Delete(target,true);
            Copy(Path.Combine(root,"tools/llama-runtime/apple/llama.xcframework/macos-arm64_x86_64/llama.framework"),target);
            // Preserve Unity's ad-hoc development signing; this is not Developer ID/notarization.
            foreach(var path in new[]{target,bundle,app})Run("/usr/bin/codesign","--force --sign - \""+path+"\"");
            Run("/usr/bin/codesign","--verify --deep --strict \""+app+"\"");
        }
        static void Run(string executable,string arguments) {
            var info=new ProcessStartInfo(executable,arguments){UseShellExecute=false};
            using var p=Process.Start(info);p.WaitForExit();if(p.ExitCode!=0)throw new BuildFailedException("Native GGUF packaging failed: "+executable);
        }
        static void Copy(string source,string target) {
            // ditto retains signed Apple framework symlinks and resource layout.
            var info=new ProcessStartInfo("/usr/bin/ditto", "\""+source+"\" \""+target+"\"") {UseShellExecute=false};
            using var p=Process.Start(info);p.WaitForExit();if(p.ExitCode!=0)throw new BuildFailedException("Could not copy llama.xcframework.");
        }
    }
}
