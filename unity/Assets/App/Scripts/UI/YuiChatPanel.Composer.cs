using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;
namespace YuiPhysicalAI.UI {
 public sealed partial class YuiChatPanel {
  private RectTransform composerMenu;
  private RectTransform composerMenuSurface;
  private readonly Vector3[] composerAnchorCorners = new Vector3[4];
  private Text composerAttachmentLabel;
  private Button composerRemoveAttachment;
  private Button composerAttachButton;
  private Button historyButton;
  private RawImage composerImagePreview;
  private string previewDataUrl;
  private int composerUiState = -1;
  private void UpdateAttachmentPreview() {
   var data=pendingVisionImageAttachment.ImageDataUrl;
   if(data==previewDataUrl)return;
   previewDataUrl=data;
   if(composerImagePreview.texture!=null)Destroy(composerImagePreview.texture);
   composerImagePreview.texture=null;composerImagePreview.gameObject.SetActive(false);
   if(string.IsNullOrEmpty(data))return;
   Texture2D texture=null;
   try {
    texture=new Texture2D(2,2);
    if(!texture.LoadImage(Convert.FromBase64String(data.Substring(data.IndexOf(',')+1)))){Destroy(texture);return;}
    composerImagePreview.texture=texture;composerImagePreview.GetComponent<YuiOwnedPreviewTexture>().Texture=texture;
    composerImagePreview.gameObject.SetActive(true);
   } catch(Exception) {if(texture!=null)Destroy(texture);}
  }
  private static void Place(RectTransform r,float x0,float y0,float x1,float y1) {
   r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;
  }
  private Button ComposerButton(Transform parent,string name,string label,UnityEngine.Events.UnityAction action,float x0,float y0,float x1,float y1,bool localize=true) {
   var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
   go.GetComponent<Image>().color=new Color(.16f,.2f,.29f,.98f);
   var button=go.GetComponent<Button>();button.onClick.AddListener(action);Place(go.GetComponent<RectTransform>(),x0,y0,x1,y1);
   var textGo=new GameObject("Label",typeof(RectTransform),typeof(Text));textGo.transform.SetParent(go.transform,false);
   var text=textGo.GetComponent<Text>();text.supportRichText=false;text.font=YuiUiTypography.Regular;if(localize)YuiUiLocalization.Set(text,label);else text.text=label;text.fontSize=YuiUiTypography.Button;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.resizeTextForBestFit=false;Place(text.rectTransform,.02f,.02f,.98f,.98f);
   YuiUiTheme.ButtonStyle(button,name=="Save" || name=="Continue");
   if(name=="Title" || name=="Count") YuiUiTheme.SurfaceOn(go.GetComponent<Image>(),YuiUiTheme.Surface);
   return button;
  }
  private static void ForegroundModal(GameObject root) {
   var canvas=root.GetComponent<Canvas>();
   if(canvas==null)canvas=root.AddComponent<Canvas>();
   canvas.overrideSorting=true;canvas.sortingOrder=6100;
   if(root.GetComponent<GraphicRaycaster>()==null)root.AddComponent<GraphicRaycaster>();
  }
  private void FocusModal(Transform root) { StartCoroutine(FocusModalAfterLayout(root)); }
  private System.Collections.IEnumerator FocusModalAfterLayout(Transform root) {
   yield return null;
   if(root==null || !root.gameObject.activeInHierarchy || UnityEngine.EventSystems.EventSystem.current==null)yield break;
   foreach(var field in root.GetComponentsInChildren<InputField>()) {
    if(!field.interactable || !field.isActiveAndEnabled)continue;
    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(field.gameObject);
    field.ActivateInputField();yield break;
   }
   foreach(var button in root.GetComponentsInChildren<Button>()) {
    if(!button.interactable || !button.isActiveAndEnabled)continue;
    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);yield break;
   }
  }
  private void EnsureUnifiedComposer() {
   if(composerAttachButton!=null)return;
   // Attachment actions only. Character management and history have their own places.
   if(importImageButton!=null)importImageButton.gameObject.SetActive(false);
   if(lookButton!=null)lookButton.gameObject.SetActive(false);
   if(inputField!=null){Place(inputField.GetComponent<RectTransform>(),.155f,.035f,.73f,.21f);inputField.lineType=InputField.LineType.MultiLineNewline;}
   if(recordButton!=null)Place(recordButton.GetComponent<RectTransform>(),.745f,.035f,.845f,.21f);
   if(sendButton!=null)Place(sendButton.GetComponent<RectTransform>(),.86f,.035f,.97f,.21f);
   if(scrollRect!=null){Place(scrollRect.GetComponent<RectTransform>(),.03f,.36f,.97f,.84f);scrollRect.horizontal=false;if(scrollRect.horizontalScrollbar!=null)scrollRect.horizontalScrollbar.gameObject.SetActive(false);}
   composerAttachButton=ComposerButton(transform,"ComposerAttach","",()=>{ if(Application.isMobilePlatform) ImportImageAndAnalyze(); else SetAttachmentMenuVisible(!composerMenu.gameObject.activeSelf); },.03f,.035f,.14f,.21f);
   var icon=new GameObject("Paperclip",typeof(RectTransform),typeof(YuiAttachmentIcon));
   icon.transform.SetParent(composerAttachButton.transform,false);
   Place(icon.GetComponent<RectTransform>(),.14f,.14f,.86f,.86f);icon.GetComponent<YuiAttachmentIcon>().raycastTarget=false;
   composerRemoveAttachment=ComposerButton(transform,"ComposerAttachment","",()=>{pendingVisionImageAttachment.MarkConsumedAfterSuccessfulChat();latestVision=null;UpdateComposerState();},.03f,.245f,.71f,.335f);
   composerAttachmentLabel=composerRemoveAttachment.GetComponentInChildren<Text>();
   var preview=new GameObject("AttachmentPreview",typeof(RectTransform),typeof(RawImage),typeof(YuiOwnedPreviewTexture));
   preview.transform.SetParent(transform,false);composerImagePreview=preview.GetComponent<RawImage>();composerImagePreview.raycastTarget=false;
   Place(preview.GetComponent<RectTransform>(),.03f,.245f,.13f,.335f);preview.SetActive(false);
   var menu=new GameObject("ComposerMenu",typeof(RectTransform),typeof(Image),typeof(Button));menu.transform.SetParent(GetComponentInParent<Canvas>().rootCanvas.transform,false);composerMenu=menu.GetComponent<RectTransform>();Place(composerMenu,0,0,1,1);
   menu.GetComponent<Image>().color=Color.clear;ForegroundModal(menu);
   menu.GetComponent<Button>().onClick.AddListener(()=>SetAttachmentMenuVisible(false));
   menu.GetComponent<Button>().navigation=new Navigation {mode=Navigation.Mode.None};
   var surface=new GameObject("AttachmentActions",typeof(RectTransform),typeof(Image));surface.transform.SetParent(menu.transform,false);
   composerMenuSurface=surface.GetComponent<RectTransform>();YuiUiTheme.SurfaceOn(surface.GetComponent<Image>(),YuiUiTheme.Surface);
   var title=ComposerButton(surface.transform,"Title","Attach image",()=>{},.045f,.755f,.79f,.95f);title.interactable=false;
   title.GetComponentInChildren<Text>().alignment=TextAnchor.MiddleLeft;
   var close=ComposerButton(surface.transform,"CloseMenu","Close",()=>SetAttachmentMenuVisible(false),.81f,.755f,.955f,.95f);
   YuiToolbarIconUtility.ApplyCloseIcon(close);
   ComposerButton(surface.transform,"AttachImage","Choose image",()=>{menu.SetActive(false);ImportImageAndAnalyze();},.045f,.40f,.955f,.70f);
   ComposerButton(surface.transform,"AttachCamera","Use camera",()=>{menu.SetActive(false);CaptureScreenAndAnalyze();},.045f,.065f,.955f,.365f);
   historyButton=ComposerButton(transform,"HistoryButton","Log",OpenHistory,.7f,.875f,.84f,.975f);
   menu.SetActive(false);UpdateComposerState();
  }
  private void SetAttachmentMenuVisible(bool visible) {
   composerMenu.gameObject.SetActive(visible);
   if(visible) { PositionAttachmentMenu(); FocusModal(composerMenuSurface); }
   else FocusDesktopComposer();
  }
  private void PositionAttachmentMenu() {
   if(composerMenuSurface==null || !composerMenu.gameObject.activeSelf)return;
   var bounds=composerMenu.rect;
   composerAttachButton.GetComponent<RectTransform>().GetWorldCorners(composerAnchorCorners);
   var min=composerMenu.InverseTransformPoint(composerAnchorCorners[0]);
   var max=composerMenu.InverseTransformPoint(composerAnchorCorners[2]);
   var area=YuiPopupPlacement.Above(bounds,Rect.MinMaxRect(min.x,min.y,max.x,max.y),new Vector2(450,284));
   composerMenuSurface.anchorMin=composerMenuSurface.anchorMax=Vector2.zero;composerMenuSurface.pivot=Vector2.zero;
   composerMenuSurface.anchoredPosition=area.position-bounds.min;composerMenuSurface.sizeDelta=area.size;
  }
  private void UpdateComposerState() {
   if(composerAttachButton==null)return;
   var hasImage=pendingVisionImageAttachment.HasImage;
   var importing=runtimeVrmImporter != null && runtimeVrmImporter.IsImporting;
   var stoppable=HasStoppableComposerOperation;
   var state=(isSending?1:0)|(hasImage?2:0)|(importing?4:0)|(stoppable?8:0)|(isRecording?16:0);
   if(state==composerUiState && previewDataUrl==pendingVisionImageAttachment.ImageDataUrl)return;
   composerUiState=state;
   composerAttachButton.interactable=!isSending && (runtimeVrmImporter == null || !runtimeVrmImporter.IsImporting);
   if(sendButton!=null)sendButton.interactable=HasStoppableComposerOperation || !isSending;
   if(sendButtonText!=null)YuiUiLocalization.Set(sendButtonText,isRecording ? (IsRealtimeConversationMode() ? "Finish" : "Send") : HasStoppableComposerOperation ? "Stop" : isSending ? "..." : "Send");
   UpdateAttachmentPreview();
   composerRemoveAttachment.interactable=hasImage && !isSending;
   composerRemoveAttachment.gameObject.SetActive(hasImage);
   YuiUiLocalization.Set(composerAttachmentLabel,"Remove image");
   conversationLayoutState=-1;
  }
  private bool HasStoppableComposerOperation => (runtimeVrmImporter != null && runtimeVrmImporter.IsImporting) || activeChatCancellation != null || activeVoiceCancellation != null || isRecording
   || realtimeSocket != null || realtimeStreamActive || realtimeWaitingForResponse
   || realtimeVoicevoxSpeechActive || (audioSource != null && audioSource.isPlaying);
  private void StopComposerOperation() {
   if (!HasStoppableComposerOperation) return;
   runtimeVrmImporter?.CancelImport();
   activeChatCancellation?.Cancel();
   activeVoiceCancellation?.Cancel();
   realtimeTranslateCancellation?.Cancel();
   isRecording=false;
   var stoppedClip=unityMicrophoneRecorder!=null ? unityMicrophoneRecorder.Stop().Clip : null;
   DestroyOwnedAudioClip(stoppedClip,null);
   if(recordingClip!=stoppedClip)DestroyOwnedAudioClip(recordingClip,null);
   StopMacEditorMicrophoneFallback();
   recordingClip=null;
   realtimeWaitingForResponse=false;
   StopRealtimeAudioPlayback();
   _ = CloseRealtimeStreamAsync();
   ClearPendingLine();UpdateMicrophoneLevel(0f);SetRecordButtonText("Mic");
   SetInteractable(!isSending);SetStatus(activeChatCancellation != null ? "停止しています…" : "停止しました");
  }
  private void LateUpdate(){UpdateComposerState();RefreshConversationLayout();PositionAttachmentMenu();}
 }
}
