using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.UI;
using YuiPhysicalAI.Avatar;
namespace YuiPhysicalAI.Tests.Editor {
 public sealed class YuiComposerRegressionTests {
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
