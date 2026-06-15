using UnityEngine;

namespace YuiPhysicalAI.Avatar
{
    /// <summary>
    /// Applies a natural conversational idle pose to custom runtime-imported VRM avatars
    /// by directly rotating arm bones in world space each LateUpdate.
    ///
    /// Approach: direct bone rotation via GetBoneTransform + Quaternion.FromToRotation.
    /// No HumanPoseHandler, no SetHumanPose, no AnimatorController, no root transform edits.
    /// Must run every LateUpdate because Unity's Humanoid Animator (with no controller)
    /// resets all bones to T-pose during its evaluation phase every frame.
    ///
    /// Finger bones are intentionally NOT manipulated. Generic per-finger axis detection
    /// (rotating toward an estimated palm normal) proved brittle across VRM avatars:
    /// the ChooseCurlAxis heuristic selected lateral axes on some rigs, bending fingers
    /// sideways instead of forward, producing an L-shape / thumbs-up silhouette.
    /// Straight T-pose fingers following the corrected arm is less interesting but is
    /// stable and does not distort the hand shape. Proper hand animation is deferred to
    /// a future version that uses a tested Humanoid idle clip or VRMA asset.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class YuiCustomVrmIdlePose : MonoBehaviour
    {
        // Upper arm: how far outward from the body centerline (fraction of normalized vector).
        // 0.0 = straight down, 0.707 = 45 degrees.
        private const float UpperArmOutward = 0.28f;
        // Upper arm: slight forward lean so the silhouette is not robotically flat.
        private const float UpperArmForward = 0.04f;
        // Upper arm: downward weight (normalized with the values above).
        private const float UpperArmDown = 0.955f;

        // Lower arm: nudge the forearm slightly more inward and forward than the upper arm
        // to create a gentle, natural-looking elbow curve.
        private const float LowerArmOutward = 0.11f;
        private const float LowerArmForward = 0.18f;
        private const float LowerArmDown = 0.975f;

        private Animator _animator;
        private bool _initialized;

        private void OnDisable()
        {
            _initialized = false;
            _animator = null;
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                _initialized = TryInitialize();
            }

            if (_initialized)
            {
                ApplyArmPose();
            }
        }

        private bool TryInitialize()
        {
            _animator = GetComponentInChildren<Animator>(true);
            if (_animator == null)
            {
                return false;
            }

            _animator.applyRootMotion = false;
            return true;
        }

        private void ApplyArmPose()
        {
            var leftUpper = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var leftLower = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var leftHand  = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var rightUpper = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var rightLower = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var rightHand  = _animator.GetBoneTransform(HumanBodyBones.RightHand);

            if (leftUpper == null || leftLower == null || rightUpper == null || rightLower == null)
            {
                return;
            }

            // Build avatar-local axes from the Animator root so directions stay correct
            // regardless of the avatar parent or scene root orientation.
            var avatarRight   = _animator.transform.right;
            var avatarForward = _animator.transform.forward;

            // --- Left upper arm ---
            // Rotate from T-pose (horizontal-outward) to mostly-down with small outward + forward lean.
            var leftUpperTarget = (-avatarRight * UpperArmOutward
                                   + Vector3.down  * UpperArmDown
                                   + avatarForward * UpperArmForward).normalized;
            var leftUpperCurrent = (leftLower.position - leftUpper.position).normalized;
            if (leftUpperCurrent.sqrMagnitude > 0.001f)
            {
                leftUpper.rotation = Quaternion.FromToRotation(leftUpperCurrent, leftUpperTarget)
                                     * leftUpper.rotation;
            }

            // --- Right upper arm ---
            var rightUpperTarget = (avatarRight  * UpperArmOutward
                                    + Vector3.down  * UpperArmDown
                                    + avatarForward * UpperArmForward).normalized;
            var rightUpperCurrent = (rightLower.position - rightUpper.position).normalized;
            if (rightUpperCurrent.sqrMagnitude > 0.001f)
            {
                rightUpper.rotation = Quaternion.FromToRotation(rightUpperCurrent, rightUpperTarget)
                                      * rightUpper.rotation;
            }

            // --- Left lower arm (forearm) ---
            // After upper arm rotation the forearm follows its parent and already points roughly
            // in the upper-arm target direction (no elbow bend yet). We read the updated forearm
            // direction and nudge it to be slightly more vertical and slightly more forward than
            // the upper arm — this is what creates the gentle elbow curve.
            var leftLowerRef     = leftHand != null ? leftHand.position : leftLower.position + leftLower.right * -0.01f;
            var leftLowerCurrent = (leftLowerRef - leftLower.position).normalized;
            if (leftLowerCurrent.sqrMagnitude > 0.001f)
            {
                var leftLowerTarget = (-avatarRight * LowerArmOutward
                                       + Vector3.down  * LowerArmDown
                                       + avatarForward * LowerArmForward).normalized;
                leftLower.rotation = Quaternion.FromToRotation(leftLowerCurrent, leftLowerTarget)
                                     * leftLower.rotation;
            }

            // --- Right lower arm (forearm) ---
            var rightLowerRef     = rightHand != null ? rightHand.position : rightLower.position + rightLower.right * 0.01f;
            var rightLowerCurrent = (rightLowerRef - rightLower.position).normalized;
            if (rightLowerCurrent.sqrMagnitude > 0.001f)
            {
                var rightLowerTarget = (avatarRight  * LowerArmOutward
                                        + Vector3.down  * LowerArmDown
                                        + avatarForward * LowerArmForward).normalized;
                rightLower.rotation = Quaternion.FromToRotation(rightLowerCurrent, rightLowerTarget)
                                      * rightLower.rotation;
            }

            // Finger bones are deliberately left in their Animator T-pose state.
            // See class summary for the reason.
        }
    }
}
