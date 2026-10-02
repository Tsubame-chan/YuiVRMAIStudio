using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniVRM10;

namespace YuiPhysicalAI.Avatar
{
    // Visibility means the selected outfit, not camera/frustum visibility.
    // Cache non-zero skin weights once; appearance changes only rebuild membership.
    [DefaultExecutionOrder(900)]
    public sealed class YuiVisibleSecondaryMotion : MonoBehaviour
    {
        [Serializable] public sealed class Binding {
            public Renderer Renderer;
            public Mesh Mesh;
            public Transform[] WeightedBones;
            public bool Exact;
        }
        public Binding[] Bindings=Array.Empty<Binding>();
        public int ActiveWeightedBoneCount {get;private set;}
        private readonly HashSet<Transform> needed=new HashSet<Transform>();
        private Vrm10Instance vrm;
        private List<Vrm10InstanceSpringBone.Spring> authoredSprings;
        private UnityChan.SpringManager[] managers;
        private UnityChan.SpringBone[][] authoredLegacy;
        private float nextCheck;
        private int lastState=int.MinValue;
        public void CaptureBindings(bool editorPreparation=false)
        {
            Bindings=GetComponentsInChildren<Renderer>(true).Where(r=>r is SkinnedMeshRenderer || r is MeshRenderer).Select(r=> {
                if(!(r is SkinnedMeshRenderer skin))return new Binding {Renderer=r,WeightedBones=new[]{r.transform},Exact=true};
                if(skin.sharedMesh==null)return new Binding {Renderer=r,WeightedBones=Array.Empty<Transform>(),Exact=true};
                var existing=Bindings.FirstOrDefault(b=>b.Renderer==r && b.Mesh==skin.sharedMesh && b.Exact);
                if(existing!=null)return existing;
                var bones=skin.bones;var indices=new HashSet<int>();var exact=skin.sharedMesh.isReadable || editorPreparation;
                if(exact) {
                    var weights=skin.sharedMesh.GetAllBoneWeights(); // Borrowed mesh data; not owned by this component.
                    foreach(var w in weights)if(w.weight>0)indices.Add(w.boneIndex);
                }
                return new Binding {Renderer=r,Mesh=skin.sharedMesh,Exact=exact,WeightedBones=exact?indices.Where(i=>i>=0 && i<bones.Length).Select(i=>bones[i]).ToArray():bones};
            }).ToArray();
        }
        private void Awake()
        {
            CaptureBindings();vrm=GetComponentInChildren<Vrm10Instance>(true);
            if(vrm!=null)authoredSprings=vrm.SpringBone.Springs.ToList();
            managers=GetComponentsInChildren<UnityChan.SpringManager>(true);
            authoredLegacy=managers.Select(m=>m.springBones.ToArray()).ToArray();
        }
        private void OnEnable(){lastState=int.MinValue;nextCheck=0;}
        private void Update(){if(Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+.5f;Refresh();}
        public void Refresh()
        {
            var state=17;
            foreach(var b in Bindings)state=unchecked(state*31+(b.Renderer!=null && b.Renderer.enabled && b.Renderer.gameObject.activeInHierarchy?1:0));
            if(state==lastState)return;lastState=state;needed.Clear();
            foreach(var b in Bindings)if(b.Renderer!=null && b.Renderer.enabled && b.Renderer.gameObject.activeInHierarchy)
                foreach(var bone in b.WeightedBones)for(var t=bone;t!=null && t.IsChildOf(transform);t=t.parent)needed.Add(t);
            ActiveWeightedBoneCount=needed.Count;
            GetComponent<YuiAvatarSpringMotion>()?.SetActiveBones(needed);
            GetComponent<YuiNativeSpringRig>()?.SetActiveBones(needed);
            if(vrm!=null && authoredSprings!=null) {
                var selected=authoredSprings.Where(s=>s.Joints.Any(j=>j!=null && needed.Contains(j.transform))).ToList();
                if(!selected.SequenceEqual(vrm.SpringBone.Springs)) {
                    vrm.Runtime.SpringBone.RestoreInitialTransform();vrm.SpringBone.Springs=selected;
                    vrm.Runtime.SpringBone.ReconstructSpringBone();YuiVrmSpringReset.ResetParticlePositions(vrm);
                }
            }
            if(managers!=null)for(var i=0;i<managers.Length;i++)managers[i].springBones=authoredLegacy[i].Where(b=>b!=null && needed.Contains(b.transform)).ToArray();
        }
    }
}
