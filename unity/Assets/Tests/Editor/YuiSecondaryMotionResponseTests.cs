using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.Avatar;

namespace YuiPhysicalAI.Tests.Editor
{
    public class YuiSecondaryMotionResponseTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ExplicitlyConfiguredLongChainRespondsAboveTheTipsAndSettlesAfterTurning(int fps)
        {
            var root = new GameObject("avatar");
            try
            {
                var bones = new Transform[5];
                for (var i = 0; i < bones.Length; i++)
                {
                    bones[i] = new GameObject("joint " + i).transform;
                    bones[i].SetParent(i == 0 ? root.transform : bones[i-1], false);
                    bones[i].localPosition = i == 0 ? new Vector3(.1f, 1.5f, .08f) : new Vector3(.02f, -.12f, .025f);
                }
                // Use the same serialized rig path as directly imported Unity avatars.
                var rig = root.AddComponent<YuiSecondaryMotionRig>();
                rig.MaxAngleDegrees = 20; // Diagnostic fixture, not the shipped preset.
                rig.Chains = new[] { new YuiSecondaryMotionRig.Chain { Root = bones[0], Pull = .05f } };
                // AddComponent invokes Awake with an empty rig; recreate its runtime driver.
                Object.DestroyImmediate(root.GetComponent<YuiAvatarSpringMotion>());
                rig.Initialize();
                var motion = root.GetComponent<YuiAvatarSpringMotion>();
                var peaks = new float[4];
                for (var frame = 0; frame < fps * 4; frame++)
                {
                    root.transform.rotation = Quaternion.Euler(0, Mathf.Sin(frame / (float)fps * Mathf.PI) * 120, 0);
                    motion.Step(1f / fps);
                    for (var i = 0; i < peaks.Length; i++)
                    {
                        var angle = Quaternion.Angle(Quaternion.identity, bones[i].localRotation);
                        peaks[i] = Mathf.Max(peaks[i], angle);
                        Assert.That(angle, Is.LessThanOrEqualTo(20.05f));
                        Assert.That(Vector3.Distance(bones[i].position, bones[i+1].position),
                            Is.EqualTo(bones[i+1].localPosition.magnitude).Within(.0001f));
                    }
                }
                Assert.That(peaks[0], Is.GreaterThan(3.5f), "The root must bend, not only the tips.");
                Assert.That(peaks[1], Is.GreaterThan(3.5f), "The middle must respond to rotation.");
                for (var frame = 0; frame < fps * 10; frame++) motion.Step(1f / fps);
                for (var i = 0; i < peaks.Length; i++)
                    Assert.That(Quaternion.Angle(Quaternion.identity, bones[i].localRotation), Is.LessThan(.1f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
