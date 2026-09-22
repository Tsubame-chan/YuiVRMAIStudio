using System;
using System.Collections.Generic;
using UniVRM10;
using UnityEngine;

namespace YuiPhysicalAI.Avatar
{
    /// <summary>
    /// A newly placed/re-enabled avatar must not simulate from particles cached at
    /// its import origin/T-pose. Reset after pose/constraints, before VRM spring jobs.
    /// Keep rendering hidden until initialization motion settles; animation keeps running.
    /// </summary>
    [DefaultExecutionOrder(11005)]
    public sealed class YuiAvatarPresentation : MonoBehaviour
    {
        private Renderer[] renderers;
        private bool[] previousHidden;
        private int preparedFrames;
        private bool holdingVisibility;
        private bool heldForSelection;
        private Transform[] springJoints = Array.Empty<Transform>();
        private Vector3[] lastJointPositions = Array.Empty<Vector3>();
        private float preparedAt, stableFor, speedThreshold;
        public bool IsReady { get; private set; }

        public void HoldForSelection() => heldForSelection = true;
        public void ReleaseForDisplay()
        {
            heldForSelection = false;
            if (IsReady) RestoreVisibility();
        }

        private void OnEnable()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            previousHidden = new bool[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                previousHidden[i] = renderers[i].forceRenderingOff;
                renderers[i].forceRenderingOff = true;
            }
            holdingVisibility = true;
            // Runtime loading can already have advanced spring bones while textures
            // were awaited. Fit against the authored garment, not that transient pose.
            // This must happen before the fallback's first LateUpdate/mesh sampling.
            try
            {
                foreach (var vrm in GetComponentsInChildren<Vrm10Instance>(true))
                    vrm.Runtime.SpringBone.RestoreInitialTransform();
            }
            catch (Exception ex) { Debug.LogWarning("Yui avatar initial garment pose: " + ex.Message); }
            preparedFrames = 0;
            stableFor = 0f;
            IsReady = false;
        }

        private void LateUpdate()
        {
            if (IsReady) return;
            if (preparedFrames == 0)
            {
                try
                {
                    foreach (var vrm in GetComponentsInChildren<Vrm10Instance>(true))
                    {
                        // Reset local spring rotations too: an inactive model may
                        // retain deformation from its last displayed frame.
                        vrm.Runtime.SpringBone.RestoreInitialTransform();
                        GetComponent<YuiCustomVrmIdlePose>()?.ApplyNow();
                        vrm.Runtime.Process();
                        // Nop/disabled spring runtimes return false. They must not
                        // prevent an otherwise usable avatar from ever appearing.
                        if (vrm.Runtime.SpringBone.ReconstructSpringBone())
                            YuiVrmSpringReset.ResetParticlePositions(vrm);
                    }
                    foreach (var spring in GetComponentsInChildren<YuiAvatarSpringMotion>(true)) spring.ResetMotion();
                    var joints = new HashSet<Transform>();
                    foreach (var vrm in GetComponentsInChildren<Vrm10Instance>(true))
                    foreach (var spring in vrm.SpringBone.Springs)
                    foreach (var joint in spring.Joints)
                        if (joint != null) joints.Add(joint.transform);
                    springJoints = new Transform[joints.Count]; joints.CopyTo(springJoints);
                    lastJointPositions = new Vector3[springJoints.Length];
                    for (var i = 0; i < springJoints.Length; i++) lastJointPositions[i] = springJoints[i].position;
                }
                catch (Exception ex)
                {
                    // Do not leave an otherwise usable avatar invisible forever.
                    Debug.LogWarning("Yui avatar spring initialization: " + ex.Message);
                }
                preparedAt = Time.unscaledTime;
                var animator = GetComponentInChildren<Animator>();
                speedThreshold = .025f * (animator != null && animator.isHuman ? animator.humanScale : 1f)
                    * Mathf.Abs(transform.lossyScale.y);
            }
            else
            {
                var maxMovement = 0f;
                for (var i = 0; i < springJoints.Length; i++)
                {
                    if (springJoints[i] == null) continue;
                    var position = springJoints[i].position;
                    maxMovement = Mathf.Max(maxMovement, Vector3.Distance(position, lastJointPositions[i]));
                    lastJointPositions[i] = position;
                }
                stableFor = maxMovement <= speedThreshold * Time.unscaledDeltaTime
                    ? stableFor + Time.unscaledDeltaTime : 0f;
            }
            // Always allow two completed job updates. Longer gravity/collision settling
            // stays hidden too; animated/windy avatars cannot block display indefinitely.
            if (++preparedFrames <= 2) return;
            if (springJoints.Length > 0 && stableFor < .05f && Time.unscaledTime - preparedAt < .8f) return;
            IsReady = true;
            if (!heldForSelection) RestoreVisibility();
        }

        private void OnDisable()
        {
            RestoreVisibility();
            IsReady = false;
        }

        private void RestoreVisibility()
        {
            if (!holdingVisibility) return;
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].forceRenderingOff = previousHidden[i];
            holdingVisibility = false;
        }
    }
}
