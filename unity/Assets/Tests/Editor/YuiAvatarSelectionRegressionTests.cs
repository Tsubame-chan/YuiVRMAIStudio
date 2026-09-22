using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public class YuiAvatarSelectionRegressionTests
    {
        [Test]
        public void PublicLegacyDemoSelectionResolvesToBundledAvatar()
        {
            Assert.AreEqual(YuiAvatarSlots.UnityChanDefault,YuiAvatarSlots.NormalizeForProfile(YuiAvatarSlots.DemoAvatar,true));
            Assert.AreEqual(YuiAvatarSlots.UnityChanDefault,YuiAvatarSlots.NormalizeForProfile("demo_avatar",true));
            Assert.AreEqual(YuiAvatarSlots.DemoAvatar,YuiAvatarSlots.NormalizeForProfile(YuiAvatarSlots.DemoAvatar,false));
        }
        [Test]
        public void EmptySlotCannotAliasAnImportedAvatarInAnotherSlot()
        {
            var host=new GameObject("switcher");var demo=new GameObject("bundled");var custom=new GameObject("custom",typeof(Animator));
            try
            {
                var switcher=host.AddComponent<YuiAvatarSwitcher>();
                switcher.Configure(null,demo,null,null,null,null,null,null);
                switcher.SetCustomAvatar(custom,YuiAvatarSlots.CustomVrm1,false);
                Assert.AreSame(custom,switcher.SetAvatarSlot(YuiAvatarSlots.CustomVrm1));
                Assert.AreSame(demo,switcher.SetAvatarSlot(YuiAvatarSlots.CustomVrm2));
                Assert.IsFalse(custom.activeSelf);
                Assert.AreSame(demo,switcher.SetAvatarSlot(YuiAvatarSlots.UnityChanDefault));
                Assert.IsTrue(demo.activeSelf);
            }
            finally { Object.DestroyImmediate(host);Object.DestroyImmediate(demo);Object.DestroyImmediate(custom); }
        }
        [Test]
        public void BundledControllerStartsAtRestWithoutChangingSourceAsset()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnityChan/Prefabs/unitychan.prefab");
            Assert.NotNull(prefab);
            var instance=Object.Instantiate(prefab);
            try
            {
                var animator=instance.GetComponent<Animator>();
                animator.Rebind();animator.Update(0);
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("JUMP00B"),"The sample's authored entry motion is the source of the jump.");
                YuiAvatarSwitcher.StartBundledAvatarAtRest(instance);
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("WAIT00"));
            }
            finally {Object.DestroyImmediate(instance);}
        }
        [Test]
        public void DiagnosticDoesNotCallConfiguredCredentialsVerified()
        {
            Assert.AreEqual("Configured",YuiHelpOverlay.HumanStatus("configured"));
            Assert.AreEqual("Online",YuiHelpOverlay.HumanStatus("ok"));
            Assert.AreEqual("No key",YuiHelpOverlay.HumanStatus("missing_key"));
        }
    }
}
