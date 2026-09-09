using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;
namespace YuiPhysicalAI.UI {
 public sealed partial class YuiChatPanel {
  private RectTransform composerMenu;
  private Text composerAttachmentLabel;
  private Button composerRemoveAttachment;
  private Button composerAttachButton;
  private Button composerStopButton;
  private static void Place(RectTransform r,float x0,float y0,float x1,float y1) {
   r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;
  }
  private Button ComposerButton(Transform parent,string name,string label,UnityEngine.Events.UnityAction action,float x0,float y0,float x1,float y1) {
   var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
   go.GetComponent<Image>().color=new Color(.16f,.2f,.29f,.98f);
   var button=go.GetComponent<Button>();button.onClick.AddListener(action);Place(go.GetComponent<RectTransform>(),x0,y0,x1,y1);
   var textGo=new GameObject("Label",typeof(RectTransform),typeof(Text));textGo.transform.SetParent(go.transform,false);
   var text=textGo.GetComponent<Text>();text.font=statusText!=null?statusText.font:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text=label;text.fontSize=20;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.resizeTextForBestFit=true;text.resizeTextMinSize=14;text.resizeTextMaxSize=20;Place(text.rectTransform,.02f,.02f,.98f,.98f);
   return button;
  }
  private void EnsureUnifiedComposer() {
   if(composerAttachButton!=null)return;
   // Keep the existing real handlers; consolidate infrequent actions in one surface.
   if(importImageButton!=null)importImageButton.gameObject.SetActive(false);
   if(lookButton!=null)lookButton.gameObject.SetActive(false);
   if(inputField!=null){Place(inputField.GetComponent<RectTransform>(),.155f,.035f,.73f,.21f);inputField.lineType=InputField.LineType.MultiLineNewline;}
   if(recordButton!=null)Place(recordButton.GetComponent<RectTransform>(),.745f,.035f,.845f,.21f);
   if(sendButton!=null)Place(sendButton.GetComponent<RectTransform>(),.86f,.035f,.97f,.21f);
   if(scrollRect!=null){Place(scrollRect.GetComponent<RectTransform>(),.03f,.36f,.97f,.84f);scrollRect.horizontal=false;if(scrollRect.horizontalScrollbar!=null)scrollRect.horizontalScrollbar.gameObject.SetActive(false);}
   composerAttachButton=ComposerButton(transform,"ComposerAttach","添付",()=>composerMenu.gameObject.SetActive(!composerMenu.gameObject.activeSelf),.03f,.035f,.14f,.21f);
   composerStopButton=ComposerButton(transform,"ComposerStop","音声を止める",()=>{StopRealtimeAudioPlayback();SetStatus("音声を停止しました");},.73f,.245f,.97f,.335f);
   composerRemoveAttachment=ComposerButton(transform,"ComposerAttachment","",()=>{pendingVisionImageAttachment.MarkConsumedAfterSuccessfulChat();latestVision=null;UpdateComposerState();},.03f,.245f,.71f,.335f);
   composerAttachmentLabel=composerRemoveAttachment.GetComponentInChildren<Text>();
   var menu=new GameObject("ComposerMenu",typeof(RectTransform),typeof(Image));menu.transform.SetParent(transform,false);composerMenu=menu.GetComponent<RectTransform>();Place(composerMenu,.03f,.36f,.97f,.84f);menu.GetComponent<Image>().color=new Color(.055f,.07f,.11f,.99f);
   ComposerButton(menu.transform,"AttachImage","画像を選ぶ",()=>{menu.SetActive(false);ImportImageAndAnalyze();},.04f,.69f,.48f,.94f);
   ComposerButton(menu.transform,"AttachCamera","カメラで撮る",()=>{menu.SetActive(false);CaptureScreenAndAnalyze();},.52f,.69f,.96f,.94f);
   ComposerButton(menu.transform,"AvatarLibrary","アバター一覧",()=>{menu.SetActive(false);ShowAvatarLibrary();},.04f,.37f,.48f,.62f);
   ComposerButton(menu.transform,"ImportAvatar","VRM / ZIPを追加",()=>{menu.SetActive(false);ImportCustomVrmFromFilePicker();},.52f,.37f,.96f,.62f);
   ComposerButton(menu.transform,"ImportGuide","Unity・スマホ導入ガイド",()=>Application.OpenURL("https://github.com/Tsubame-chan/YuiVRMAIStudio/blob/main/docs/AVATAR_IMPORT.md"),.04f,.06f,.73f,.3f);
   ComposerButton(menu.transform,"CloseMenu","閉じる",()=>menu.SetActive(false),.77f,.06f,.96f,.3f);menu.SetActive(false);UpdateComposerState();
  }
  private void UpdateComposerState() {
   if(composerAttachButton==null)return;
   composerAttachButton.interactable=!isSending;
   composerStopButton.interactable=audioSource!=null && audioSource.isPlaying;
   var hasImage=pendingVisionImageAttachment.HasImage;
   composerRemoveAttachment.interactable=hasImage && !isSending;
   composerAttachmentLabel.text=hasImage ? "画像を添付中 · 外す" : (isRecording ? "聞き取り中" : isSending ? "返答を準備しています…" : "画像やアバターは「添付」から");
  }
  private void LateUpdate(){UpdateComposerState();}
 }
}
