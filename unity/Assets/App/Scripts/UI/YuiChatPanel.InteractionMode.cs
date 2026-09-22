using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private static readonly Color ActiveModeColor = YuiUiTheme.Selected;
        private static readonly Color InactiveModeColor = YuiUiTheme.Field;

        private void EnsureChatInteractionModeControls()
        {
            var modeRoot = transform.Find("ChatInteractionMode") as RectTransform;
            if (modeRoot == null)
            {
                var modeObject = new GameObject("ChatInteractionMode", typeof(RectTransform));
                modeObject.transform.SetParent(transform, false);
                modeRoot = modeObject.GetComponent<RectTransform>();
            }

            modeRoot.anchorMin = new Vector2(0.04f, 0.865f);
            modeRoot.anchorMax = new Vector2(0.205f, 0.975f);
            modeRoot.offsetMin = Vector2.zero;
            modeRoot.offsetMax = Vector2.zero;

            talkModeButton = EnsureModeButton(modeRoot, "TalkModeButton", "Talk", false, out talkModeButtonText);
            workModeButton = EnsureModeButton(modeRoot, "WorkModeButton", "Work", true, out workModeButtonText);

            if (statusText != null)
            {
                var statusRect = statusText.rectTransform;
                statusRect.anchorMin = new Vector2(0.225f, 0.86f);
                statusRect.anchorMax = new Vector2(0.42f, 0.98f);
                statusRect.offsetMin = Vector2.zero;
                statusRect.offsetMax = Vector2.zero;
            }
        }

        private Button EnsureModeButton(
            RectTransform parent,
            string objectName,
            string label,
            bool rightHalf,
            out Text labelText)
        {
            var buttonTransform = parent.Find(objectName) as RectTransform;
            if (buttonTransform == null)
            {
                var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(parent, false);
                buttonTransform = buttonObject.GetComponent<RectTransform>();
            }

            buttonTransform.anchorMin = new Vector2(rightHalf ? 0.5f : 0f, 0f);
            buttonTransform.anchorMax = new Vector2(rightHalf ? 1f : 0.5f, 1f);
            buttonTransform.offsetMin = new Vector2(rightHalf ? 2f : 0f, 0f);
            buttonTransform.offsetMax = new Vector2(rightHalf ? 0f : -2f, 0f);

            var image = buttonTransform.GetComponent<Image>() ?? buttonTransform.gameObject.AddComponent<Image>();
            var button = buttonTransform.GetComponent<Button>() ?? buttonTransform.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var labelTransform = buttonTransform.Find("Label") as RectTransform;
            if (labelTransform == null)
            {
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(buttonTransform, false);
                labelTransform = labelObject.GetComponent<RectTransform>();
            }

            labelTransform.anchorMin = Vector2.zero;
            labelTransform.anchorMax = Vector2.one;
            labelTransform.offsetMin = Vector2.zero;
            labelTransform.offsetMax = Vector2.zero;

            labelText = labelTransform.GetComponent<Text>();
            YuiUiLocalization.Set(labelText,label);
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.fontSize = 15;
            labelText.color = Color.white;
            labelText.raycastTarget = false;
            if (statusText != null && statusText.font != null)
            {
                labelText.font = statusText.font;
            }

            return button;
        }

        private void SetChatInteractionMode(string mode)
        {
            if (isSending || isRecording || realtimeStreamActive)
            { SetStatus("Stop the conversation before changing modes.");return; }
            chatInteractionMode = YuiChatRequestModes.Normalize(mode);
            _=RestoreConversationViewAsync();
            PlayerPrefs.SetString(ChatInteractionModeKey, chatInteractionMode);
            PlayerPrefs.Save();
            UpdateChatInteractionModeUi();
            ApplyPrimaryCommandLabels();
            SetStatus(YuiChatRequestModes.IsWork(chatInteractionMode) ? "Work mode" : "Talk mode");
        }

        private void SelectTalkMode()
        {
            SetChatInteractionMode(YuiChatRequestModes.Talk);
        }

        private void SelectWorkMode()
        {
            SetChatInteractionMode(YuiChatRequestModes.Work);
        }

        private void UpdateChatInteractionModeUi()
        {
            var workMode = YuiChatRequestModes.IsWork(chatInteractionMode);
            SetModeButtonVisual(talkModeButton, talkModeButtonText, !workMode);
            SetModeButtonVisual(workModeButton, workModeButtonText, workMode);
        }

        private static void SetModeButtonVisual(Button button, Text label, bool active)
        {
            if (button != null && button.targetGraphic is Image image)
            {
                image.color = active ? ActiveModeColor : InactiveModeColor;
            }

            if (label != null)
            {
                label.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                label.color = active ? YuiUiTheme.Accent : YuiUiTheme.Muted;
            }
        }

        private void ApplyPrimaryCommandLabels()
        {
            SetImportImageButtonText("Image");
            SetLookButtonText("Camera");
            if (!isRecording)
            {
                SetRecordButtonText("Mic");
            }
            if (sendButtonText != null)
            {
                YuiUiLocalization.Set(sendButtonText,"Send");
            }

            if (inputField?.placeholder is Text placeholder)
            {
                YuiUiLocalization.Set(placeholder,YuiChatRequestModes.IsWork(chatInteractionMode) ? "Describe the task" : "Say something");
            }
        }
    }
}
