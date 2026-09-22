using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YuiPhysicalAI.Avatar;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiNeutralPoseTests
    {
        private static readonly MethodInfo Apply = typeof(YuiCustomVrmIdlePose).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        [TestCase(1f, 0f)]
        [TestCase(.5f, 180f)]
        [TestCase(1.8f, 90f)]
        public void RetargetedPosePreservesBodyAndFeetAcrossTransformedParents(float scale, float yaw)
        {
            var parent=new GameObject("Parent");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnityChan/Prefabs/unitychan.prefab");
            var root=Object.Instantiate(prefab,parent.transform);
            try
            {
                parent.transform.position=new Vector3(3,2,-4);
                parent.transform.rotation=Quaternion.Euler(0,yaw,0);parent.transform.localScale=Vector3.one*scale;
                var animator=root.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.Rebind();animator.Update(0);
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
                var foot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
                var hipsBefore=hips.position;var footBefore=foot.position;var rootBefore=root.transform.localPosition;
                var idle=root.AddComponent<YuiCustomVrmIdlePose>();
                Apply.Invoke(idle,null);
                Assert.Less(hand.position.y,animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.y-.2f*scale);
                foreach (var left in new[] { true, false })
                {
                    var wrist = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    var knuckle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                    var forearm = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                    Assert.Greater(Vector3.Dot((knuckle.position - wrist.position).normalized, (wrist.position - forearm.position).normalized), .9f, "Wrist should follow forearm, not bend sideways");
                    var index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
                    var indexNext = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexIntermediate : HumanBodyBones.RightIndexIntermediate);
                    var indexLast = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexDistal : HumanBodyBones.RightIndexDistal);
                    var little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
                    var along = (knuckle.position - wrist.position).normalized;
                    var side = Mathf.Sign(root.transform.InverseTransformPoint(wrist.position).x - root.transform.InverseTransformPoint(hips.position).x);
                    var palm = Vector3.Cross(index.position-little.position, along).normalized * -side;
                    Assert.That(Vector3.Dot((indexNext.position-index.position).normalized,palm), Is.InRange(.20f,.32f), "Index knuckle has a gentle inward curl, not a flat hand or fist");
                    Assert.That(Vector3.Dot((indexLast.position-indexNext.position).normalized,palm), Is.InRange(.51f,.63f), "Second phalanx follows a relaxed curl");
                    var thumb = animator.GetBoneTransform(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
                    var thumbNext = animator.GetBoneTransform(left ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.RightThumbIntermediate);
                    Assert.Greater(Vector3.Dot((thumbNext.position - thumb.position).normalized, (knuckle.position - wrist.position).normalized), .25f, "Thumb should not remain splayed perpendicular to the fingers");
                }
                var handAfter=hand.localRotation;
                for(var frame=0;frame<300;frame++) Apply.Invoke(idle,null);
                Assert.Less(Vector3.Distance(hipsBefore,hips.position),.0001f);
                Assert.Less(Vector3.Distance(footBefore,foot.position),.0001f);
                Assert.AreEqual(rootBefore,root.transform.localPosition);
                Assert.Less(Quaternion.Angle(handAfter,hand.localRotation),.01f);
            }
            finally{Object.DestroyImmediate(parent);}
        }

        [Test]
        public void AuthoredControllerOwnsThePose()
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnityChan/Prefabs/unitychan.prefab"));
            try
            {
                var animator=root.GetComponent<Animator>();var controller=animator.runtimeAnimatorController;
                var hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);var rotation=Quaternion.Euler(11,22,33);hand.localRotation=rotation;
                Apply.Invoke(root.AddComponent<YuiCustomVrmIdlePose>(),null);
                Assert.AreSame(controller,animator.runtimeAnimatorController);
                Assert.Less(Quaternion.Angle(rotation,hand.localRotation),.01f);
            }
            finally{Object.DestroyImmediate(root);}
        }
        [TestCase(1f, 0f)]
        [TestCase(.6f, 180f)]
        [TestCase(1.7f, 90f)]
        public void FittingUsesVisibleClothingButIgnoresHairAndHiddenOutfits(float scale, float yaw)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnityChan/Prefabs/unitychan.prefab"));
            var mesh = new Mesh();
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(3, 2, -4), Quaternion.Euler(0, yaw, 0));
                root.transform.localScale = Vector3.one * scale;
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.enabled = false;
                var animator = root.GetComponent<Animator>(); animator.runtimeAnimatorController = null; animator.Rebind(); animator.Update(0);
                root.AddComponent<YuiCustomVrmIdlePose>().ApplyNow();
                var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                var upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                var lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var before = root.transform.InverseTransformPoint(hand.position);
                var shoulder = root.transform.InverseTransformPoint(upper.position);
                var side = Mathf.Sign(shoulder.x - root.transform.InverseTransformPoint(hips.position).x);
                var armLength = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
                var localLength = armLength / scale;
                var edge = before.x + side * localLength * .20f;
                mesh.vertices = new[] { new Vector3(edge, before.y - .1f, before.z), new Vector3(edge, before.y + .2f, before.z), new Vector3(edge, before.y, before.z + .01f) };
                mesh.triangles = new[] { 0, 1, 2 };
                mesh.boneWeights = new[] { new BoneWeight { boneIndex0 = 0, weight0 = 1 }, new BoneWeight { boneIndex0 = 0, weight0 = 1 }, new BoneWeight { boneIndex0 = 0, weight0 = 1 } };
                var garment = new GameObject("Garment").AddComponent<SkinnedMeshRenderer>(); garment.transform.SetParent(root.transform, false); garment.sharedMesh = mesh;
                // A visible hair/accessory at the very same location is not body clearance.
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                garment.bones = new[] { head }; mesh.bindposes = new[] { head.worldToLocalMatrix * root.transform.localToWorldMatrix };
                YuiHumanoidRestPoseSolver.Fit(animator);
                Assert.Less(Vector3.Distance(before, root.transform.InverseTransformPoint(hand.position)), .001f);
                garment.bones = new[] { hips }; mesh.bindposes = new[] { hips.worldToLocalMatrix * root.transform.localToWorldMatrix };
                garment.enabled = false;
                YuiHumanoidRestPoseSolver.Fit(animator);
                Assert.Less(Vector3.Distance(before, root.transform.InverseTransformPoint(hand.position)), .001f);
                garment.enabled = true;
                YuiHumanoidRestPoseSolver.Fit(animator);
                var after = root.transform.InverseTransformPoint(hand.position);
                Assert.Greater(Vector3.Distance(after, before), .04f * localLength, "Visible garment must change the hand position");
                Assert.Less(Mathf.Abs(after.x - shoulder.x), .35f * localLength, "Garment clearance must not spread the arms excessively");
                Assert.AreEqual(armLength, Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position), .0001f);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void UnreachableIkTargetDoesNotStretchOrMoveTheBody()
        {
            var upper = new GameObject("Upper").transform;
            var lower = new GameObject("Lower").transform; lower.SetParent(upper); lower.localPosition = Vector3.down * .3f;
            var hand = new GameObject("Hand").transform; hand.SetParent(lower); hand.localPosition = new Vector3(0, -.28f, .03f);
            try
            {
                var a = lower.localPosition; var b = hand.localPosition; var rotation = hand.rotation;
                YuiHumanoidRestPoseSolver.SolveTwoBone(upper, lower, hand, new Vector3(4, -4, 2), Vector3.back);
                Assert.AreEqual(Vector3.zero, upper.position);
                Assert.AreEqual(a, lower.localPosition); Assert.AreEqual(b, hand.localPosition);
                Assert.Less(Quaternion.Angle(rotation, hand.rotation), .001f);
                Assert.Less(Vector3.Distance(upper.position, hand.position), a.magnitude + b.magnitude + .0001f);
            }
            finally { Object.DestroyImmediate(upper.gameObject); }
        }
    }
}
