using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using YuiPhysicalAI.Avatar;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiAvatarBundleLifetimeTests
    {
        [UnityTest]
        public IEnumerator SharedBundleSurvivesFirstOwnerAndUnloadsAfterLastOwner()
        {
            var assets = "Assets/__YuiLeaseTest_" + Guid.NewGuid().ToString("N");
            var output = Path.Combine(Path.GetTempPath(), "yui-lease-" + Guid.NewGuid().ToString("N"));
            var bundlePath = Path.Combine(output, "avatar");
            GameObject first = null, second = null;
            var acquired = 0;
            try
            {
                Directory.CreateDirectory(assets); Directory.CreateDirectory(output);
                var material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, assets + "/surface.mat");
                var built = BuildPipeline.BuildAssetBundles(output, new [] { new AssetBundleBuild {
                    assetBundleName = "avatar", assetNames = new [] { assets + "/surface.mat" }, addressableNames = new [] { "surface" }
                } }, BuildAssetBundleOptions.ForceRebuildAssetBundle, EditorUserBuildSettings.activeBuildTarget);
                Assert.NotNull(built);
                var load1 = YuiAvatarBundleLease.AcquireAsync(bundlePath); acquired++;
                var load2 = YuiAvatarBundleLease.AcquireAsync(bundlePath); acquired++;
                while (!load1.IsCompleted || !load2.IsCompleted) yield return null;
                Assert.AreSame(load1.GetAwaiter().GetResult(), load2.GetAwaiter().GetResult());
                var loadedMaterial = load1.Result.LoadAsset<Material>("surface"); Assert.NotNull(loadedMaterial);
                first = new GameObject("first"); second = new GameObject("second");
                first.AddComponent<YuiAvatarBundleLease>().Own(bundlePath); acquired--;
                second.AddComponent<YuiAvatarBundleLease>().Own(bundlePath); acquired--;
                first.GetComponent<YuiAvatarBundleLease>().ReleaseOwner();
                Assert.IsTrue(YuiAvatarBundleLease.IsInUse(bundlePath)); Assert.IsTrue(loadedMaterial != null);
                second.GetComponent<YuiAvatarBundleLease>().ReleaseOwner();
                Assert.IsFalse(YuiAvatarBundleLease.IsInUse(bundlePath)); Assert.IsTrue(loadedMaterial == null);
            }
            finally
            {
                if (first != null) { first.GetComponent<YuiAvatarBundleLease>()?.ReleaseOwner(); UnityEngine.Object.DestroyImmediate(first); }
                if (second != null) { second.GetComponent<YuiAvatarBundleLease>()?.ReleaseOwner(); UnityEngine.Object.DestroyImmediate(second); }
                while (acquired-- > 0) YuiAvatarBundleLease.Release(bundlePath);
                AssetDatabase.DeleteAsset(assets);
                if (Directory.Exists(output)) Directory.Delete(output, true);
            }
        }
    }
}
