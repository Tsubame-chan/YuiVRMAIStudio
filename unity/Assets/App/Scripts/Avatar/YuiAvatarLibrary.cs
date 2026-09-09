using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEngine;
namespace YuiPhysicalAI.Avatar {
 public static class YuiAvatarLibrary {
  [Serializable] public sealed class Entry { public string id; public string name; public string file; }
  [Serializable] private sealed class Index { public List<Entry> entries=new List<Entry>(); }
  public static string DirectoryPath => Path.Combine(Application.persistentDataPath,"AvatarLibrary");
  public static List<Entry> Read() {
   var path=Path.Combine(DirectoryPath,"index.json");
   if(!File.Exists(path))return new List<Entry>();
   try{return (JsonUtility.FromJson<Index>(File.ReadAllText(path))?.entries ?? new List<Entry>()).Where(e=>e!=null && SafeName(e.file) && SafeName(e.id)).ToList();}
   catch(Exception ex){Debug.LogWarning("Avatar library index: "+ex.Message);return new List<Entry>();}
  }
  private static bool SafeName(string s)=>!string.IsNullOrEmpty(s) && s!="." && s!=".." && s.IndexOfAny(new[]{'/','\\',':'})<0 && Path.GetFileName(s)==s;
  public static string Resolve(Entry entry) {if(entry==null || !SafeName(entry.file))throw new ArgumentException("Invalid avatar library entry");return Path.Combine(DirectoryPath,entry.file);}
  public static Entry Register(string source) {
   Directory.CreateDirectory(DirectoryPath);string hash;
   using(var sha=SHA256.Create())using(var stream=File.OpenRead(source))hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
   var rows=Read();var old=rows.FirstOrDefault(e=>e.id==hash);if(old!=null && File.Exists(Resolve(old)))return old;
   var entry=new Entry{id=hash,name=Path.GetFileNameWithoutExtension(source),file=hash+Path.GetExtension(source).ToLowerInvariant()};
   var target=Resolve(entry);if(!File.Exists(target)){var stage=target+".tmp";try{File.Copy(source,stage,true);File.Move(stage,target);}finally{if(File.Exists(stage))File.Delete(stage);}}
   rows.RemoveAll(e=>e.id==hash);rows.Add(entry);Write(rows);return entry;
  }
  private static void Write(List<Entry> rows) {
   Directory.CreateDirectory(DirectoryPath);var target=Path.Combine(DirectoryPath,"index.json");var temp=target+".tmp";
   File.WriteAllText(temp,JsonUtility.ToJson(new Index{entries=rows},true));
   if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);
  }
  public static void Rename(Entry entry,string name){if(string.IsNullOrWhiteSpace(name))return;var rows=Read();var row=rows.FirstOrDefault(e=>e.id==entry.id);if(row!=null){row.name=name.Trim();Write(rows);}}
  public static void RemoveFromList(Entry entry){var rows=Read();rows.RemoveAll(e=>e.id==entry.id);Write(rows);}
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
