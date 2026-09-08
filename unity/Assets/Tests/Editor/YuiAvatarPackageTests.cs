using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.Avatar;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiAvatarPackageTests
    {
        [Test]
        public void CurrentPlatform_UsesEditorHostRegardlessOfSelectedBuildTarget()
        {
#if UNITY_EDITOR_OSX
            Assert.AreEqual("macos", YuiAvatarPackageLoader.CurrentPlatform());
#elif UNITY_EDITOR_WIN
            Assert.AreEqual("windows", YuiAvatarPackageLoader.CurrentPlatform());
#endif
        }

        [Test]
        public void ValidateManifest_AcceptsPublishedSchema()
        {
            var manifest = JObject.Parse(@"{
  ""schemaVersion"": 1,
  ""format"": ""unity-avatar-package"",
  ""avatarId"": ""avatar-123""
}");

            Assert.DoesNotThrow(() => YuiAvatarPackageLoader.ValidateManifest(manifest));
        }

        [Test]
        public void ValidateManifest_RejectsArbitraryZip()
        {
            var manifest = JObject.Parse(@"{
  ""schemaVersion"": 1,
  ""format"": ""something-else"",
  ""avatarId"": ""avatar-123""
}");

            Assert.Throws<InvalidDataException>(() => YuiAvatarPackageLoader.ValidateManifest(manifest));
        }

        [TestCase("../escape.bundle")]
        [TestCase("payloads/..")]
        [TestCase("payloads/./avatar.bundle")]
        [TestCase("payloads/..\\escape.bundle")]
        [TestCase("payloads/run.dll")]
        [TestCase("/absolute/avatar.bundle")]
        [TestCase("C:/avatar.bundle")]
        public void ValidateArchive_RejectsUnsafeEntries(string entryName)
        {
            using var stream = new MemoryStream();
            using (var writable = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                writable.CreateEntry(entryName);
            }
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            Assert.Throws<InvalidDataException>(() => YuiAvatarPackageLoader.ValidateArchive(archive));
        }

        [TestCase("manifest.json", "manifest.json")]
        [TestCase("manifest.json", "MANIFEST.JSON")]
        [TestCase("payloads/avatar.bundle", "payloads\\avatar.bundle")]
        public void ValidateArchive_RejectsAmbiguousDuplicateNames(string first, string second)
        {
            using var stream = new MemoryStream();
            using (var writable = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                writable.CreateEntry(first);
                writable.CreateEntry(second);
            }
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            Assert.Throws<InvalidDataException>(() => YuiAvatarPackageLoader.ValidateArchive(archive));
        }

        [Test]
        public void ValidateArchive_RejectsOversizedManifestBeforeParsing()
        {
            using var stream = new MemoryStream();
            using (var writable = new ZipArchive(stream, ZipArchiveMode.Create, true))
            using (var content = writable.CreateEntry("manifest.json").Open())
            {
                var bytes = new byte[1024 * 1024 + 1];
                content.Write(bytes, 0, bytes.Length);
            }
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            Assert.Throws<InvalidDataException>(() => YuiAvatarPackageLoader.ValidateArchive(archive));
        }

        [Test]
        public void PackageMetadata_ResolvesExplicitArbitraryVisemeName()
        {
            var root = new GameObject("Avatar");
            var face = new GameObject("FaceMesh");
            face.transform.SetParent(root.transform);
            var renderer = face.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { vertices = new[] { Vector3.zero } };
            mesh.SetIndices(new[] { 0 }, MeshTopology.Points, 0);
            mesh.AddBlendShapeFrame("CreatorSpecificMouthA", 100f, new[] { Vector3.zero }, new[] { Vector3.zero }, new[] { Vector3.zero });
            renderer.sharedMesh = mesh;
            var metadata = root.AddComponent<YuiAvatarPackageMetadata>();
            metadata.Configure("id", "Avatar", "avatar.zip", new[]
            {
                new YuiAvatarPackageMetadata.Viseme
                {
                    Vowel = "aa",
                    RendererPath = "FaceMesh",
                    BlendShape = "CreatorSpecificMouthA",
                },
            });

            try
            {
                Assert.IsTrue(metadata.TryGetViseme("aa", out var resolvedRenderer, out var blendShape));
                Assert.AreSame(renderer, resolvedRenderer);
                Assert.AreEqual("CreatorSpecificMouthA", blendShape);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}
