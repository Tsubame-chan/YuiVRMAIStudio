using System.Linq;
using UnityEngine;
using UniVRM10;

namespace YuiPhysicalAI.Avatar
{
    // Common gentle excitation. Each model keeps its authored spring simulator,
    // colliders and bone selection. No avatar names or inferred facial bones.
    [DefaultExecutionOrder(1000)]
    public sealed class YuiAvatarAmbientMotion : MonoBehaviour
    {
        private Vrm10Instance vrm10;
        private VRM.VRMSpringBone[] vrm0;
        private Vector3[] gravityDirections;
        private float[] gravityPowers;
        private UnityChan.SpringBone[] legacy;
        private Vector3[] legacyForces;
        private YuiAvatarSpringMotion fallback;
        private float fallbackSway, phase;
        public int AuthoredSpringCount => vrm10!=null?vrm10.SpringBone.Springs.Count:(vrm0?.Length??0)+(legacy?.Length??0)+(fallback!=null?fallback.JointCount:0)+(GetComponent<YuiNativeSpringRig>()?.ActiveSpringCount??0);
        private void Awake()
        {
            vrm10=GetComponentInChildren<Vrm10Instance>();
            vrm0=vrm10!=null?System.Array.Empty<VRM.VRMSpringBone>():GetComponentsInChildren<VRM.VRMSpringBone>();
            gravityDirections=vrm0.Select(s=>s.m_gravityDir).ToArray();gravityPowers=vrm0.Select(s=>s.m_gravityPower).ToArray();
            legacy=vrm10!=null||vrm0.Length>0?System.Array.Empty<UnityChan.SpringBone>():GetComponentsInChildren<UnityChan.SpringBone>();
            legacyForces=legacy.Select(s=>s.springForce).ToArray();
            fallback=GetComponent<YuiAvatarSpringMotion>();fallbackSway=fallback!=null?fallback.AmbientSwayDegrees:0;
        }
        private void Update() { Step(Time.deltaTime); }
        // A slowly varying field, not a metronome shared by every spring.
        // Keep the input coherent; authored stiffness/damping determine each part's response.
        public static Vector2 SampleIdleDrift(float time)
        {
            var t = Mathf.Max(0, time);
            var ramp = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 4f));
            return Vector2.ClampMagnitude(new Vector2(
                Mathf.PerlinNoise(13.17f + t * .13f, 7.43f) * 2f - 1f,
                Mathf.PerlinNoise(37.61f, 23.29f + t * .09f) * 2f - 1f), 1f) * ramp;
        }
        public void Step(float deltaTime)
        {
            if(deltaTime<=0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))return;
            phase+=Mathf.Min(deltaTime,.0333f);
            // Keep idle excitation subtle without reducing movement-driven inertia.
            var drift = SampleIdleDrift(phase);
            var breeze=(transform.right * drift.x + transform.forward * drift.y)*.0015f;
            if(vrm10!=null)YuiVrmSpringReset.SetAmbientForce(vrm10,breeze);
            for(var i=0;i<vrm0.Length;i++)if(vrm0[i]!=null) {
                var force=gravityDirections[i]*gravityPowers[i]+breeze;
                vrm0[i].m_gravityPower=force.magnitude;vrm0[i].m_gravityDir=force.normalized;
            }
            for(var i=0;i<legacy.Length;i++)if(legacy[i]!=null)legacy[i].springForce=legacyForces[i]+breeze*.025f;
            if(fallback!=null)fallback.AmbientSwayDegrees=.2f;
        }
        private void OnDisable()
        {
            if(vrm10!=null)YuiVrmSpringReset.SetAmbientForce(vrm10,Vector3.zero);
            if(vrm0!=null)for(var i=0;i<vrm0.Length;i++)if(vrm0[i]!=null) {vrm0[i].m_gravityDir=gravityDirections[i];vrm0[i].m_gravityPower=gravityPowers[i];}
            if(legacy!=null)for(var i=0;i<legacy.Length;i++)if(legacy[i]!=null)legacy[i].springForce=legacyForces[i];
            if(fallback!=null)fallback.AmbientSwayDegrees=fallbackSway;
        }
    }
}
