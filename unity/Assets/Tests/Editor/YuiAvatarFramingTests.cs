using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests
{
    public class YuiAvatarFramingTests
    {
        [TestCase(.55f, .5f)] [TestCase(1f, 1.6f)] [TestCase(1.8f, 2.4f)]
        public void FaceRemainsInUpperQuarterAcrossShapes(float aspect, float height)
        {
            var go = new GameObject("framing-test", typeof(Camera));
            try
            {
                var camera = go.GetComponent<Camera>();
                camera.aspect = aspect; camera.fieldOfView = 27f;
                var face = new Vector3(4f, height + 3f, -2f);
                camera.transform.position = YuiAvatarFraming.CameraPosition(face, height, camera.fieldOfView);
                camera.transform.rotation = YuiAvatarFraming.CameraRotation;
                Assert.That(camera.transform.position.y, Is.GreaterThan(face.y));
                var viewport = camera.WorldToViewportPoint(face);
                Assert.That(viewport.x, Is.EqualTo(.5f).Within(.001f));
                Assert.That(viewport.y, Is.EqualTo(.75f).Within(.001f));
                Assert.That(viewport.z, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
