using System.Collections.Generic;
using UnityEngine;

namespace YuiPhysicalAI.Avatar
{
    /// <summary>
    /// One-time fitting of the fallback resting pose. Infer body/garment clearance
    /// from skinned vertices weighted to the torso/legs, excluding arms/head/hair.
    /// Solve hand targets using measured arm lengths; do not scale or translate bones.
    /// </summary>
    public static class YuiHumanoidRestPoseSolver
    {
        private const int MaxSamplesPerMesh = 30000;
        public static void Fit(Animator animator)
        {
            if (animator == null || !animator.isHuman) return;
            var root = animator.transform;
            var human = new Dictionary<Transform, HumanBodyBones>();
            for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = animator.GetBoneTransform((HumanBodyBones)i);
                if (bone != null) human[bone] = (HumanBodyBones)i;
            }
            var samples = CollectBodySurface(animator, human);
            FitArm(animator, root, true, samples);
            FitArm(animator, root, false, samples);
            if (Application.productName.EndsWith("Validation"))
            {
                var wrist = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Debug.Log("Yui rest fit: surface=" + samples.Count + " scale=" + root.lossyScale
                    + " leftWrist=" + (wrist != null ? root.InverseTransformPoint(wrist.position).ToString() : "unmapped"));
            }
        }

        private static List<Vector3> CollectBodySurface(Animator animator, Dictionary<Transform, HumanBodyBones> human)
        {
            var result = new List<Vector3>();
            foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.enabled || renderer.sharedMesh == null || !renderer.sharedMesh.isReadable) continue;
                var visible = true;
                for (var node = renderer.transform; node != animator.transform && node != null; node = node.parent)
                    if (!node.gameObject.activeSelf) { visible = false; break; }
                if (!visible) continue;
                var bones = renderer.bones;
                var body = new bool[bones.Length];
                for (var i = 0; i < bones.Length; i++)
                {
                    for (var bone = bones[i]; bone != null; bone = bone.parent)
                    {
                        if (!human.TryGetValue(bone, out var id)) continue;
                        body[i] = id == HumanBodyBones.Hips || id == HumanBodyBones.Spine || id == HumanBodyBones.Chest
                            || id == HumanBodyBones.UpperChest || (id >= HumanBodyBones.LeftUpperLeg && id <= HumanBodyBones.RightFoot)
                            || id == HumanBodyBones.LeftToes || id == HumanBodyBones.RightToes;
                        break;
                    }
                }
                var mesh = new Mesh();
                try
                {
                    // true compensates renderer scale. Without it, parent scale
                    // is applied twice when the baked points return to world space.
                    renderer.BakeMesh(mesh, true);
                    var vertices = mesh.vertices;
                    var weights = renderer.sharedMesh.boneWeights;
                    if (weights.Length != vertices.Length) continue;
                    var stride = Mathf.Max(1, Mathf.CeilToInt(vertices.Length / (float)MaxSamplesPerMesh));
                    for (var i = 0; i < vertices.Length; i += stride)
                    {
                        var w = weights[i];
                        var bodyWeight = Weight(body, w.boneIndex0, w.weight0) + Weight(body, w.boneIndex1, w.weight1)
                            + Weight(body, w.boneIndex2, w.weight2) + Weight(body, w.boneIndex3, w.weight3);
                        if (bodyWeight < .5f) continue;
                        result.Add(animator.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertices[i])));
                    }
                }
                finally { if (Application.isPlaying) UnityEngine.Object.Destroy(mesh); else UnityEngine.Object.DestroyImmediate(mesh); }
            }
            return result;
        }

        private static float Weight(bool[] body, int index, float weight) => index >= 0 && index < body.Length && body[index] ? weight : 0f;

        private static void FitArm(Animator animator, Transform root, bool left, List<Vector3> surface)
        {
            var upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (upper == null || lower == null || hand == null || hips == null) return;
            var shoulder = root.InverseTransformPoint(upper.position);
            var hip = root.InverseTransformPoint(hips.position);
            var upperLength = root.InverseTransformVector(lower.position - upper.position).magnitude;
            var lowerLength = root.InverseTransformVector(hand.position - lower.position).magnitude;
            var length = upperLength + lowerLength;
            if (length < .01f) return;
            var side = shoulder.x >= hip.x ? 1f : -1f;
            var finger = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleDistal : HumanBodyBones.RightMiddleDistal);
            var handLength = finger != null ? root.InverseTransformVector(finger.position - hand.position).magnitude : length * .22f;
            handLength = Mathf.Clamp(handLength, length * .12f, length * .35f);
            OrientRestingHand(animator, root, left, side, lower, hand);
            var wrist = root.InverseTransformPoint(hand.position);
            var handSamples = new List<Vector3> { Vector3.zero };
            foreach (var id in left
                ? new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftLittleDistal }
                : new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightLittleProximal, HumanBodyBones.RightIndexDistal, HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightLittleDistal })
            {
                var bone = animator.GetBoneTransform(id);
                if (bone != null) handSamples.Add(root.InverseTransformPoint(bone.position) - wrist);
            }
            if (handSamples.Count == 1) handSamples.Add(Vector3.down * handLength);

            // Resting arms first, clearance second. Test the actual palm/finger
            // positions at their own height/depth, not the widest skirt hem plus
            // a palm-sized buffer. Allow a small forward shift as well as abduction.
            // Keep the upper arm near vertical. Clothing clearance is taken up
            // mostly by the elbow/forearm, rather than opening the entire shoulder.
            var elbowDirection = new Vector3(side * .14f, -1f, .04f).normalized;
            var naturalElbow = shoulder + elbowDirection * upperLength;
            var natural = new Vector3(shoulder.x + side * length * .045f, 0, hip.z + length * .025f);
            var envelope = new SurfaceEnvelope(surface, side, length * .035f,
                shoulder.y - length - handLength * 1.3f, shoulder.y - length * .7f,
                natural.z - handLength, natural.z + length * .2f + handLength);
            var best = natural;
            var bestCost = float.PositiveInfinity;
            for (var outStep = 0; outStep <= 10; outStep++)
            for (var frontStep = 0; frontStep <= 7; frontStep++)
            {
                var outward = length * .022f * outStep;
                var forward = length * .025f * frontStep;
                var target = natural + new Vector3(side * outward, 0, forward);
                var dx = target.x - naturalElbow.x; var dz = target.z - naturalElbow.z;
                var lowerReach = lowerLength * .995f;
                if (dx * dx + dz * dz >= lowerReach * lowerReach * .92f) continue;
                target.y = naturalElbow.y - Mathf.Sqrt(lowerReach * lowerReach - dx * dx - dz * dz);
                var penetration = 0f;
                foreach (var offset in handSamples)
                {
                    var point = target + offset;
                    penetration = Mathf.Max(penetration, envelope.Edge(point.y, point.z) + handLength * .06f - side * point.x);
                }
                // Displacement has a cost even when collision-free. Huge clothing
                // cannot force an exaggerated pose: the search is bounded (~17°).
                var cost = outward * outward + forward * forward * 1.25f + penetration * penetration * 100f;
                if (cost < bestCost) { bestCost = cost; best = target; }
            }
            if (!float.IsPositiveInfinity(bestCost))
            {
                SolveTwoBone(upper, lower, hand, root.TransformPoint(best), root.TransformDirection(elbowDirection));
                OrientRestingHand(animator, root, left, side, lower, hand);
            }
        }

        private static void OrientRestingHand(Animator animator, Transform root, bool left, float side, Transform lower, Transform hand)
        {
            var middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            var index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            if (middle == null || index == null || little == null) return;
            // Infer hand axes from the fingers instead of assuming a bone's local
            // XYZ convention. Relax the wrist with the forearm, palm toward the body.
            var direction = Vector3.Slerp((hand.position - lower.position).normalized, -root.up, .50f).normalized;
            var handAxis = middle.position - hand.position;
            if (handAxis.sqrMagnitude < .000001f) return;
            hand.rotation = Quaternion.FromToRotation(handAxis, direction) * hand.rotation;
            var palm = Vector3.Cross(index.position - little.position, middle.position - hand.position) * -side;
            palm = Vector3.ProjectOnPlane(palm, direction);
            var inward = Vector3.ProjectOnPlane(-side * root.right + root.forward * .12f, direction);
            if (palm.sqrMagnitude > .000001f && inward.sqrMagnitude > .000001f)
                hand.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(palm, inward, direction), direction) * hand.rotation;

            // Muscle ranges differ among imported Humanoid rigs. Set a small,
            // anatomical curl from the actual phalanges rather than treating the
            // same normalized muscle number as the same finger angle.
            var along = (middle.position - hand.position).normalized;
            var acrossPalm = Vector3.ProjectOnPlane(index.position - little.position, along).normalized;
            var intoPalm = Vector3.Cross(acrossPalm, along).normalized * -side;
            var fingers = left
                ? new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftLittleProximal }
                : new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal };
            for (var f = 0; f < fingers.Length; f++)
            {
                var first = animator.GetBoneTransform(fingers[f]);
                var second = animator.GetBoneTransform(fingers[f] + 1);
                var third = animator.GetBoneTransform(fingers[f] + 2);
                if (first == null || second == null || third == null) continue;
                var curl = (15f + f * 3f) * Mathf.Deg2Rad;
                AimBone(first, second, along * Mathf.Cos(curl) + intoPalm * Mathf.Sin(curl));
                curl += (20f + f * 2f) * Mathf.Deg2Rad;
                AimBone(second, third, along * Mathf.Cos(curl) + intoPalm * Mathf.Sin(curl));
                // Keep the retargeted distal curl: VRM often has no fingertip
                // transform, so its direction cannot be inferred reliably.
            }

            // Thumb muscle limits can leave an imported thumb almost perpendicular
            // to the fingers. Adduct its base using anatomical landmarks, with a
            // bounded change; do not assume the metacarpal's local rotation axis.
            var thumb = animator.GetBoneTransform(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            var thumbNext = animator.GetBoneTransform(left ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.RightThumbIntermediate);
            if (thumb != null && thumbNext != null)
            {
                var across = (index.position - little.position).normalized;
                var palmIn = Vector3.ProjectOnPlane(inward, direction).normalized;
                var desiredThumb = (direction * .94f + across * .30f + palmIn * .12f).normalized;
                var thumbAxis = thumbNext.position - thumb.position;
                if (thumbAxis.sqrMagnitude > .000001f)
                {
                    var correction = Quaternion.FromToRotation(thumbAxis, desiredThumb);
                    thumb.rotation = Quaternion.RotateTowards(Quaternion.identity, correction, 60f) * thumb.rotation;
                }
            }
        }

        private static void AimBone(Transform bone, Transform child, Vector3 direction)
        {
            var axis = child.position - bone.position;
            if (axis.sqrMagnitude > .000001f && direction.sqrMagnitude > .000001f)
                bone.rotation = Quaternion.FromToRotation(axis, direction) * bone.rotation;
        }

        // A small one-time height/depth lookup keeps the candidate search cheap.
        // Adjacent cells bridge vertex sampling gaps. This is a conservative
        // surface approximation, not a per-frame collision/cloth simulation.
        private sealed class SurfaceEnvelope
        {
            private readonly float[,] edges;
            private readonly float minY, minZ, cell;
            public SurfaceEnvelope(List<Vector3> surface, float side, float cell, float minY, float maxY, float minZ, float maxZ)
            {
                this.minY = minY; this.minZ = minZ; this.cell = cell;
                edges = new float[Mathf.CeilToInt((maxY - minY) / cell) + 1, Mathf.CeilToInt((maxZ - minZ) / cell) + 1];
                for (var y = 0; y < edges.GetLength(0); y++)
                for (var z = 0; z < edges.GetLength(1); z++) edges[y, z] = float.NegativeInfinity;
                foreach (var point in surface)
                {
                    var cy = Mathf.RoundToInt((point.y - minY) / cell); var cz = Mathf.RoundToInt((point.z - minZ) / cell);
                    for (var y = cy - 1; y <= cy + 1; y++)
                    for (var z = cz - 1; z <= cz + 1; z++)
                        if (y >= 0 && z >= 0 && y < edges.GetLength(0) && z < edges.GetLength(1))
                            edges[y, z] = Mathf.Max(edges[y, z], side * point.x);
                }
            }
            public float Edge(float y, float z)
            {
                var row = Mathf.RoundToInt((y - minY) / cell); var col = Mathf.RoundToInt((z - minZ) / cell);
                return row >= 0 && col >= 0 && row < edges.GetLength(0) && col < edges.GetLength(1)
                    ? edges[row, col] : float.NegativeInfinity;
            }
        }

        public static void SolveTwoBone(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 bendHint)
        {
            var origin = upper.position;
            var a = Vector3.Distance(origin, lower.position); var b = Vector3.Distance(lower.position, hand.position);
            if (a < .0001f || b < .0001f) return;
            var delta = target - origin;
            var distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
            if (delta.sqrMagnitude < .000001f) return;
            var direction = delta.normalized;
            var bend = Vector3.ProjectOnPlane(bendHint, direction).normalized;
            if (bend.sqrMagnitude < .0001f) bend = Vector3.ProjectOnPlane(lower.position - origin, direction).normalized;
            if (bend.sqrMagnitude < .0001f) return;
            var along = (a * a + distance * distance - b * b) / (2 * distance);
            var height = Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            var elbow = origin + direction * along + bend * height;
            var handRotation = hand.rotation;
            upper.rotation = Quaternion.FromToRotation(lower.position - origin, elbow - origin) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, origin + direction * distance - lower.position) * lower.rotation;
            hand.rotation = handRotation;
        }
    }
}
