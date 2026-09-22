using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private Vector2 conversationLayoutSize;
        private int conversationLayoutState = -1;

        private void RefreshConversationLayout()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null || composerAttachButton == null) return;
            var size = parent.rect.size;
            var work = YuiChatRequestModes.IsWork(chatInteractionMode);
            var image = pendingVisionImageAttachment.HasImage;
            var state = (work ? 1 : 0) | (image ? 2 : 0);
            if (size == conversationLayoutSize && state == conversationLayoutState) return;
            conversationLayoutSize = size; conversationLayoutState = state;
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,0);
            var width = Mathf.Min(1080,size.x*.94f);
            var height = Mathf.Min(size.y*.78f,Mathf.Max(530,size.y*(work ? .49f : .34f)));
            rect.sizeDelta = new Vector2(width,height); rect.anchoredPosition = new Vector2(0,size.y*.035f);
            ConversationRect(transform.Find("ChatInteractionMode"),24,height-96,238,80);
            ConversationRect(statusText?.transform,282,height-100,width-548,88);
            ConversationRect(historyButton?.transform,width-238,height-96,120,80);
            ConversationRect(transform.Find("HideConsoleButton"),width-104,height-96,80,80);
            ConversationRect(composerAttachButton.transform,24,24,88,112);
            ConversationRect(inputField?.transform,126,24,width-382,112);
            ConversationRect(recordButton?.transform,width-242,24,96,112);
            ConversationRect(sendButton?.transform,width-132,24,108,112);
            ConversationRect(transform.Find("MicrophoneLevel"),24,143,width-48,5);
            var logBottom = image ? 238 : 158;
            ConversationRect(scrollRect?.transform,24,logBottom,width-48,height-logBottom-112);
            if (image)
            {
                ConversationRect(composerImagePreview?.transform,24,160,64,64);
                ConversationRect(composerRemoveAttachment?.transform,104,160,width-128,64);
            }
        }

        private static void ConversationRect(Transform target,float x,float y,float width,float height)
        {
            if (!(target is RectTransform rect)) return;
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;
            rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);
        }

        private void StyleConversationSurface()
        {
            YuiUiTheme.SurfaceOn(GetComponent<Image>(), YuiUiTheme.Surface);
            foreach (var button in new[] { composerAttachButton, composerRemoveAttachment, recordButton, historyButton, talkModeButton, workModeButton })
                YuiUiTheme.ButtonStyle(button);
            YuiUiTheme.ButtonStyle(sendButton, true);
            YuiToolbarIconUtility.ApplyCloseIcon(transform.Find("HideConsoleButton")?.GetComponent<Button>());
            YuiToolbarIconUtility.ApplyAttachmentIcon(composerAttachButton);
            if (inputField != null)
            {
                YuiControlAffordance.Input(inputField);
                inputField.caretColor = YuiUiTheme.Accent;
                inputField.customCaretColor = true;
                inputField.selectionColor = new Color(.6f,.5f,.85f,.4f);
                foreach (var text in inputField.GetComponentsInChildren<Text>(true))
                {
                    text.fontSize = YuiUiTypography.Body; text.resizeTextForBestFit = false;
                    text.color = text == inputField.textComponent ? YuiUiTheme.Text : YuiUiTheme.Muted;
                    text.rectTransform.offsetMin = new Vector2(18,12);
                    text.rectTransform.offsetMax = new Vector2(-18,-12);
                }
            }
            if (statusText != null) { statusText.fontSize = YuiUiTypography.Caption; statusText.color = YuiUiTheme.Muted; }
            if (scrollRect != null)
            {
                if (scrollRect.TryGetComponent<Image>(out var background)) background.color = Color.clear;
                if (scrollRect.viewport != null && scrollRect.viewport.TryGetComponent<Image>(out var viewport)) viewport.color = new Color(1,1,1,.01f);
            }
            if (composerMenuSurface != null)
            {
                YuiUiTheme.SurfaceOn(composerMenuSurface.GetComponent<Image>(), YuiUiTheme.Surface);
                foreach (var button in composerMenuSurface.GetComponentsInChildren<Button>(true)) YuiUiTheme.ButtonStyle(button);
            }
            var modeRoot = transform.Find("ChatInteractionMode") as RectTransform;
            if (modeRoot != null) Place(modeRoot,.035f,.875f,.30f,.975f);
            if (statusText != null) Place(statusText.rectTransform,.32f,.87f,.78f,.98f);
            var micLabel = transform.Find("MicrophoneDeviceText");
            if (micLabel != null) micLabel.gameObject.SetActive(false); // Full device name stays in Settings.
            UpdateChatInteractionModeUi();
            ConfigureConversationNavigation();
        }

        private void ConfigureConversationNavigation()
        {
            var close=transform.Find("HideConsoleButton")?.GetComponent<Button>();
            NavigationFor(talkModeButton,inputField,inputField,null,workModeButton);
            NavigationFor(workModeButton,inputField,inputField,talkModeButton,historyButton);
            NavigationFor(historyButton,workModeButton,inputField,workModeButton,close);
            NavigationFor(close,secretModeButton,sendButton,historyButton,null);
            NavigationFor(secretModeButton,close,close,close,close);
            NavigationFor(composerAttachButton,talkModeButton,talkModeButton,null,inputField);
            NavigationFor(inputField,workModeButton,talkModeButton,composerAttachButton,recordButton);
            NavigationFor(recordButton,workModeButton,workModeButton,inputField,sendButton);
            NavigationFor(sendButton,close,close,recordButton,null);
        }

        private void MoveComposerFocus(bool reverse)
        {
            var current=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            var modal = savedDataPanel != null ? savedDataPanel.transform : avatarLibraryPanel != null ? avatarLibraryPanel.transform : composerMenu != null && composerMenu.gameObject.activeSelf ? composerMenuSurface : null;
            if (modal != null)
            {
                var choices = System.Array.FindAll(modal.GetComponentsInChildren<Selectable>(), c => c.isActiveAndEnabled && c.IsInteractable());
                if (choices.Length == 0) return;
                var at = System.Array.FindIndex(choices, c => c.gameObject == current);
                choices[(at + (reverse ? -1 : 1) + choices.Length) % choices.Length].Select();
                return;
            }
            if(current==null) return;
            // Do not steal focus from Settings, Help, source links or other dialogs.
            var controls=new Selectable[]{talkModeButton,workModeButton,historyButton,composerAttachButton,inputField,recordButton,sendButton,transform.Find("HideConsoleButton")?.GetComponent<Button>(),secretModeButton};
            var index=System.Array.FindIndex(controls,c=>c!=null && c.gameObject==current);
            if(index<0) return;
            for(var step=1;step<=controls.Length;step++)
            {
                var next=controls[(index+(reverse ? -step : step)+controls.Length)%controls.Length];
                if(next!=null && next.isActiveAndEnabled && next.IsInteractable()) {next.Select();return;}
            }
        }

        private static void NavigationFor(Selectable control,Selectable up,Selectable down,Selectable left,Selectable right)
        {
            if(control!=null) control.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=up,selectOnDown=down,selectOnLeft=left,selectOnRight=right};
        }
    }
}
