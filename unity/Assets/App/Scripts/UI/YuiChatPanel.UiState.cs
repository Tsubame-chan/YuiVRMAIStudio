using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private void SetStatus(string status)
        {
            currentStatus = string.IsNullOrWhiteSpace(status) ? "Ready" : status;
            RenderStatus();
        }

        private void RenderStatus()
        {
            if (statusText == null)
            {
                return;
            }

            statusText.supportRichText = true;
            statusText.color = Color.white;
            statusText.alignment = TextAnchor.MiddleLeft;
            var modeLabel = YuiConversationModes.StatusLabel(conversationMode);
            var modePrefix = string.IsNullOrEmpty(modeLabel)
                ? string.Empty
                : $"<color=#f5c542><b>{modeLabel}</b></color>\n";
            statusText.text = secretMode
                ? $"{modePrefix}<b>Secret Mode</b>\n{currentStatus}"
                : $"{modePrefix}{currentStatus}";
            if (!string.IsNullOrWhiteSpace(appContextStatus))
            {
                statusText.text += $"\n<color=#a8c7ff>{appContextStatus}</color>";
            }
        }

        private void SetInteractable(bool interactable)
        {
            if (sendButton != null)
            {
                sendButton.interactable = interactable;
            }

            if (sendButtonText != null)
            {
                sendButtonText.text = interactable ? "Send" : "...";
            }

            if (recordButton != null)
            {
                recordButton.interactable = interactable || isRecording;
            }

            if (lookButton != null)
            {
                lookButton.interactable = interactable;
            }

            if (importImageButton != null)
            {
                importImageButton.interactable = interactable;
            }

            if (inputField != null)
            {
                inputField.interactable = interactable;
            }
        }

        private void SetRecordButtonText(string text)
        {
            if (recordButtonText != null)
            {
                recordButtonText.text = text;
            }
        }

        private void SetLookButtonText(string text)
        {
            if (lookButtonText != null)
            {
                lookButtonText.text = text;
            }
        }

        private void SetImportImageButtonText(string text)
        {
            if (importImageButtonText != null)
            {
                importImageButtonText.text = text;
            }
        }

        private void SetMicrophoneDeviceText(string text)
        {
            if (microphoneDeviceText != null)
            {
                microphoneDeviceText.text = text;
            }
        }

        private void UpdateMicrophoneLevel(float value)
        {
            if (microphoneLevelFill == null)
            {
                return;
            }

            var target = Mathf.Clamp01(value);
            var deltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            var speed = target > displayedMicrophoneLevel ? 18f : 4f;
            displayedMicrophoneLevel = Mathf.MoveTowards(displayedMicrophoneLevel, target, speed * deltaTime);
            microphoneLevelFill.fillAmount = displayedMicrophoneLevel;
            var rect = microphoneLevelFill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(displayedMicrophoneLevel, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void DisableUnstableRuntimePresenceAnimator()
        {
            var presence = GetComponent<YuiPresenceAnimator>();
            if (presence != null)
            {
                presence.enabled = false;
            }

            if (avatarController != null)
            {
                avatarController.SetPresenceAnimator(null);
            }
        }

        private void ApplyReadableFont()
        {
            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Meiryo", "Yu Gothic", "MS Gothic", "Arial" },
                20);

            if (font == null)
            {
                return;
            }

            foreach (var text in GetComponentsInChildren<Text>(true))
            {
                text.font = font;
            }
        }
    }
}
