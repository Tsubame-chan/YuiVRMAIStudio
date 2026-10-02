//
//SpringBone.cs for unity-chan!
//
//Original Script is here:
//ricopin / SpringBone.cs
//Rocket Jump : http://rocketjump.skr.jp/unity3d/109/
//https://twitter.com/ricopin416
//
//Revised by N.Kobayashi 2014/06/20
//
using UnityEngine;
using System.Collections;

namespace UnityChan
{
	public class SpringBone : MonoBehaviour
	{
		//次のボーン
		public Transform child;

		//ボーンの向き
		public Vector3 boneAxis = new Vector3 (-1.0f, 0.0f, 0.0f);
		public float radius = 0.05f;

		//各SpringBoneに設定されているstiffnessForceとdragForceを使用するか？
		public bool isUseEachBoneForceSettings = false; 

		//バネが戻る力
		public float stiffnessForce = 0.01f;

		//力の減衰力
		public float dragForce = 0.4f;
		public Vector3 springForce = new Vector3 (0.0f, -0.0001f, 0.0f);
		public SpringCollider[] colliders;
		public bool debug = true;
		//Kobayashi:Thredshold Starting to activate activeRatio
		public float threshold = 0.01f;
		private float springLength;
		private Quaternion localRotation;
		private Transform trs;
		private Vector3 currTipPos;
		private Vector3 prevTipPos;
        private float previousDeltaTime = 1f / 60f;
		//Kobayashi
		private Transform org;
		//Kobayashi:Reference for "SpringManager" component with unitychan 
		private SpringManager managerRef;

		private void Awake ()
		{
			trs = transform;
			localRotation = transform.localRotation;
			//Kobayashi:Reference for "SpringManager" component with unitychan
			// GameObject.Find("unitychan_dynamic").GetComponent<SpringManager>();
			managerRef = GetParentSpringManager (transform);
		}

		private SpringManager GetParentSpringManager (Transform t)
		{
			var springManager = t.GetComponent<SpringManager> ();

			if (springManager != null)
				return springManager;

			if (t.parent != null) {
				return GetParentSpringManager (t.parent);
			}

			return null;
		}

		private void Start ()
		{
			springLength = Vector3.Distance (trs.position, child.position);
			currTipPos = child.position;
			prevTipPos = child.position;
		}

        public void ResetSpring()
        {
            if(trs==null)trs=transform;
            if(child==null)return;
            springLength=Vector3.Distance(trs.position,child.position);
            currTipPos=prevTipPos=child.position;
            previousDeltaTime=1f/60f;
        }
        public void UpdateSpring() { UpdateSpring(Time.deltaTime); }
        public void UpdateSpring(float deltaTime)
        {
            if(child==null || deltaTime<=0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))return;
            if(trs==null)trs=transform;
            if(springLength<=0)ResetSpring();
            if(deltaTime>.2f){trs.localRotation=localRotation;ResetSpring();return;}
            trs.localRotation=localRotation;
            // The authored values are per 60 Hz step. The old dt divisions cancelled
            // multiplication by dt², so higher frame rates made every strand stiffer.
            var step = deltaTime * 60f;
            var velocity=(currTipPos-prevTipPos)*(deltaTime/previousDeltaTime)
                * Mathf.Pow(Mathf.Clamp01(1f-dragForce),step);
            var acceleration=trs.rotation*(boneAxis*stiffnessForce)+springForce;
            var next=currTipPos+velocity+acceleration*(step*step);
            var direction=next-trs.position;
            if(direction.sqrMagnitude<.00000001f)direction=trs.TransformDirection(boneAxis);
            next=direction.normalized*springLength+trs.position;
            foreach(var collider in colliders ?? new SpringCollider[0])
            {
                if(collider==null)continue;
                var radiusSum=radius+collider.radius;
                if(Vector3.Distance(next,collider.transform.position)<=radiusSum)
                {
                    var normal=next-collider.transform.position;
                    if(normal.sqrMagnitude<.00000001f)normal=trs.TransformDirection(boneAxis);
                    next=collider.transform.position+normal.normalized*radiusSum;
                    next=(next-trs.position).normalized*springLength+trs.position;
                }
            }
            prevTipPos=currTipPos;currTipPos=next;previousDeltaTime=deltaTime;
            var aimVector=trs.TransformDirection(boneAxis);
            var aimRotation=Quaternion.FromToRotation(aimVector,currTipPos-trs.position);
            var secondaryRotation=aimRotation*trs.rotation;
            trs.rotation=Quaternion.Lerp(trs.rotation,secondaryRotation,managerRef!=null?managerRef.dynamicRatio:1f);
        }

		private void OnDrawGizmos ()
		{
			if (debug) {
				Gizmos.color = Color.yellow;
				Gizmos.DrawWireSphere (currTipPos, radius);
			}
		}
	}
}
