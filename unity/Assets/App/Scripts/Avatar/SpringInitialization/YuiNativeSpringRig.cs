using System;
using System.Collections.Generic;
using System.Linq;
using UniGLTF.SpringBoneJobs;
using UniGLTF.SpringBoneJobs.Blittables;
using UniGLTF.SpringBoneJobs.InputPorts;
using UniVRM10;
using UnityEngine;

namespace YuiPhysicalAI.Avatar
{
    // Data-only bridge for baked humanoids. Simulation is UniVRM's official solver;
    // no inferred stiffness, global angle cap, or procedural bone animation.
    [DefaultExecutionOrder(11010)]
    public sealed class YuiNativeSpringRig : MonoBehaviour
    {
        public List<Vrm10InstanceSpringBone.Spring> Springs=new List<Vrm10InstanceSpringBone.Spring>();
        private FastSpringBoneBuffer buffer;
        private FastSpringBoneBufferCombiner combiner;
        private FastSpringBoneScheduler scheduler;
        private Dictionary<Transform,Quaternion> rest;
        private HashSet<Transform> visible;
        public int ActiveSpringCount {get;private set;}
        private void OnEnable(){if(Application.isPlaying)Rebuild();}
        private void LateUpdate(){Simulate(Time.deltaTime);}
        public void Simulate(float deltaTime){if(scheduler!=null && deltaTime>0 && !float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime))scheduler.Schedule(deltaTime).Complete();}
        private void OnDisable(){Release();RestorePose();}
        private void OnDestroy(){Release();}
        public void SetActiveBones(HashSet<Transform> bones)
        {
            if(visible!=null && visible.SetEquals(bones))return;
            visible=new HashSet<Transform>(bones);
            if(isActiveAndEnabled && Application.isPlaying)Rebuild();
        }
        public void Rebuild()
        {
            Release();
            if(rest==null)rest=Springs.SelectMany(s=>s.Joints).Where(j=>j!=null).Select(j=>j.transform).Distinct().ToDictionary(t=>t,t=>t.localRotation);
            RestorePose();
            var selected=Springs.Where(s=>s.Joints.Count>1 && (visible==null || s.Joints.Any(j=>j!=null&&visible.Contains(j.transform)))).ToArray();
            ActiveSpringCount=selected.Length;
            if(selected.Length==0)return;
            var input=selected.Select(s=>new FastSpringBoneSpring{
                center=s.Center,
                joints=s.Joints.Select(j=>new FastSpringBoneJoint{Transform=j.transform,Joint=j.Blittable,DefaultLocalRotation=rest[j.transform]}).ToArray(),
                colliders=s.ColliderGroups.Where(g=>g!=null).SelectMany(g=>g.Colliders).Where(c=>c!=null).Select(c=>new FastSpringBoneCollider{
                    Transform=c.transform,Collider=new BlittableCollider(c.Offset,c.Radius,c.TailOrNormal,ColliderType(c.ColliderType))
                }).ToArray()
            }).ToArray();
            buffer=new FastSpringBoneBuffer(transform,input);
            combiner=new FastSpringBoneBufferCombiner();
            scheduler=new FastSpringBoneScheduler(combiner);
            combiner.Register(buffer,null);
            YuiVrmSpringReset.ResetParticlePositions(transform,combiner);
            combiner.Combined.SetModelLevel(transform,new BlittableModelLevel(supportsScalingAtRuntime:true));
        }
        private void RestorePose(){if(rest!=null)foreach(var p in rest)if(p.Key!=null)p.Key.localRotation=p.Value;}
        private void Release()
        {
            scheduler?.Dispose();scheduler=null;
            combiner?.Dispose();combiner=null;
            buffer?.Dispose();buffer=null;
            ActiveSpringCount=0;
        }
        private static BlittableColliderType ColliderType(VRM10SpringBoneColliderTypes type)
        {
            switch(type){
                case VRM10SpringBoneColliderTypes.Sphere:return BlittableColliderType.Sphere;
                case VRM10SpringBoneColliderTypes.Capsule:return BlittableColliderType.Capsule;
                case VRM10SpringBoneColliderTypes.Plane:return BlittableColliderType.Plane;
                case VRM10SpringBoneColliderTypes.SphereInside:return BlittableColliderType.SphereInside;
                case VRM10SpringBoneColliderTypes.CapsuleInside:return BlittableColliderType.CapsuleInside;
                default:throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
