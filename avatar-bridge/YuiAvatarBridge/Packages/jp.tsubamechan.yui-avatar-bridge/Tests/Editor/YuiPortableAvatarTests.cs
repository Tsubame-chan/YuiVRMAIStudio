using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UniGLTF;
using UniVRM10;
using Object = UnityEngine.Object;

namespace Yui.AvatarBridge.Editor.Tests
{
    public sealed class YuiPortableAvatarTests
    {
        private sealed class Descriptor
        {
            public string lipSync = "VisemeBlendShape";
            public string[] VisemeBlendShapes = new string[15];
            public SkinnedMeshRenderer VisemeSkinnedMesh;
            public string MouthOpenBlendShapeName;
            public bool enableEyeLook = true;
            public Eyes customEyeLookSettings = new Eyes();
        }
        private sealed class Eyes
        {
            public string eyelidType = "Blendshapes";
            public SkinnedMeshRenderer eyelidsSkinnedMesh;
            public int[] eyelidsBlendshapes = { -1, -1, -1 };
        }
        private readonly List<Object> owned = new List<Object>();
        private GameObject root;
        [SetUp] public void SetUp() { root = new GameObject("Avatar"); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); foreach(var value in owned) if(value != null) Object.DestroyImmediate(value); owned.Clear(); }
        private SkinnedMeshRenderer Face(string name, params string[] shapes)
        {
            var child = new GameObject(name); child.transform.SetParent(root.transform);
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh(); owned.Add(mesh);
            mesh.vertices = new [] { Vector3.zero, Vector3.right, Vector3.up }; mesh.triangles = new [] { 0, 1, 2 }; mesh.RecalculateNormals();
            foreach (var shape in shapes) mesh.AddBlendShapeFrame(shape, 100, new [] { Vector3.forward, Vector3.zero, Vector3.zero }, new Vector3[3], new Vector3[3]);
            renderer.sharedMesh = mesh; return renderer;
        }
        [Test] public void DescriptorHasPriorityOverNamesAndSupportsSingleMouth()
        {
            var face = Face("Face", "OwnerDefinedMouth", "vrc.v_aa");
            var descriptor = new Descriptor { VisemeSkinnedMesh = face };
            descriptor.VisemeBlendShapes[10] = "OwnerDefinedMouth";
            var found = YuiAvatarBridgeAnalyzer.AnalyzeVisemes(root, descriptor, new [] { face });
            Assert.AreEqual("OwnerDefinedMouth", found[0].blendShape);
            descriptor.lipSync = "JawFlapBlendShape"; descriptor.MouthOpenBlendShapeName = "OwnerDefinedMouth";
            Assert.IsTrue(YuiAvatarBridgeAnalyzer.AnalyzeVisemes(root, descriptor, new [] { face }).All(v => v.found && v.blendShape == "OwnerDefinedMouth"));
            descriptor.lipSync = "JawFlapBone";
            Assert.IsFalse(YuiAvatarBridgeAnalyzer.AnalyzeVisemes(root, descriptor, new [] { face }).Any(v => v.found));
        }
        [Test] public void NamesAreConservativeAndMultipleFacesAreNotGuessed()
        {
            var face = Face("Face", "Angry", "Fcl_MTH_Angry", "notmouth_a", "blendShape1.MTH_I");
            var found = YuiAvatarBridgeAnalyzer.AnalyzeVisemes(root, null, new [] { face });
            Assert.IsFalse(found[0].found); Assert.IsTrue(found[1].found);
            var other = Face("Other", "MTH_I");
            found = YuiAvatarBridgeAnalyzer.AnalyzeVisemes(root, null, new [] { face, other });
            Assert.AreEqual("ambiguous", found[1].source); Assert.IsFalse(found[1].found);
        }
        [Test] public void BlinkUsesDescriptorThenPairedShapesAndIgnoresHiddenClothes()
        {
            var face = Face("Face", "MyClosedEyes", "eyeBlinkLeft", "eyeBlinkRight");
            var descriptor = new Descriptor(); descriptor.customEyeLookSettings.eyelidsSkinnedMesh = face;
            descriptor.customEyeLookSettings.eyelidsBlendshapes[0] = 0;
            Assert.AreEqual(0, YuiPortableExpressions.Blink(root, descriptor).Single().Index);
            descriptor.enableEyeLook = false;
            CollectionAssert.AreEqual(new [] { 1, 2 }, YuiPortableExpressions.Blink(root, descriptor).Select(b => b.Index));
            var hidden = Face("Hidden", "Blink"); hidden.gameObject.SetActive(false);
            Assert.AreEqual(2, YuiPortableExpressions.Blink(root, null).Length);
            face.enabled = false; Assert.AreEqual(0, YuiPortableExpressions.Blink(root, null).Length);
        }
        [Test] public void MeshPreparationPreservesVisibleCustomizationAndSource()
        {
            var face = Face("Face", "BodyCustomization"); var original = face.sharedMesh;
            face.SetBlendShapeWeight(0, 40);
            YuiPortableMeshes.Prepare(root, owned);
            Assert.AreNotSame(original, face.sharedMesh); Assert.AreEqual(Vector3.zero, original.vertices[0]);
            Assert.AreEqual(3, face.sharedMesh.uv.Length); Assert.AreEqual(0, original.uv.Length);
            Assert.That(face.sharedMesh.vertices[0].z, Is.EqualTo(.4f).Within(.0001));
            var delta = new Vector3[3]; face.sharedMesh.GetBlendShapeFrameVertices(0, 0, delta, new Vector3[3], new Vector3[3]);
            Assert.That(delta[0].z, Is.EqualTo(.6f).Within(.0001)); Assert.AreEqual(0, face.GetBlendShapeWeight(0));
        }
        [Test] public void ExportFailureLeavesExistingFileAndSourceUntouched()
        {
            var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".vrm");
            File.WriteAllText(output, "previous");
            try { Assert.Throws<InvalidOperationException>(() => YuiPortableAvatarExporter.Export(root, output, "Test", true)); Assert.AreEqual("previous", File.ReadAllText(output)); Assert.AreEqual(1, root.GetComponents<Component>().Length); }
            finally { File.Delete(output); }
        }
        [UnityTest] public IEnumerator RealHumanoidPortableRoundTrip()
        {
            var fixture = Environment.GetEnvironmentVariable("YUI_PORTABLE_VRM_FIXTURE");
            if (string.IsNullOrEmpty(fixture)) Assert.Ignore("Set YUI_PORTABLE_VRM_FIXTURE to a licensed humanoid VRM fixture.");
            var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".vrm");
            Vrm10Instance source = null, loaded = null;
            try
            {
                var task = Vrm10.LoadPathAsync(fixture, canLoadVrm0X: true, controlRigGenerationOption: ControlRigGenerationOption.None, awaitCaller: new ImmediateCaller());
                while (!task.IsCompleted) yield return null;
                source = task.GetAwaiter().GetResult(); source.UpdateType = Vrm10Instance.UpdateTypes.None;
                var count = source.GetComponentsInChildren<SkinnedMeshRenderer>().Length;
                var sourceName = source.name;
                YuiPortableAvatarExporter.Export(source.gameObject, output, "Portable round trip", true);
                Assert.AreEqual(sourceName, source.name); Assert.IsNotNull(source.Vrm); Assert.Greater(new FileInfo(output).Length, 1024);
                task = Vrm10.LoadPathAsync(output, canLoadVrm0X: false, controlRigGenerationOption: ControlRigGenerationOption.None, awaitCaller: new ImmediateCaller());
                while (!task.IsCompleted) yield return null;
                loaded = task.GetAwaiter().GetResult();
                Assert.IsTrue(loaded.GetComponent<Animator>().isHuman);
                Assert.AreEqual(count, loaded.GetComponentsInChildren<SkinnedMeshRenderer>().Length);
                Assert.NotNull(loaded.Vrm.Expression.Aa); Assert.NotNull(loaded.Vrm.Expression.Blink);
                loaded.Runtime.Expression.SetWeight(ExpressionKey.Aa, .8f);
                loaded.Runtime.Expression.SetWeight(new ExpressionKey(ExpressionPreset.blink), 1f);
                loaded.Runtime.Process();
                foreach (var binding in loaded.Vrm.Expression.Blink.MorphTargetBindings)
                {
                    var renderer = loaded.transform.Find(binding.RelativePath).GetComponent<SkinnedMeshRenderer>();
                    Assert.Greater(renderer.GetBlendShapeWeight(binding.Index), 90);
                }
                Debug.Log("Portable humanoid round trip passed: " + Path.GetFileName(fixture));
            }
            finally { if (source != null) Object.DestroyImmediate(source.gameObject); if (loaded != null) Object.DestroyImmediate(loaded.gameObject); if (File.Exists(output)) File.Delete(output); }
        }
    }
}
