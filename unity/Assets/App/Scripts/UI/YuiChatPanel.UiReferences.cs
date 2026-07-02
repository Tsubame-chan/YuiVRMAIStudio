using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.Audio;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private void EnsureUiReferences()
        {
            if (inputField == null)
            {
                inputField = GetComponentInChildren<InputField>(true);
            }

            if (scrollRect == null)
            {
                scrollRect = GetComponentInChildren<ScrollRect>(true);
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
            if (audioSource != null)
            {
                audioSource.volume = PlayerPrefs.GetFloat(VoiceVolumeKey, audioSource.volume);
            }

            ResolveAvatarReferences();
            ResolveButtonReferences();
            ResolveTextReferences();
            NormalizeLogView();
            EnsureChatLogView();
        }

        private void ResolveAvatarReferences()
        {
            if (avatarController == null)
            {
                avatarController = GetComponentInChildren<YuiAvatarController>(true);
            }

            if (chatdollKitController == null)
            {
                chatdollKitController = GetComponentInChildren<YuiChatdollKitController>(true);
            }

            if (chatdollKitVoicevoxTts == null)
            {
                chatdollKitVoicevoxTts = GetComponent<YuiChatdollVoicevoxTts>();
            }
            if (chatdollKitVoicevoxTts != null)
            {
                ConfigureChatdollKitVoicevoxTts();
            }

            if (avatarController != null && audioSource != null)
            {
                avatarController.SetSpeechAudioSource(audioSource);
            }
            DisableUnstableRuntimePresenceAnimator();

            if (chatdollKitController != null && audioSource != null)
            {
                chatdollKitController.SetSpeechAudioSource(audioSource);
            }
        }

        private void ResolveButtonReferences()
        {
            if (sendButton == null)
            {
                var sendTransform = UiTreeUtility.FindDeepChild(transform, "SendButton");
                sendButton = sendTransform != null
                    ? sendTransform.GetComponent<Button>()
                    : GetComponentInChildren<Button>(true);
            }

            if (recordButton == null)
            {
                recordButton = FindButton("RecordButton");
            }

            if (lookButton == null)
            {
                lookButton = FindButton("LookButton");
            }

            if (importImageButton == null)
            {
                importImageButton = FindButton("ImportImageButton");
            }

            if (secretModeButton == null)
            {
                secretModeButton = FindButton("SecretModeButton");
            }
        }

        private void ResolveTextReferences()
        {
            if (sendButtonText == null)
            {
                sendButtonText = FindText("Label");
            }

            if (recordButtonText == null)
            {
                recordButtonText = FindText("RecordLabel");
            }

            if (lookButtonText == null)
            {
                lookButtonText = FindText("LookLabel");
            }

            if (importImageButtonText == null)
            {
                importImageButtonText = FindText("ImportImageLabel");
            }

            if (secretModeButtonText == null)
            {
                secretModeButtonText = FindText("SecretModeLabel");
            }

            if (secretModeIndicatorText == null)
            {
                secretModeIndicatorText = FindText("SecretModeIndicator");
            }

            if (microphoneLevelFill == null)
            {
                var levelTransform = UiTreeUtility.FindDeepChild(transform, "MicrophoneLevelFill");
                microphoneLevelFill = levelTransform != null ? levelTransform.GetComponent<Image>() : null;
            }

            if (microphoneDeviceText == null)
            {
                microphoneDeviceText = FindText("MicrophoneDeviceText");
            }

            if (logText == null)
            {
                logText = FindText("ChatLogText");
            }

            if (statusText == null)
            {
                statusText = FindText("StatusText");
            }
        }

        private void EnsureChatLogView()
        {
            if (chatLogView == null)
            {
                chatLogView = GetComponent<YuiChatLogView>();
                if (chatLogView == null)
                {
                    chatLogView = gameObject.AddComponent<YuiChatLogView>();
                }
            }
            chatLogView.Configure(logText, scrollRect);

            if (logText == null)
            {
                Debug.LogWarning("YuiChatPanel could not find ChatLogText. Recreate the scene from Yui > Create Chat UI Scene.");
            }
        }

        private void NormalizeLogView()
        {
            if (logText != null)
            {
                logText.enabled = true;
                logText.color = Color.white;
                logText.alignment = TextAnchor.UpperLeft;
                logText.horizontalOverflow = HorizontalWrapMode.Wrap;
                logText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            if (scrollRect == null || scrollRect.viewport == null)
            {
                return;
            }

            var legacyMask = scrollRect.viewport.GetComponent<Mask>();
            if (legacyMask != null)
            {
                legacyMask.enabled = false;
            }

            var viewportImage = scrollRect.viewport.GetComponent<Image>();
            if (viewportImage != null && viewportImage.color.a <= 0.01f)
            {
                viewportImage.enabled = false;
            }

            if (scrollRect.viewport.GetComponent<RectMask2D>() == null)
            {
                scrollRect.viewport.gameObject.AddComponent<RectMask2D>();
            }
        }

        private Button FindButton(string objectName)
        {
            var target = UiTreeUtility.FindDeepChild(transform, objectName);
            return target != null ? target.GetComponent<Button>() : null;
        }

        private Text FindText(string objectName)
        {
            var target = UiTreeUtility.FindDeepChild(transform, objectName);
            return target != null ? target.GetComponent<Text>() : null;
        }
    }
}
