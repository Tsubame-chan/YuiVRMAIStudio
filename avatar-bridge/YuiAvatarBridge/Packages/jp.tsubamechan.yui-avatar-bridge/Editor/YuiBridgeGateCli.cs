using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Yui.AvatarBridge.Editor {
 public static class YuiBridgeGateCli {
  public static void ExportRobot() {
   EditorSceneManager.OpenScene("Packages/com.vrchat.avatars/Samples/Dynamics/Robot Avatar/Avatar Dynamics Robot Avatar PC.unity",OpenSceneMode.Single);
   GameObject root=null;
   foreach(var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) {
    if(YuiAvatarBridgeAnalyzer.Analyze(go).Descriptor!=null){root=go;break;}
   }
   if(root==null)throw new Exception("Robot not found");
   var analysis=YuiAvatarBridgeAnalyzer.Analyze(root);
   var output=Environment.GetEnvironmentVariable("YUI_BRIDGE_GATE_OUTPUT");
   YuiAvatarBridgeExporter.Export(analysis,new YuiAvatarExportOptions{OutputPath=output,BuildWindows=true,BuildMacOS=true,BuildAndroid=false,BuildIOS=false,RightsAcknowledged=true});
   Debug.Log("BRIDGE GATE exported "+output);
  }
  public static void ExportPrefab() {
   var path=Environment.GetEnvironmentVariable("YUI_BRIDGE_GATE_PREFAB");
   var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
   if(root==null)throw new ArgumentException("Avatar prefab not found: "+path);
   YuiAvatarBridgeExporter.Export(YuiAvatarBridgeAnalyzer.Analyze(root),new YuiAvatarExportOptions{
    OutputPath=Environment.GetEnvironmentVariable("YUI_BRIDGE_GATE_OUTPUT"),BuildWindows=true,BuildMacOS=true,
    BuildAndroid=false,BuildIOS=false,RightsAcknowledged=true});
  }
  public static void Cube() {
   var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
   var analysis=new YuiAvatarAnalysis{Root=go};
   try {YuiAvatarBridgeExporter.Export(analysis,new YuiAvatarExportOptions{OutputPath=Environment.GetEnvironmentVariable("YUI_BRIDGE_GATE_OUTPUT"),BuildWindows=false,BuildMacOS=true,BuildAndroid=false,BuildIOS=false,RightsAcknowledged=true});}
   finally{UnityEngine.Object.DestroyImmediate(go);}
  }
 }
}
