using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.UI;
using YuiPhysicalAI.Avatar;
using System.Reflection;
using System.Threading;
namespace YuiPhysicalAI.Tests.Editor {
 public sealed class YuiComposerRegressionTests {
  [Test] public void ModelImportPreservesDraftAndBlocksCompetingComposerOperations(){
   var owner=new GameObject("IsolatedInactiveChat");owner.SetActive(false);var panel=owner.AddComponent<YuiChatPanel>();
   var inputOwner=new GameObject("Draft",typeof(RectTransform),typeof(InputField));
   var flags=BindingFlags.Instance|BindingFlags.NonPublic;var busy=typeof(YuiLocalModelMenu).GetField("importing",BindingFlags.Static|BindingFlags.NonPublic);var previous=busy.GetValue(null);
   try {
    var input=inputOwner.GetComponent<InputField>();input.text="Keep this draft";typeof(YuiChatPanel).GetField("inputField",flags).SetValue(panel,input);busy.SetValue(null,true);
    typeof(YuiChatPanel).GetMethod("SendCurrentInput",flags).Invoke(panel,null);
    Assert.That(input.text,Is.EqualTo("Keep this draft"));
    foreach(var method in new[]{"StartRecordingAsync","ImportImageAndAnalyzeFromPickerAsync","CaptureScreenAndAnalyzeAsync"}) {
     var task=(System.Threading.Tasks.Task)typeof(YuiChatPanel).GetMethod(method,flags).Invoke(panel,null);
     Assert.That(task.IsCompletedSuccessfully,Is.True,method+" must stop before opening another native operation");
    }
   }finally{busy.SetValue(null,previous);Object.DestroyImmediate(owner);Object.DestroyImmediate(inputOwner);}
  }
  [Test] public void AssetCompletionDefersRuntimeReplacementDuringActiveChat(){
   var owner=new GameObject("IsolatedInactiveChat");owner.SetActive(false);var panel=owner.AddComponent<YuiChatPanel>();
   try {
    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
    typeof(YuiChatPanel).GetField("isSending",flags).SetValue(panel,true);
    panel.RefreshLocalAiRuntimeAfterAssetInstall();
    Assert.That(typeof(YuiChatPanel).GetField("pendingLocalAiAssetRefresh",flags).GetValue(panel),Is.True);
    Assert.That(typeof(YuiChatPanel).GetField("aiRuntimeRouter",flags).GetValue(panel),Is.Null);
    typeof(YuiChatPanel).GetMethod("ApplyPendingLocalAiAssetRefresh",flags).Invoke(panel,null);
    Assert.That(typeof(YuiChatPanel).GetField("aiRuntimeRouter",flags).GetValue(panel),Is.Null);
   }finally{Object.DestroyImmediate(owner);}
  }
  [Test] public void ActiveChatKeepsItsStopButtonAvailableWhenOtherControlsAreDisabled(){
   var owner=new GameObject("IsolatedInactiveChat");owner.SetActive(false);var panel=owner.AddComponent<YuiChatPanel>();
   var buttonOwner=new GameObject("Send",typeof(RectTransform),typeof(Button));var labelOwner=new GameObject("Label",typeof(RectTransform),typeof(Text));
   using var operation=new CancellationTokenSource();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   try{
    typeof(YuiChatPanel).GetField("sendButton",flags).SetValue(panel,buttonOwner.GetComponent<Button>());
    typeof(YuiChatPanel).GetField("sendButtonText",flags).SetValue(panel,labelOwner.GetComponent<Text>());
    typeof(YuiChatPanel).GetField("isSending",flags).SetValue(panel,true);
    typeof(YuiChatPanel).GetField("activeChatCancellation",flags).SetValue(panel,operation);
    typeof(YuiChatPanel).GetMethod("SetInteractable",flags).Invoke(panel,new object[]{false});
    Assert.IsTrue(buttonOwner.GetComponent<Button>().interactable,"The active request must remain stoppable.");
    Assert.AreEqual(YuiUiLocalization.Text("Stop"),labelOwner.GetComponent<Text>().text);
   }finally{typeof(YuiChatPanel).GetField("activeChatCancellation",flags).SetValue(panel,null);Object.DestroyImmediate(owner);Object.DestroyImmediate(buttonOwner);Object.DestroyImmediate(labelOwner);}
  }
  [Test] public void RebindingClonedBubbleDoesNotDuplicateSaveAction(){
   var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");var template=YuiChatMessageBubble.CreateTemplate(font,null);var clone=Object.Instantiate(template);
   try{for(var i=0;i<3;i++)clone.Bind("Yui","result",360,false,Color.black,Color.white,Color.white,font,null);
    Assert.AreEqual(1,clone.GetComponentsInChildren<Button>(true).Count(b=>b.name=="SaveButton"));
   }finally{Object.DestroyImmediate(clone.gameObject);Object.DestroyImmediate(template.gameObject);}
  }
  [TestCase("../outside.vrm")][TestCase("nested/avatar.zip")][TestCase("C:\\private.vrm")][TestCase("..")]
  public void LibraryRejectsPathsOutsideItsDirectory(string file){Assert.Throws<System.ArgumentException>(()=>YuiAvatarLibrary.Resolve(new YuiAvatarLibrary.Entry{file=file}));}
  [Test]public void ConsumedAttachmentNoLongerAppearsInComposer(){var state=new YuiPendingVisionImageAttachment();state.SetImageDataUrl("data:image/png;base64,AA==");Assert.IsTrue(state.HasImage);state.MarkConsumedAfterSuccessfulChat();Assert.IsFalse(state.HasImage);}
 }
}
