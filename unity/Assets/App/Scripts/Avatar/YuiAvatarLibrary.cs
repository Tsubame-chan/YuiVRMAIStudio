using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEngine;
namespace YuiPhysicalAI.Avatar {
 public static class YuiAvatarLibrary {
  [Serializable] public sealed class Appearance { public string name; public string file; }
  [Serializable] public sealed class Entry { public string id; public string name; public string file; public List<Appearance> appearances; }
  public static string DirectoryPath => Path.Combine(Application.persistentDataPath,"AvatarLibrary");
  public static List<Entry> Read() {
   try { return new YuiAvatarLibraryStore(DirectoryPath).Read(); }
   catch(Exception ex) { Debug.LogWarning("Avatar library index: " + ex.Message); return new List<Entry>(); }
  }
  public static string Resolve(Entry entry) => new YuiAvatarLibraryStore(DirectoryPath).Resolve(entry);
  public static Entry Register(string source) => new YuiAvatarLibraryStore(DirectoryPath).Register(source);
  public static System.Threading.Tasks.Task<Entry> RegisterAsync(string source, string replaceId = null, System.Threading.CancellationToken cancellationToken = default) {
   var store = new YuiAvatarLibraryStore(DirectoryPath);
   return System.Threading.Tasks.Task.Run(() => store.Register(source, replaceId, cancellationToken));
  }
  public static void Rename(Entry entry, string name) => new YuiAvatarLibraryStore(DirectoryPath).Rename(entry.id,name);
  public static void RemoveFromList(Entry entry) => new YuiAvatarLibraryStore(DirectoryPath).Remove(entry.id);
  public static void CaptureThumbnail(Entry entry) {
   var camera=Camera.main;if(camera==null)return;
   var rt=new RenderTexture(192,192,24);var target=camera.targetTexture;var active=RenderTexture.active;var aspect=camera.aspect;
   Texture2D texture=null;
   try{camera.targetTexture=rt;camera.aspect=1;camera.Render();RenderTexture.active=rt;texture=new Texture2D(192,192,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,192,192),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(DirectoryPath,entry.id+".png"),texture.EncodeToPNG());}
   catch(Exception ex){Debug.LogWarning("Avatar thumbnail: "+ex.Message);}
   finally{camera.targetTexture=target;camera.aspect=aspect;RenderTexture.active=active;rt.Release();UnityEngine.Object.Destroy(rt);if(texture!=null)UnityEngine.Object.Destroy(texture);}
  }
 }
}
