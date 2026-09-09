#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
namespace YuiPhysicalAI.EditorTools {
 public static class YuiAvatarFileSharingPostprocessor {
  [PostProcessBuild(210)]
  public static void EnableFiles(BuildTarget target,string output) {
   if(target!=BuildTarget.iOS)return;
   var path=Path.Combine(output,"Info.plist");var plist=new PlistDocument();plist.ReadFromFile(path);
   plist.root.SetBoolean("UIFileSharingEnabled",true);
   plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace",true);
   plist.WriteToFile(path);
  }
 }
}
#endif
