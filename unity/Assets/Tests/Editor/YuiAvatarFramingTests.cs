using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests
{
    public class YuiAvatarFramingTests
    {
        [TestCase(.46f)] [TestCase(1.7f)]
        public void ViewerFitsHeadFeetAndWideClothing(float aspect)
        {
            var cameraObject = new GameObject("camera", typeof(Camera));
            var root = new GameObject("avatar", typeof(MeshFilter), typeof(MeshRenderer));
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[] {new Vector3(-.7f, -1, -.2f), new Vector3(.7f, -1, 0), new Vector3(0, 1.2f, .3f)};
                mesh.triangles = new[] {0,1,2}; root.GetComponent<MeshFilter>().sharedMesh = mesh;
                root.transform.position = new Vector3(3,2,4);
                var camera = cameraObject.GetComponent<Camera>(); camera.aspect = aspect; camera.fieldOfView = 22;
                camera.transform.rotation = Quaternion.Euler(12,15,0);
                var distance = YuiAvatarFraming.RequiredOrbitDistance(root.transform, root.transform.position,
                    camera.transform.rotation, camera.fieldOfView, aspect, .06f, .91f);
                camera.transform.position = root.transform.position - camera.transform.forward * distance;
                foreach (var vertex in mesh.vertices)
                {
                    var v = camera.WorldToViewportPoint(root.transform.TransformPoint(vertex));
                    Assert.That(v.y, Is.InRange(.059f,.911f)); Assert.That(v.x, Is.InRange(.039f,.961f));
                }
            }
            finally {Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(mesh);}
        }
        [TestCase(.5f)] [TestCase(1.25f)] [TestCase(2f)]
        public void SkinnedAccessoryPreservesWorldScaleDuringFraming(float scale)
        {
            var cameraObject = new GameObject("camera", typeof(Camera));
            var root = new GameObject("scaled avatar", typeof(SkinnedMeshRenderer));
            var mesh = new Mesh();
            try
            {
                var camera = cameraObject.GetComponent<Camera>(); camera.fieldOfView = 25;
                root.transform.position = new Vector3(0, 0, 3);
                root.transform.localScale = Vector3.one * scale;
                var bone = new GameObject("hat bone").transform; bone.SetParent(root.transform, false);
                mesh.vertices = new[] {new Vector3(-.1f, 1, 0), new Vector3(.1f, 1, 0), new Vector3(0, 1.2f, 0)};
                mesh.triangles = new[] {0, 1, 2};
                mesh.bindposes = new[] {Matrix4x4.identity};
                var weight = new BoneWeight {boneIndex0 = 0, weight0 = 1};
                mesh.boneWeights = new[] {weight, weight, weight};
                var skin = root.GetComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh; skin.bones = new[] {bone}; skin.rootBone = bone;
                YuiAvatarFraming.FitVisibleHeadroom(camera, root.transform, .92f);
                var expectedTop = root.transform.TransformPoint(mesh.vertices[2]);
                Assert.That(camera.WorldToViewportPoint(expectedTop).y, Is.LessThanOrEqualTo(.921f));
                if (scale >= 1) Assert.That(camera.WorldToViewportPoint(expectedTop).y, Is.EqualTo(.92f).Within(.001f));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(mesh); }
        }
        [Test]
        public void SeparateTallAccessoryFitsSafeTopWithoutUsingInflatedBounds()
        {
            var cameraObject = new GameObject("camera", typeof(Camera));
            var root = new GameObject("avatar");
            var mesh = new Mesh();
            try
            {
                var camera = cameraObject.GetComponent<Camera>(); camera.fieldOfView = 25;
                root.transform.position = new Vector3(0, 0, 3);
                var accessory = new GameObject("separate hat", typeof(MeshFilter), typeof(MeshRenderer));
                accessory.transform.SetParent(root.transform, false);
                mesh.vertices = new[] {new Vector3(-.1f, 1, 0), new Vector3(.1f, 1, 0), new Vector3(0, 1.2f, 0)};
                mesh.triangles = new[] {0, 1, 2};
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100);
                accessory.GetComponent<MeshFilter>().sharedMesh = mesh;
                YuiAvatarFraming.FitVisibleHeadroom(camera, root.transform, .92f);
                Assert.That(camera.WorldToViewportPoint(accessory.transform.TransformPoint(mesh.vertices[2])).y, Is.EqualTo(.92f).Within(.001f));
                var position = camera.transform.position;
                YuiAvatarFraming.FitVisibleHeadroom(camera, root.transform, .92f);
                Assert.That(Vector3.Distance(position, camera.transform.position), Is.LessThan(.001f));
                Assert.That(position.y, Is.LessThan(1f));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(mesh); }
        }
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
