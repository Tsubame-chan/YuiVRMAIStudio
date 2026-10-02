//
//SpingManager.cs for unity-chan!
//
//Original Script is here:
//ricopin / SpingManager.cs
//Rocket Jump : http://rocketjump.skr.jp/unity3d/109/
//https://twitter.com/ricopin416
//
//Revised by N.Kobayashi 2014/06/24
//           Y.Ebata
//
using UnityEngine;
using System.Collections;

// Yui: schedule after viewer transforms/animation and normalize authored 60 Hz motion.

namespace UnityChan
{
    [DefaultExecutionOrder(11020)]
	public class SpringManager : MonoBehaviour
	{
		//Kobayashi
		// DynamicRatio is paramater for activated level of dynamic animation 
		public float dynamicRatio = 1.0f;

		//Ebata
		public float			stiffnessForce;
		public AnimationCurve	stiffnessCurve;
		public float			dragForce;
		public AnimationCurve	dragCurve;
		public SpringBone[] springBones;

		void Start ()
		{
			UpdateParameters ();
		}
	
		void Update ()
		{
#if UNITY_EDITOR
		//Kobayashi
		if(dynamicRatio >= 1.0f)
			dynamicRatio = 1.0f;
		else if(dynamicRatio <= 0.0f)
			dynamicRatio = 0.0f;
		//Ebata
		UpdateParameters();
#endif
		}
	
        private void LateUpdate() { Simulate(Time.deltaTime); }
        public void Simulate(float deltaTime)
        {
            if(deltaTime<=0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || dynamicRatio==0 || springBones==null)return;
            if(deltaTime>.2f){foreach(var bone in springBones)if(bone!=null)bone.ResetSpring();return;}
            var steps=Mathf.Max(1,Mathf.CeilToInt(deltaTime*60f));
            var dt=deltaTime/steps;
            for(var step=0;step<steps;step++)foreach(var bone in springBones)
                if(bone!=null && dynamicRatio>bone.threshold)bone.UpdateSpring(dt);
        }

		private void UpdateParameters ()
		{
			UpdateParameter ("stiffnessForce", stiffnessForce, stiffnessCurve);
			UpdateParameter ("dragForce", dragForce, dragCurve);
		}
	
		private void UpdateParameter (string fieldName, float baseValue, AnimationCurve curve)
		{
            if(springBones==null || springBones.Length==0 || curve==null || curve.length==0)return;
			var start = curve.keys [0].time;
			var end = curve.keys [curve.length - 1].time;
			//var step	= (end - start) / Mathf.Max(1,(springBones.Length - 1));
		
			var prop = typeof(SpringBone).GetField (fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
		
			for (int i = 0; i < springBones.Length; i++) {
				//Kobayashi
				if (springBones[i]!=null && !springBones [i].isUseEachBoneForceSettings) {
					var scale = curve.Evaluate (start + (end - start) * i / Mathf.Max(1,(springBones.Length - 1)));
					prop.SetValue (springBones [i], baseValue * scale);
				}
			}
		}
	}
}