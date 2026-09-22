using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.Avatar;
namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiLipSyncCompatibilityTests
    {
        [Test]
        public void SharedMouthShapeKeepsStrongestVowelAndSwitchDoesNotDrivePreviousAvatar()
        {
            var first = new GameObject("First"); var second = new GameObject("Second");
            var controller = new GameObject("LipSync"); var mesh = new Mesh();
            mesh.vertices = new [] { Vector3.zero }; mesh.AddBlendShapeFrame("vrc.v_aa", 100, new [] { Vector3.forward }, new Vector3[1], new Vector3[1]);
            var renderer = first.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh;
            try
            {
                var sync = controller.AddComponent<YuiSimpleLipSync>(); sync.Configure(first, null);
                var set = typeof(YuiSimpleLipSync).GetMethod("SetVisemes", BindingFlags.Instance | BindingFlags.NonPublic);
                set.Invoke(sync, new object[] { 80f, 20f, 10f, 5f, 3f });
                Assert.AreEqual(80, renderer.GetBlendShapeWeight(0));
                sync.Configure(second, null); set.Invoke(sync, new object[] { 0f, 0f, 0f, 0f, 0f });
                Assert.AreEqual(80, renderer.GetBlendShapeWeight(0));
            }
            finally { Object.DestroyImmediate(controller); Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(mesh); }
        }
        [Test]
        public void AngryShapeIsNotUsedAsMouthA()
        {
            var root = new GameObject("Face"); var mesh = new Mesh();
            mesh.vertices = new [] { Vector3.zero }; mesh.AddBlendShapeFrame("Fcl_MTH_Angry", 100, new [] { Vector3.forward }, new Vector3[1], new Vector3[1]);
            var renderer = root.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh;
            try
            {
                var sync = root.AddComponent<YuiSimpleLipSync>(); sync.Configure(root, null);
                typeof(YuiSimpleLipSync).GetMethod("SetVisemes", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sync, new object[] { 80f, 20f, 10f, 5f, 3f });
                Assert.AreEqual(0, renderer.GetBlendShapeWeight(0));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); }
        }
    }
}
