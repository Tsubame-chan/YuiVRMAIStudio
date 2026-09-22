using System;
using System.Collections.Generic;
using UnityEngine;
using UniVRM10;

namespace YuiPhysicalAI.Avatar
{
    /// <summary>
    /// Neutral fallback for imported humanoids without their own animation.
    /// Unity's muscle solver adapts shoulder, arm and finger rotations to the Avatar.
    /// Solve once, retain only local upper-limb rotations, then apply before VRM springs.
    /// The model's body, feet, root transform and non-humanoid bones remain untouched.
    /// Animator controllers and VRMA take precedence over this fallback.
    /// A Playables-based motion player explicitly owns/relinquishes the pose through
    /// SetExternalAnimationActive; Unity's hasBoundPlayables can remain stale after teardown.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class YuiCustomVrmIdlePose : MonoBehaviour
    {
        private Animator animator;
        private Vrm10Instance vrm;
        private UnityEngine.Avatar solvedAvatar;
        private readonly List<BoneRotation> rotations = new List<BoneRotation>();
        private bool attempted;
        private bool externalAnimationActive;

        public void SetExternalAnimationActive(bool active) => externalAnimationActive = active;

        private void LateUpdate() => ApplyNow();

        // Also used when placing an avatar, before spring particles are initialized.
        public void ApplyNow()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.enabled || !animator.isHuman || animator.avatar == null) return;
            // Never overwrite an intentionally supplied animation, even when named Idle.
            if (externalAnimationActive || animator.runtimeAnimatorController != null) return;
            if (vrm == null) vrm = GetComponentInChildren<Vrm10Instance>(true);
            if (vrm != null && vrm.Runtime.VrmAnimation != null) return;

            if (solvedAvatar != animator.avatar)
            {
                rotations.Clear(); attempted = false; solvedAvatar = animator.avatar;
            }
            if (!attempted)
            {
                attempted = true;
                BuildPose();
            }
            foreach (var bone in rotations)
                if (bone.transform != null) bone.transform.localRotation = bone.rotation;
        }

        private void BuildPose()
        {
            // SetHumanPose adjusts center of mass as arms move. Do not keep its hips
            // translation or re-solve every frame: that would move the feet.
            var transforms = animator.GetComponentsInChildren<Transform>(true);
            var original = new LocalPose[transforms.Length];
            for (var i = 0; i < transforms.Length; i++) original[i] = new LocalPose(transforms[i]);
            try
            {
                using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
                {
                    var pose = new HumanPose();
                    handler.GetHumanPose(ref pose);
                    foreach (var side in new[] { "Left", "Right" })
                    {
                        Set(ref pose, side + " Shoulder Down-Up", -.15f);
                        Set(ref pose, side + " Shoulder Front-Back", 0f);
                        Set(ref pose, side + " Arm Down-Up", -.60f);
                        // Muscle zero is not a relaxed hanging arm. A slight back
                        // rotation places the hands beside the hips, not in front.
                        Set(ref pose, side + " Arm Front-Back", .30f);
                        Set(ref pose, side + " Arm Twist In-Out", 0f);
                        Set(ref pose, side + " Forearm Stretch", .93f);
                        Set(ref pose, side + " Forearm Twist In-Out", .50f);
                        Set(ref pose, side + " Hand Down-Up", 0f);
                        Set(ref pose, side + " Hand In-Out", 0f);
                        foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                        {
                            // A resting hand is not a uniform grip: most flexion is
                            // at the knuckles; middle/distal joints remain relaxed.
                            var order = finger == "Index" ? 0 : finger == "Middle" ? 1 : finger == "Ring" ? 2 : 3;
                            Set(ref pose, side + " " + finger + " 1 Stretched", finger == "Thumb" ? -.30f : .52f - order * .035f);
                            Set(ref pose, side + " " + finger + " 2 Stretched", finger == "Thumb" ? .45f : .68f - order * .025f);
                            Set(ref pose, side + " " + finger + " 3 Stretched", finger == "Thumb" ? .70f : .80f - order * .02f);
                            Set(ref pose, side + " " + finger + " Spread", finger == "Thumb" ? -.65f : -.45f);
                        }
                    }
                    handler.SetHumanPose(ref pose);
                    for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
                    {
                        var id = (HumanBodyBones)i;
                        if (!IsUpperLimb(id)) continue;
                        var bone = animator.GetBoneTransform(id);
                        if (bone != null) rotations.Add(new BoneRotation(bone));
                    }
                }
                // Evaluate clothing clearance at the real body/root position, not
                // the temporary center-of-mass shift introduced by SetHumanPose.
                for (var i = 0; i < transforms.Length; i++) original[i].Restore(transforms[i]);
                foreach (var bone in rotations) bone.transform.localRotation = bone.rotation;
                try
                {
                    YuiHumanoidRestPoseSolver.Fit(animator);
                    rotations.Clear();
                    for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
                    {
                        var id = (HumanBodyBones)i;
                        if (!IsUpperLimb(id)) continue;
                        var bone = animator.GetBoneTransform(id);
                        if (bone != null) rotations.Add(new BoneRotation(bone));
                    }
                }
                catch (Exception ex)
                {
                    // Keep the valid Humanoid fallback if a mesh cannot be inspected.
                    Debug.LogWarning("Yui automatic rest-pose fitting: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                rotations.Clear();
                Debug.LogWarning("Yui neutral pose could not be retargeted: " + ex.Message);
            }
            finally
            {
                for (var i = 0; i < transforms.Length; i++) original[i].Restore(transforms[i]);
            }
        }

        private static void Set(ref HumanPose pose, string name, float value)
        {
            var index = Array.IndexOf(HumanTrait.MuscleName, name);
            if (index >= 0 && index < pose.muscles.Length) pose.muscles[index] = value;
        }

        private static bool IsUpperLimb(HumanBodyBones bone)
        {
            return (bone >= HumanBodyBones.LeftShoulder && bone <= HumanBodyBones.RightHand)
                || (bone >= HumanBodyBones.LeftThumbProximal && bone <= HumanBodyBones.RightLittleDistal);
        }

        private readonly struct BoneRotation
        {
            public readonly Transform transform;
            public readonly Quaternion rotation;
            public BoneRotation(Transform transform) { this.transform = transform; rotation = transform.localRotation; }
        }

        private readonly struct LocalPose
        {
            private readonly Vector3 position, scale;
            private readonly Quaternion rotation;
            public LocalPose(Transform transform) { position = transform.localPosition; rotation = transform.localRotation; scale = transform.localScale; }
            public void Restore(Transform transform) { transform.localPosition = position; transform.localRotation = rotation; transform.localScale = scale; }
        }
    }
}
