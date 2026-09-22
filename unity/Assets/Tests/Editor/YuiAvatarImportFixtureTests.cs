using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UniGLTF;
using UniVRM10;
using YuiPhysicalAI.Avatar;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiAvatarImportFixtureTests
    {
        [UnityTest]
        public IEnumerator ImportExternalFixturesAndReleaseTheirAssets()
        {
            var fixtures = Environment.GetEnvironmentVariable("YUI_AVATAR_BETA_FIXTURES");
            if (string.IsNullOrEmpty(fixtures)) Assert.Ignore("Set YUI_AVATAR_BETA_FIXTURES to newline-separated local .vrm/.zip paths.");
            foreach (var path in fixtures.Split(new [] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                GameObject root = null;
                try
                {
                    if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        var task = YuiAvatarPackageLoader.LoadAsync(path, null);
                        while (!task.IsCompleted) yield return null;
                        root = task.GetAwaiter().GetResult().Root;
                        Assert.NotNull(root.GetComponent<YuiAvatarBundleLease>());
                    }
                    else
                    {
                        var task = Vrm10.LoadPathAsync(path, canLoadVrm0X: true, controlRigGenerationOption: ControlRigGenerationOption.None,
                            showMeshes: false, awaitCaller: new ImmediateCaller(), materialGenerator: new BuiltInVrm10MaterialDescriptorGenerator());
                        while (!task.IsCompleted) yield return null;
                        root = task.GetAwaiter().GetResult().gameObject;
                    }
                    Assert.NotNull(root);
                    var animator = root.GetComponentInChildren<Animator>(true);
                    Assert.NotNull(animator); Assert.IsTrue(animator.isHuman);
                    var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    Assert.Greater(renderers.Length, 0);
                    foreach (var renderer in renderers) Assert.NotNull(renderer.sharedMesh);
                    var expressions = root.AddComponent<YuiAvatarExpressionDriver>();
                    expressions.SetBlink(1); expressions.SetBlink(0); expressions.ApplyFace("happy"); expressions.ApplyFace("neutral");
                    var motion = root.GetComponent<YuiAvatarSpringMotion>();
                    if (motion != null) for (var i = 0; i < 120; i++) { root.transform.rotation = Quaternion.Euler(0, Mathf.Sin(i * .1f) * 30, 0); motion.Step(1f / 60f); }
                    Debug.Log($"Avatar fixture accepted: {Path.GetFileName(path)}, meshes={renderers.Length}, springJoints={motion?.JointCount ?? 0}");
                }
                finally
                {
                    if (root != null) { root.GetComponent<YuiAvatarBundleLease>()?.ReleaseOwner(); UnityEngine.Object.DestroyImmediate(root); }
                }
            }
        }
    }
}
