using System;
using System.IO;
using NUnit.Framework;
using YuiPhysicalAI.Editor;

namespace YuiPhysicalAI.Tests.Editor
{
    public class YuiMobileBuildAssetGuardTests
    {
        [TestCase("essential", false)]
        [TestCase("prefetch", false)]
        [TestCase("onDemand", true)]
        public void AppleDeliveryRejectsAutomaticDownloadArchive(string policy, bool accepted)
        {
            var root = Path.Combine(Path.GetTempPath(), "yui-policy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var archive = Path.Combine(root, "model.aar"); File.WriteAllText(archive, "synthetic archive");
                using var sha = System.Security.Cryptography.SHA256.Create();
                var hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(archive))).Replace("-", "").ToLowerInvariant();
                var evidence = new Newtonsoft.Json.Linq.JObject {
                    ["filename"] = Path.GetFileName(archive),
                    ["size_bytes"] = new FileInfo(archive).Length, ["sha256"] = hash,
                    ["asset_pack_id"] = "yui-gemma-e4b-v1", ["download_policy"] = policy,
                    ["model_sha256"] = "0b2a8980ce155fd97673d8e820b4d29d9c7d99b8fa6806f425d969b145bd52e0"
                };
                File.WriteAllText(Path.Combine(root, "archive-evidence.json"), evidence.ToString());
                if (accepted) Assert.DoesNotThrow(() => YuiMobileBuildAssetGuard.ValidateAppleAssetArchive(archive));
                else Assert.Throws<UnityEditor.Build.BuildFailedException>(() => YuiMobileBuildAssetGuard.ValidateAppleAssetArchive(archive));
            }
            finally { Directory.Delete(root, true); }
        }
        [Test] public void AppleDeliveryStillRequiresBundledVoiceAndVerifiedExternalModel()
        {
            var root = Path.Combine(Path.GetTempPath(), "yui-apple-assets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var missing = YuiMobileBuildAssetGuard.MissingFiles(root, appleHostedModel: true);
                Assert.AreEqual(6, missing.Count);
                Assert.That(missing, Does.Contain("StreamingAssets/YuiLocalAI/Models/gemma-4-E2B-it.litertlm"), "Optional Apple 4B must not remove the bundled standard model.");
                Assert.Throws<UnityEditor.Build.BuildFailedException>(() => YuiMobileBuildAssetGuard.ValidateAppleAssetArchive(null));
                var archive = Path.Combine(root, "model.aar"); File.WriteAllText(archive, "incomplete");
                Assert.Throws<UnityEditor.Build.BuildFailedException>(() => YuiMobileBuildAssetGuard.ValidateAppleAssetArchive(archive));
            }
            finally { Directory.Delete(root, true); }
        }
        [Test] public void PublicIosRejectsExperimentalVoicePayloadWithoutDeletingSourceData()
        {
            var root = Path.Combine(Path.GetTempPath(), "yui-public-ios-" + Guid.NewGuid().ToString("N"));
            var aivis = Path.Combine(root, "StreamingAssets/YuiLocalAI/Aivis/Models");
            Directory.CreateDirectory(aivis);
            try
            {
                YuiMobileBuildAssetGuard.ValidatePublicIosPayload(root);
                var file = Path.Combine(aivis, "test.aivmx");
                File.WriteAllText(file, "synthetic voice");
                Assert.Throws<UnityEditor.Build.BuildFailedException>(() => YuiMobileBuildAssetGuard.ValidatePublicIosPayload(root));
                Assert.AreEqual("synthetic voice", File.ReadAllText(file));
            }
            finally { Directory.Delete(root, true); }
        }
        [TestCase("gemma.litertlm_123.vision_encoder.xnnpack_cache", true)]
        [TestCase("gemma.litertlm_123.vision_encoder.xnnpack_cache.meta", true)]
        [TestCase("gemma.litertlm.task_cache", true)]
        [TestCase("gemma_mldrift_gpu_cache.bin", true)]
        [TestCase("gemma-4-E2B-it.litertlm", false)]
        [TestCase("gemma-4-E2B-it.litertlm.meta", false)]
        [TestCase("meimei_himari_1.vvm", false)]
        public void ExcludeGeneratedCachesWithoutRemovingModels(string name, bool generated)
        { Assert.AreEqual(generated, YuiMobileBuildAssetGuard.IsGeneratedModelCache(name)); }

        [Test] public void CleanSourceAndEmptyFilesCannotBecomeAnUnusableMobileBuild()
        {
            var root = Path.Combine(Path.GetTempPath(), "yui-mobile-assets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                Assert.AreEqual(YuiMobileBuildAssetGuard.RequiredFiles.Length, YuiMobileBuildAssetGuard.MissingFiles(root).Count);
                foreach (var relative in YuiMobileBuildAssetGuard.RequiredFiles)
                {
                    var file = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllBytes(file, Array.Empty<byte>());
                }
                Assert.AreEqual(YuiMobileBuildAssetGuard.RequiredFiles.Length, YuiMobileBuildAssetGuard.MissingFiles(root).Count);
                foreach (var relative in YuiMobileBuildAssetGuard.RequiredFiles) File.WriteAllText(Path.Combine(root, relative), "synthetic asset");
                Assert.IsEmpty(YuiMobileBuildAssetGuard.MissingFiles(root));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
