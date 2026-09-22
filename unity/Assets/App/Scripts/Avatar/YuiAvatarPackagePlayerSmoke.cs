#if YUI_AVATAR_PACKAGE_SMOKE
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using YuiPhysicalAI.Avatar;
namespace YuiPhysicalAI.Diagnostics {
 public sealed class YuiAvatarPackagePlayerSmoke : MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static void Boot(){new GameObject("Avatar Package Smoke").AddComponent<YuiAvatarPackagePlayerSmoke>();}
  private IEnumerator Start(){
   var path=Environment.GetEnvironmentVariable("YUI_AVATAR_SMOKE_ZIP");
   var report=Environment.GetEnvironmentVariable("YUI_AVATAR_SMOKE_REPORT");
   if(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(report)){Application.Quit(2);yield break;}
   yield return null;
   var importer=FindObjectOfType<YuiRuntimeVrmImporter>();
   if(importer==null){File.WriteAllText(report,"FAIL: importer absent");Application.Quit(3);yield break;}
   var task=importer.ImportFromPathAsync(path,true);
   var deadline=Time.realtimeSinceStartup+90;
   while(!task.IsCompleted && Time.realtimeSinceStartup<deadline)yield return null;
   if(!task.IsCompleted || task.IsFaulted || !task.Result){File.WriteAllText(report,"FAIL: "+importer.LastImportMessage);Application.Quit(4);yield break;}
   var metadata=FindObjectOfType<YuiAvatarPackageMetadata>();
   if(metadata==null){File.WriteAllText(report,"FAIL: metadata absent");Application.Quit(5);yield break;}
   var renderers=metadata.GetComponentsInChildren<SkinnedMeshRenderer>(true);
   var materials=renderers.SelectMany(r=>r.sharedMaterials).ToArray();
   var vowels=0;
   foreach(var vowel in new[]{"aa","ih","ou","ee","oh"}){
    if(!metadata.TryGetViseme(vowel,out var r,out var shape))continue;
    var i=r.sharedMesh.GetBlendShapeIndex(shape);r.SetBlendShapeWeight(i,70);
    if(Mathf.Approximately(r.GetBlendShapeWeight(i),70))vowels++;
    r.SetBlendShapeWeight(i,0);
   }
   var humanoid=metadata.GetComponentInChildren<Animator>();
   var valid=renderers.Length>0 && materials.All(m=>m!=null && m.shader!=null && m.shader.isSupported) && vowels==5 && humanoid!=null && humanoid.isHuman;
   File.WriteAllText(report,(valid?"PASS":"FAIL")+"\nUnity="+Application.unityVersion+"\nOS="+Application.platform+"\nRenderer="+renderers.Length+"\nMaterials="+string.Join(",",materials.Select(m=>m==null?"NULL":m.shader.name+":"+m.shader.isSupported))+"\nMapped vowels writable="+vowels+"/5\nHumanoid="+(humanoid!=null && humanoid.isHuman)+"\nImport="+importer.LastImportMessage+"\nNote: mapped weights are tested; audio-driven motion, expression playback and physics are separate gates.\n");
   yield return new WaitForSeconds(3);
   // Capture the avatar camera without chat/history UI in the evidence image.
   var camera=Camera.main;
   if(camera!=null){
    var rt=new RenderTexture(900,1200,24);
    var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
    try{
     camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
     var texture=new Texture2D(900,1200,TextureFormat.RGB24,false);
     texture.ReadPixels(new Rect(0,0,900,1200),0,0);texture.Apply();
     File.WriteAllBytes(report+".png",texture.EncodeToPNG());Destroy(texture);
    }finally{camera.targetTexture=previousTarget;RenderTexture.active=previousActive;rt.Release();Destroy(rt);}
   }
   yield return new WaitForSeconds(2);
   Application.Quit(valid?0:6);
  }
 }
}
#endif
