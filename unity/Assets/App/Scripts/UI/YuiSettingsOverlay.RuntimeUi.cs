using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiSettingsOverlay
    {
        private void ResolveRuntimeMeterReferences()
        {
            if (settingsRoot == null)
            {
                return;
            }

            var content = UiTreeUtility.FindDeepChild(settingsRoot.transform, "Content");
            var parent = content != null ? content : UiTreeUtility.FindDeepChild(settingsRoot.transform, "Panel");
            if (parent == null)
            {
                return;
            }

            var meter = parent.Find("MicrophoneTestMeter");
            if (meter != null)
            {
                var fillTransform = meter.Find("Fill");
                microphoneTestLevelFill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
            }
            var statusTransform = parent.Find("MicrophoneTestStatus");
            microphoneTestStatusText = statusTransform != null ? statusTransform.GetComponent<Text>() : null;
            SetMicrophoneTestLevel(0f);
        }

        private void RepairMissingRuntimeUi()
        {
            if (settingsRoot == null)
            {
                return;
            }

            if (conversationModeDropdown == null)
            {
                var existing = UiTreeUtility.FindDeepChild(settingsRoot.transform, "ConversationModeDropdown");
                conversationModeDropdown = existing != null ? existing.GetComponent<Dropdown>() : null;
            }

            var content = UiTreeUtility.FindDeepChild(settingsRoot.transform, "Content");
            if (content == null)
            {
                return;
            }

            if (IsLayoutMissingExperimentalSpace(content))
            {
                ShiftSettingsRowsAfter(content, 382f, 130f);
            }

            EnsureRuntimeSectionLabel(content, "ExperimentalSection", "Experimental", 382f);
            CreateOrMoveRuntimeLabel(content, "ConversationModeLabel", "Mode", 436f);
            if (conversationModeDropdown == null)
            {
                if (ttsModeDropdown == null)
                {
                    return;
                }

                var clone = Instantiate(ttsModeDropdown.gameObject, content, false);
                clone.name = "ConversationModeDropdown";
                conversationModeDropdown = clone.GetComponent<Dropdown>();
            }

            conversationModeDropdown.transform.SetParent(content, false);
            SetTopRectRuntime(conversationModeDropdown.transform, 176f, 426f, 22f, 54f);
            RefreshConversationModeOptions();
            EnsureCustomVrmNameInput(content);
            Debug.Log("Yui settings UI repaired: ensured Experimental / Mode dropdown.");
        }

        private void EnsureCustomVrmNameInput(Transform content)
        {
            var existingInput = UiTreeUtility.FindDeepChild(settingsRoot.transform, "CustomVrmNameInput");
            if (existingInput == null)
            {
                ShiftSettingsRowsAfter(content, 536f, 54f);
            }

            if (customVrmNameInput == null)
            {
                customVrmNameInput = existingInput != null ? existingInput.GetComponent<InputField>() : null;
            }

            CreateOrMoveRuntimeLabel(content, "CustomVrmNameLabel", "Slot Name", 536f);
            if (customVrmNameInput == null)
            {
                if (characterNameInput != null)
                {
                    var clone = Instantiate(characterNameInput.gameObject, content, false);
                    clone.name = "CustomVrmNameInput";
                    customVrmNameInput = clone.GetComponent<InputField>();
                }
                else
                {
                    customVrmNameInput = CreateRuntimeInputField(content, "CustomVrmNameInput");
                }
            }

            customVrmNameInput.transform.SetParent(content, false);
            SetTopRectRuntime(customVrmNameInput.transform, 176f, 526f, 22f, 42f);
            if (customVrmImportButton != null)
            {
                customVrmImportButton.transform.SetParent(content, false);
                SetTopRectColumnRuntime(customVrmImportButton.transform, 176f, 22f, 574f, 42f, 0f, 0.50f, 8f);
            }
            EnsureCustomVrmClearButton(content);
            RefreshCustomVrmNameInput();
        }

        private void EnsureCustomVrmClearButton(Transform content)
        {
            if (customVrmClearButton == null)
            {
                var existing = UiTreeUtility.FindDeepChild(settingsRoot.transform, "CustomVrmClearButton");
                customVrmClearButton = existing != null ? existing.GetComponent<Button>() : null;
            }

            if (customVrmClearButton == null)
            {
                if (customVrmImportButton == null)
                {
                    return;
                }

                var clone = Instantiate(customVrmImportButton.gameObject, content, false);
                clone.name = "CustomVrmClearButton";
                customVrmClearButton = clone.GetComponent<Button>();
                var label = clone.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = "Clear";
                }
            }

            customVrmClearButton.transform.SetParent(content, false);
            SetTopRectColumnRuntime(customVrmClearButton.transform, 176f, 22f, 574f, 42f, 0.50f, 1f, 8f);
            customVrmClearButton.onClick.RemoveListener(ClearCustomVrm);
            customVrmClearButton.onClick.AddListener(ClearCustomVrm);
        }

        private static InputField CreateRuntimeInputField(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            root.transform.SetParent(parent, false);
            var image = root.GetComponent<Image>();
            image.color = new Color(0.02f, 0.02f, 0.05f, 0.95f);

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(root.transform, false);
            var text = textObject.GetComponent<Text>();
            text.font = BuiltinUiFont();
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            SetTopRectRuntime(textObject.transform, 12f, 0f, 12f, 42f);

            var input = root.GetComponent<InputField>();
            input.textComponent = text;
            return input;
        }

        private static bool IsLayoutMissingExperimentalSpace(Transform content)
        {
            var avatar = content.Find("AvatarSection");
            var rect = avatar != null ? avatar.GetComponent<RectTransform>() : null;
            if (rect == null)
            {
                return true;
            }

            return -rect.offsetMax.y < 500f;
        }

    }
}
