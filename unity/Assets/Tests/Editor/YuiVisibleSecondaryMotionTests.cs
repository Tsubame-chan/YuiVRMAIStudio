using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.Avatar;
namespace YuiPhysicalAI.Tests.Editor
{
    public class YuiVisibleSecondaryMotionTests
    {
        [Test] public void NonzeroWeightsSelectOnlyActiveOutfitAndIgnoreFrustumVisibility()
        {
            var root=new GameObject("avatar");var mesh=new Mesh();
            try {
                var a=new GameObject("a");a.transform.SetParent(root.transform,false);
                var at=new GameObject("at");at.transform.SetParent(a.transform,false);at.transform.localPosition=Vector3.down*.1f;
                var b=new GameObject("b");b.transform.SetParent(root.transform,false);
                var bt=new GameObject("bt");bt.transform.SetParent(b.transform,false);bt.transform.localPosition=Vector3.down*.1f;
                var skin=new GameObject("active outfit");skin.transform.SetParent(root.transform,false);
                var renderer=skin.AddComponent<SkinnedMeshRenderer>();
                mesh.vertices=new[]{Vector3.zero};mesh.bindposes=new[]{Matrix4x4.identity,Matrix4x4.identity};
                mesh.boneWeights=new[]{new BoneWeight {boneIndex0=0,weight0=1}};
                renderer.sharedMesh=mesh;renderer.bones=new[]{a.transform,b.transform};
                var motion=root.AddComponent<YuiAvatarSpringMotion>();motion.AddChain(a.transform,.3f,0);motion.AddChain(b.transform,.3f,0);
                var selection=root.AddComponent<YuiVisibleSecondaryMotion>();selection.CaptureBindings();selection.Refresh();
                Assert.AreEqual(2,motion.JointCount);Assert.AreEqual(1,motion.ActiveJointCount,"Unused entries in renderer.bones must not keep hidden chains alive.");
                renderer.forceRenderingOff=true;selection.Refresh();Assert.AreEqual(1,motion.ActiveJointCount,"Temporary rendering suppression must not reset physics.");
                skin.SetActive(false);selection.Refresh();Assert.AreEqual(0,motion.ActiveJointCount);
                skin.SetActive(true);selection.Refresh();Assert.AreEqual(1,motion.ActiveJointCount);
                var rigid=new GameObject("rigid accessory");rigid.transform.SetParent(b.transform,false);rigid.AddComponent<MeshRenderer>();
                selection.CaptureBindings();selection.Refresh();Assert.AreEqual(2,motion.ActiveJointCount,"Rigid accessories keep their parent spring chain active.");
                rigid.SetActive(false);selection.Refresh();Assert.AreEqual(1,motion.ActiveJointCount);

            } finally {Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);}
        }
    }
}
