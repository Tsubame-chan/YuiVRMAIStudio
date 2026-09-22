using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.Avatar;
namespace YuiPhysicalAI.Tests
{
    public class YuiBlinkBaselineTests
    {
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
