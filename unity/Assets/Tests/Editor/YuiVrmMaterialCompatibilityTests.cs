using NUnit.Framework;
using UniGLTF;
using UnityEditor;
using UnityEngine;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.EditorTools;

namespace YuiPhysicalAI.Tests.Editor
{
    public class YuiVrmMaterialCompatibilityTests
    {
        [TestCase("{}", "Standard")]
        [TestCase("{\"alphaMode\":\"BLEND\",\"doubleSided\":true}", "Standard")]
        [TestCase("{\"extensions\":{\"KHR_materials_unlit\":{}}}", "UniGLTF/UniUnlit")]
        [TestCase("{\"extensions\":{\"VRMC_materials_mtoon\":{\"specVersion\":\"1.0\"}}}", "VRM10/MToon10")]
        public void StandardVrmMaterialRoutesResolve(string material, string shader)
        {
            using var data = GlbLowLevelParser.ParseGltf("fixture.gltf", "{\"asset\":{\"version\":\"2.0\"},\"materials\":["+material+"]}", null, null, new MigrationFlags());
            var descriptor = new YuiVrmMaterialDescriptorGenerator().Get(data, 0);
            Assert.AreEqual(shader, descriptor.Shader.name);
        }
        [Test] public void UnspecifiedMaterialHasRuntimeShader()
        {
            Assert.AreEqual("Standard", new YuiVrmMaterialDescriptorGenerator().GetGltfDefault().Shader.name);
        }
        [Test] public void BuildKeepsAllImporterBranchesEvenWithoutSceneMaterials()
        {
            YuiRuntimeAvatarShaderBuildGuard.EnsureIncluded();
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var included = settings.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in YuiVrmMaterialDescriptorGenerator.RequiredShaders) {
                var found=false;
                for(var i=0;i<included.arraySize;i++)
                    if(included.GetArrayElementAtIndex(i).objectReferenceValue==Shader.Find(name)) found=true;
                Assert.IsTrue(found,name+" must survive scene-based shader stripping.");
            }
        }
    }
}
