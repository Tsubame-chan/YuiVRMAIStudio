using System;
using System.IO;
using NUnit.Framework;
using YuiPhysicalAI.Editor;

namespace YuiPhysicalAI.Tests.Editor
{
    public class YuiMobileBuildAssetGuardTests
    {
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
