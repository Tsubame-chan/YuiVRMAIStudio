using System.Linq;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using NUnit.Framework;
using UnityEngine;

namespace Yui.AvatarBridge.Editor.Tests
{
    public sealed class TestAvatarBehaviour : MonoBehaviour
    {
        public string value = "source";
    }

    public sealed class YuiAvatarBridgeTests
    {
        [Test]
        public void Export_PrimitiveHostPayloadLoadsAndDoesNotChangeSource()
        {
            var mac = Application.platform == RuntimePlatform.OSXEditor;
            var target = mac ? BuildTarget.StandaloneOSX : BuildTarget.StandaloneWindows64;
            if (!YuiAvatarBridgeExporter.IsBuildTargetAvailable(target)) Assert.Ignore("Host build support is missing.");
            var folder = Path.Combine(Path.GetTempPath(), "YuiBridgeRegression", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var source = new GameObject("Primitive");
            source.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            source.AddComponent<MeshRenderer>();
            try
            {
                var zip = Path.Combine(folder, "avatar.zip");
                var manifest = YuiAvatarBridgeExporter.Export(new YuiAvatarAnalysis { Root = source }, new YuiAvatarExportOptions
                {
                    OutputPath = zip, RightsAcknowledged = true,
                    BuildWindows = !mac, BuildMacOS = mac, BuildAndroid = false, BuildIOS = false,
                });
                Assert.AreEqual(1, manifest.payloads.Length);
                using (var archive = ZipFile.OpenRead(zip))
                {
                    Assert.AreEqual(3, archive.Entries.Count);
                    archive.GetEntry(manifest.payloads[0].filename).ExtractToFile(Path.Combine(folder, "avatar.bundle"));
                }
                var bundle = AssetBundle.LoadFromFile(Path.Combine(folder, "avatar.bundle"));
                Assert.IsNotNull(bundle, "Generated host bundle must actually load, not just hash correctly.");
                try { Assert.IsNotNull(bundle.LoadAsset<GameObject>("avatar/prefab")); }
                finally { bundle.Unload(true); }
                Assert.AreEqual("Primitive", source.name);
                Assert.AreEqual(3, source.GetComponents<Component>().Length);
                Assert.IsFalse(AssetDatabase.IsValidFolder("Assets/__YuiAvatarBridgeTemp"));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void SafeName_RemovesPathAndShellCharacters()
        {
            Assert.AreEqual("My_Avatar_test", YuiAvatarBridgeAnalyzer.SafeName("My Avatar/../test"));
            Assert.AreEqual("avatar", YuiAvatarBridgeAnalyzer.SafeName("///"));
        }

        [Test]
        public void Analyze_UsesFuzzyFiveVowelBlendShapeFallbacks()
        {
            var root = new GameObject("Avatar");
            var face = new GameObject("Face");
            face.transform.SetParent(root.transform);
            var renderer = face.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { name = "FaceMesh" };
            mesh.vertices = new[] { Vector3.zero };
            mesh.SetIndices(new[] { 0 }, MeshTopology.Points, 0);
            foreach (var name in new[] { "blendShape1.MTH_A", "Fcl_MTH_I", "mouth_u", "え", "vrc.v_oh" })
            {
                mesh.AddBlendShapeFrame(name, 100f, new[] { Vector3.zero }, new[] { Vector3.zero }, new[] { Vector3.zero });
            }
            renderer.sharedMesh = mesh;
            var material = new Material(Shader.Find("Standard")) { name = "FaceMaterial" };
            renderer.sharedMaterial = material;

            try
            {
                var analysis = YuiAvatarBridgeAnalyzer.Analyze(root);
                Assert.AreEqual(5, analysis.Diagnostics.visemes.Count(item => item.found));
                CollectionAssert.AreEqual(new[] { "aa", "ih", "ou", "ee", "oh" }, analysis.Diagnostics.visemes.Select(item => item.vowel));
                Assert.AreEqual(1, analysis.Diagnostics.materials.Length);
                Assert.AreEqual("FaceMaterial", analysis.Diagnostics.materials[0].name);
                Assert.IsTrue(analysis.Diagnostics.materials[0].shaderFound);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SanitizedClone_RemovesScriptsWithoutChangingSource()
        {
            var source = new GameObject("SourceAvatar");
            var sourceBehaviour = source.AddComponent<TestAvatarBehaviour>();
            var animator = source.AddComponent<Animator>();
            GameObject clone = null;
            try
            {
                clone = YuiAvatarBridgeExporter.CreateSanitizedClone(source);

                Assert.NotNull(source.GetComponent<TestAvatarBehaviour>());
                Assert.AreEqual("source", sourceBehaviour.value);
                Assert.NotNull(source.GetComponent<Animator>());
                Assert.IsNull(clone.GetComponent<TestAvatarBehaviour>());
                Assert.NotNull(clone.GetComponent<Animator>());
            }
            finally
            {
                if (clone != null) Object.DestroyImmediate(clone);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void Manifest_ContainsVersionedNativePayloadContract()
        {
            var manifest = new YuiAvatarBridgeManifest
            {
                avatarId = "abc",
                displayName = "Avatar",
                payloads = new[]
                {
                    new YuiAvatarBundlePayload { platform = "windows", filename = "payloads/avatar_windows.bundle", sha256 = "hash", sizeBytes = 42 },
                    new YuiAvatarBundlePayload { platform = "android", filename = "payloads/avatar_android.bundle", sha256 = "hash2", sizeBytes = 84 },
                },
            };

            var json = JsonUtility.ToJson(manifest);

            StringAssert.Contains("\"schemaVersion\":1", json);
            StringAssert.Contains("\"format\":\"unity-avatar-package\"", json);
            StringAssert.Contains("avatar_windows.bundle", json);
            StringAssert.Contains("avatar_android.bundle", json);
        }
    }
}
