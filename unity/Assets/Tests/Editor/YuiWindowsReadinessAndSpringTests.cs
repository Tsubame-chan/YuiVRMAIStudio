using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.LocalAI;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Avatar;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YuiPhysicalAI.Tests
{
    public class YuiWindowsReadinessAndSpringTests
    {
        [Test] public void BackendVoiceSelectionDoesNotOverrideProfileWithClientDefaults()
        {
            var options=new JsonSerializerSettings { NullValueHandling=NullValueHandling.Ignore };
            var body=JObject.Parse(JsonConvert.SerializeObject(new TtsRequest { Text="test", VoiceProfileId="saved" },options));
            Assert.AreEqual("saved",(string)body["voice_profile_id"]);
            Assert.IsNull(body["speaker_id"]);Assert.IsNull(body["speed_scale"]);Assert.IsNull(body["pitch_scale"]);
            var legacy=JObject.Parse(JsonConvert.SerializeObject(new TtsRequest { Text="test", SpeakerId=3 },options));
            Assert.AreEqual(3,(int)legacy["speaker_id"]);
        }
        [Test] public void BackendVoiceAssignmentRemainsWithinItsCharacterProfile()
        {
            var directory=Path.Combine(Path.GetTempPath(),"yui-profile-"+Guid.NewGuid().ToString("N"));
            try {
                var store=new YuiCharacterProfileStore(directory);
                store.Save("first",new YuiCharacterProfile { TtsMode="backend-profile",BackendVoiceProfileId="saved",BackendVoiceServer="http://127.0.0.1:8000" });
                store.Save("second",new YuiCharacterProfile());
                Assert.AreEqual("saved",store.Read("first").BackendVoiceProfileId);
                Assert.AreEqual("",store.Read("second").BackendVoiceProfileId);
                Assert.AreEqual("server",store.Read("second").TtsMode);
            } finally { if(Directory.Exists(directory))Directory.Delete(directory,true); }
        }
        [Test] public void WindowsModelDoesNotSubstituteForRuntimeAndMissingDllIsDetected()
        {
            var root=Path.Combine(Path.GetTempPath(),"yui-readiness-"+Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"model.litertlm"),"test");
                Assert.IsFalse(YuiDesktopInferenceProcess.WindowsChatFilesReady(root));
                foreach(var file in YuiDesktopInferenceProcess.WindowsChatFiles){var path=Path.Combine(root,file);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,"test");}
                Assert.IsTrue(YuiDesktopInferenceProcess.WindowsChatFilesReady(root));
                File.Delete(Path.Combine(root,"backend/.venv/Lib/site-packages/litert_lm/dxil.dll"));
                Assert.IsFalse(YuiDesktopInferenceProcess.WindowsChatFilesReady(root));
            } finally { if(Directory.Exists(root))Directory.Delete(root,true); }
        }
        private static float Response(int fps)
        {
            var host=new GameObject("spring-test");
            try {
                var manager=host.AddComponent<UnityChan.SpringManager>();
                var hair=new GameObject("hair");hair.transform.SetParent(host.transform,false);
                var tip=new GameObject("tip");tip.transform.SetParent(hair.transform,false);tip.transform.localPosition=Vector3.right*.2f;
                var spring=hair.AddComponent<UnityChan.SpringBone>();spring.child=tip.transform;spring.boneAxis=Vector3.right;
                spring.stiffnessForce=.002f;spring.dragForce=.25f;spring.springForce=Vector3.zero;spring.colliders=Array.Empty<UnityChan.SpringCollider>();
                manager.springBones=new[]{spring};spring.ResetSpring();var peak=0f;
                for(var i=0;i<fps;i++){host.transform.rotation=Quaternion.Euler(0,90f*(i+1)/fps,0);manager.Simulate(1f/fps);peak=Mathf.Max(peak,Quaternion.Angle(Quaternion.identity,hair.transform.localRotation));}
                Assert.That(tip.transform.localPosition.magnitude,Is.EqualTo(.2f).Within(.00001));
                return peak;
            }finally{UnityEngine.Object.DestroyImmediate(host);}
        }
        [Test] public void UnityChanSpringRetainsTurnInertiaAtHighFrameRates()
        {
            var baseline=Response(60);var fast=Response(144);
            Assert.That(baseline,Is.GreaterThan(1));
            Assert.That(fast,Is.EqualTo(baseline).Within(baseline*.25f));
        }
        [Test] public void UnityChanSpringHandlesEmptyAndSingleBoneAndPausedTime()
        {
            var root=new GameObject("empty");
            try {var manager=root.AddComponent<UnityChan.SpringManager>();manager.springBones=Array.Empty<UnityChan.SpringBone>();manager.Simulate(0);manager.Simulate(float.NaN);manager.Simulate(.3f);}finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
