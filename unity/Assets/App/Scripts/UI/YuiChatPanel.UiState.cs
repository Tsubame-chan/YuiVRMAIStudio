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
            var modeLabel = YuiUiLocalization.Text(YuiConversationModes.StatusLabel(conversationMode));
            // Privacy and connectivity are independent; keep connectivity on its own line.
            var heading = modeLabel + (secretMode ? (string.IsNullOrEmpty(modeLabel) ? "" : " · ") + "Secret Mode" : "");
            var displayedStatus = currentStatus;
            if (!isSending && !isRecording && conversationMode == YuiConversationModes.DirectOpenAi
                && string.IsNullOrWhiteSpace(openAiApiKey))
                displayedStatus = string.IsNullOrEmpty(YuiApiKeyStore.LastError) ? "API key required" : "Unlock API key in Settings";
            statusText.text = (string.IsNullOrEmpty(heading) ? "" : $"<color=#f5c542><b>{heading}</b></color>\n")
                + YuiUiLocalization.Text(displayedStatus);
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
                YuiUiLocalization.Set(sendButtonText,interactable ? "Send" : "...");
            }

            if (recordButton != null)
            {
                recordButton.interactable = (interactable || isRecording) && !(isRecording && IsRealtimeConversationMode());
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
                inputField.interactable = true;
            }
        }

        private void SetRecordButtonText(string text)
        {
            if (recordButtonText != null)
            {
                recordButtonText.resizeTextForBestFit = true;
                recordButtonText.resizeTextMinSize = 16;
                recordButtonText.resizeTextMaxSize = YuiUiTypography.Button;
                YuiUiLocalization.Set(recordButtonText,isRecording ? (IsRealtimeConversationMode() ? "Mic" : "Cancel") : text);
            }
        }

        private void SetLookButtonText(string text)
        {
            if (lookButtonText != null)
            {
                YuiUiLocalization.Set(lookButtonText,text);
            }
        }

        private void SetImportImageButtonText(string text)
        {
            if (importImageButtonText != null)
            {
                YuiUiLocalization.Set(importImageButtonText,text);
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
            // The app Canvas contains Settings/Help as siblings of the chat panel.
            YuiUiTypography.Apply(GetComponentInParent<Canvas>()?.rootCanvas.transform ?? transform);
        }
    }
}
