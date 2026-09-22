using System;
using System.Collections.Generic;
using UnityEngine;

namespace YuiPhysicalAI.Avatar
{
    // A bounded secondary-motion approximation for Bridge avatars, not VRC PhysBone.
    // It preserves bone lengths and never drives humanoid body bones. VRM keeps UniVRM physics.
    [DefaultExecutionOrder(11000)]
    public sealed class YuiAvatarSpringMotion : MonoBehaviour
    {
        private sealed class Joint
        {
            public Transform Bone;
            public Quaternion Rest;
            public Vector3 Axis, Tail, Previous;
            public float Pull, Gravity;
        }
        private readonly List<Joint> joints = new List<Joint>();
        private readonly HashSet<Transform> registered = new HashSet<Transform>();
        private readonly HashSet<Transform> body = new HashSet<Transform>();
        private Vector3 lastPosition;
        public int JointCount => joints.Count;

        public void InitializeBodyExclusions(Animator animator)
        {
            if (animator == null || !animator.isHuman) return;
            for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = animator.GetBoneTransform((HumanBodyBones)i);
                if (bone != null) body.Add(bone);
            }
        }

        public void AddChain(Transform root, float pull, float gravity, ISet<Transform> excluded = null)
        {
            if (root == null || joints.Count >= 256) return;
            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
            {
                var ignored = false;
                for (var ancestor = bone; ancestor != null && ancestor != root.parent; ancestor = ancestor.parent)
                    if (excluded != null && excluded.Contains(ancestor)) { ignored = true; break; }
                if (ignored || body.Contains(bone) || registered.Contains(bone) || joints.Count >= 256) continue;
                Transform tip = null;
                for (var i = 0; i < bone.childCount; i++)
                {
                    var child = bone.GetChild(i);
                    if (excluded != null && excluded.Contains(child)) continue;
                    if (tip == null || child.localPosition.sqrMagnitude > tip.localPosition.sqrMagnitude) tip = child;
                }
                if (tip == null || tip.localPosition.sqrMagnitude < .000001f) continue;
                registered.Add(bone);
                joints.Add(new Joint { Bone = bone, Rest = bone.localRotation, Axis = tip.localPosition,
                    Pull = Mathf.Clamp01(float.IsNaN(pull) || float.IsInfinity(pull) ? .3f : pull),
                    Gravity = Mathf.Clamp(float.IsNaN(gravity) || float.IsInfinity(gravity) ? 0 : gravity, -1, 1) });
            }
            ResetMotion();
        }

        public void ResetMotion()
        {
            foreach (var joint in joints)
            {
                if (joint.Bone == null) continue;
                joint.Bone.localRotation = joint.Rest;
                joint.Tail = joint.Previous = joint.Bone.TransformPoint(joint.Axis);
            }
            lastPosition = transform.position;
        }
        private void OnEnable() { ResetMotion(); }
        private void OnDisable() { ResetMotion(); }
        private void LateUpdate() { Step(Time.deltaTime); }

        public void Step(float deltaTime)
        {
            if (deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            if (deltaTime > .2f || (transform.position - lastPosition).sqrMagnitude > 1f) { ResetMotion(); return; }
            lastPosition = transform.position;
            var dt = Mathf.Min(deltaTime, 1f / 30f);
            foreach (var joint in joints) if (joint.Bone != null) joint.Bone.localRotation = joint.Rest;
            foreach (var joint in joints)
            {
                if (joint.Bone == null) continue;
                var origin = joint.Bone.position;
                var restVector = joint.Bone.TransformVector(joint.Axis);
                var length = restVector.magnitude;
                if (length < .0001f) continue;
                var target = origin + restVector;
                var velocity = (joint.Tail - joint.Previous) * Mathf.Pow(.75f, dt * 60f);
                var next = joint.Tail + velocity + (target - joint.Tail) * (1f - Mathf.Exp(-(5f + 25f * joint.Pull) * dt))
                    + Vector3.down * (joint.Gravity * length * 25f * dt * dt);
                var direction = next - origin;
                if (direction.sqrMagnitude < .000001f) direction = restVector;
                direction = Vector3.RotateTowards(restVector.normalized, direction.normalized, Mathf.PI / 4f, 0f);
                joint.Previous = joint.Tail;
                joint.Tail = origin + direction * length;
                joint.Bone.rotation = Quaternion.FromToRotation(restVector, direction) * joint.Bone.rotation;
            }
        }
    }
}
