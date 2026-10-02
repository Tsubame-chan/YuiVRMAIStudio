using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YuiPhysicalAI.Avatar;

namespace YuiPhysicalAI.EditorTools
{
    public sealed class YuiRuntimeAvatarShaderBuildGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public static void EnsureIncluded()
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = settings.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in YuiVrmMaterialDescriptorGenerator.RequiredShaders)
            {
                var shader = Shader.Find(name);
                if (shader == null) throw new BuildFailedException("Runtime VRM shader is not installed: " + name);
                var found = false;
                for (var i = 0; i < shaders.arraySize; i++)
                    if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader) found = true;
                if (!found) {
                    var i = shaders.arraySize; shaders.InsertArrayElementAtIndex(i);
                    shaders.GetArrayElementAtIndex(i).objectReferenceValue = shader;
                }
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
        public void OnPreprocessBuild(BuildReport report) { EnsureIncluded(); }
        public void OnPostprocessBuild(BuildReport report)
        {
            File.WriteAllText(Path.Combine(Application.dataPath, "../runtime-vrm-shaders.txt"),
                report.summary.platform + "\n" + report.summary.outputPath + "\nAlways included:\n" +
                string.Join("\n", YuiVrmMaterialDescriptorGenerator.RequiredShaders) +
                "\nThis is build configuration evidence; validate material loading on the device.\n");
        }
    }
}
