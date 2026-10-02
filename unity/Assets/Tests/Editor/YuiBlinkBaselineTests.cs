using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.Core;
using ChatdollKit.Model;
namespace YuiPhysicalAI.Tests
{
    public class YuiBlinkBaselineTests
    {
        [Test]
        public void BuiltInAvatarGetsBlinkWhenLegacyControllerLivesOutsideItsHierarchy()
        {
            var host = new GameObject("runtime-host");
            host.SetActive(false);
            var avatar = new GameObject("private-avatar");
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { Vector3.zero };
                mesh.AddBlendShapeFrame("vrc.Blink", 100, new[] { Vector3.up }, new Vector3[1], new Vector3[1]);
                var renderer = avatar.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.SetBlendShapeWeight(0, 25);
                // Matches the real scene: AIAvatar owns Blink/ModelController,
                // while the baked Yui avatar is a separate scene root.
                var legacy = host.AddComponent<Blink>();
                legacy.enabled = false;
                var model = host.AddComponent<ModelController>();
                model.enabled = false;
                var switcher = host.AddComponent<YuiAvatarSwitcher>();
                switcher.Configure(avatar, null, null, null, null, null, model, null);
                switcher.SetAvatarSlot(YuiAvatarSlots.DemoAvatar);
                var driver = avatar.GetComponent<YuiAvatarExpressionDriver>();
                Assert.That(driver, Is.Not.Null, "Disabled shared Blink must be replaced on the selected built-in avatar.");
                driver.SetBlink(1);
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(100));
                driver.SetBlink(0);
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(25));
                switcher.SetAvatarSlot(YuiAvatarSlots.DemoAvatar);
                Assert.That(avatar.GetComponents<YuiAvatarExpressionDriver>().Length, Is.EqualTo(1));
                Assert.That(legacy.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(avatar); Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ArbitraryImportedAvatarGetsOneDriverAcrossRebinding()
        {
            var root=new GameObject("user-chosen-humanoid");
            try {
                YuiAvatarExpressionDriver.BindTo(root);
                root.SetActive(false);root.SetActive(true);
                YuiAvatarExpressionDriver.BindTo(root);
                Assert.That(root.GetComponents<YuiAvatarExpressionDriver>().Length,Is.EqualTo(1));
            } finally {Object.DestroyImmediate(root);}
        }
        [Test]
        public void ExistingNativeBlinkIsNotDrivenTwice()
        {
            var root=new GameObject("native-blink-avatar");
            try {
                root.AddComponent<UnityChan.AutoBlink>().isActive=true;
                YuiAvatarExpressionDriver.BindTo(root);
                Assert.That(root.GetComponent<YuiAvatarExpressionDriver>(),Is.Null);
            } finally {Object.DestroyImmediate(root);}
        }
        [Test]
        public void EmptyCombinedBlinkDoesNotMaskAuthoredSplitBlink()
        {
            var expression=ScriptableObject.CreateInstance<UniVRM10.VRM10Expression>();
            try {Assert.That(YuiAvatarExpressionDriver.HasBindings(expression),Is.False);}
            finally {Object.DestroyImmediate(expression);}
        }
        [Test]
        public void LegacyBlinkClosesAndReturnsToAuthoredHalfLid()
        {
            var root = new GameObject("legacy-face");
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { Vector3.zero };
                mesh.AddBlendShapeFrame("vrc.Blink",100,new[] { Vector3.up },new Vector3[1],new Vector3[1]);
                var renderer=root.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh=mesh;
                renderer.SetBlendShapeWeight(0,25);
                var driver=root.AddComponent<YuiAvatarExpressionDriver>();
                driver.SetBlink(1); Assert.That(renderer.GetBlendShapeWeight(0),Is.EqualTo(100));
                driver.SetBlink(.5f); Assert.That(renderer.GetBlendShapeWeight(0),Is.EqualTo(62.5f));
                driver.SetBlink(0); Assert.That(renderer.GetBlendShapeWeight(0),Is.EqualTo(25));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); }
        }
    }
}
