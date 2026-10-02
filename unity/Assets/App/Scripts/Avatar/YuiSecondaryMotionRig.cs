using System;
using UnityEngine;

namespace YuiPhysicalAI.Avatar
{
    // Authored data for baked models whose original simulator cannot ship.
    // VRMs use their own spring definitions and never receive this fallback.
    public sealed class YuiSecondaryMotionRig : MonoBehaviour
    {
        [Serializable] public sealed class Chain { public Transform Root; public float Pull=.65f; public Transform[] Excluded=Array.Empty<Transform>(); }
        public Chain[] Chains=Array.Empty<Chain>();
        public float MaxAngleDegrees=3;
        public void Initialize()
        {
            if(GetComponent<YuiNativeSpringRig>()!=null || GetComponentInChildren<UniVRM10.Vrm10Instance>()!=null || GetComponentInChildren<VRM.VRMSpringBone>()!=null || GetComponent<YuiAvatarSpringMotion>()!=null)return;
            var motion=gameObject.AddComponent<YuiAvatarSpringMotion>();
            motion.MaxAngleDegrees=MaxAngleDegrees;
            motion.InitializeBodyExclusions(GetComponentInChildren<Animator>());
            foreach(var chain in Chains)if(chain.Root!=null)
                motion.AddChain(chain.Root,chain.Pull,0,new System.Collections.Generic.HashSet<Transform>(chain.Excluded??Array.Empty<Transform>()));
        }
        private void Awake() { Initialize(); }
    }
}
