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
            EnsureVoicePresetControls(content);
            EnsureIrodoriVoiceInstructInput(content);
            Debug.Log("Yui settings UI repaired: ensured Experimental / Mode dropdown.");
        }

        private void EnsureVoicePresetControls(Transform content)
        {
            if (voicePresetDropdown == null)
            {
                var existingDropdown = UiTreeUtility.FindDeepChild(settingsRoot.transform, "VoicePresetDropdown");
                voicePresetDropdown = existingDropdown != null ? existingDropdown.GetComponent<Dropdown>() : null;
            }

            CreateOrMoveRuntimeLabel(content, "VoicePresetLabel", "Voice Preset", 1029f);
            if (voicePresetDropdown == null && ttsModeDropdown != null)
            {
                var clone = Instantiate(ttsModeDropdown.gameObject, content, false);
                clone.name = "VoicePresetDropdown";
                voicePresetDropdown = clone.GetComponent<Dropdown>();
            }
            if (voicePresetDropdown != null)
            {
                voicePresetDropdown.transform.SetParent(content, false);
                SetTopRectRuntime(voicePresetDropdown.transform, 176f, 1019f, 22f, 54f);
                RefreshVoicePresetOptions();
                voicePresetDropdown.onValueChanged.RemoveListener(OnVoicePresetDropdownChanged);
                voicePresetDropdown.onValueChanged.AddListener(OnVoicePresetDropdownChanged);
            }

            if (voicePresetNameInput == null)
            {
                var existingInput = UiTreeUtility.FindDeepChild(settingsRoot.transform, "VoicePresetNameInput");
                voicePresetNameInput = existingInput != null ? existingInput.GetComponent<InputField>() : null;
            }
            CreateOrMoveRuntimeLabel(content, "VoicePresetNameLabel", "Preset Name", 1099f);
            if (voicePresetNameInput == null)
            {
                voicePresetNameInput = CreateRuntimeInputField(content, "VoicePresetNameInput");
            }
            voicePresetNameInput.transform.SetParent(content, false);
            SetTopRectRuntime(voicePresetNameInput.transform, 176f, 1089f, 22f, 42f);

            CreateOrMoveRuntimeLabel(content, "VoicePresetActionLabel", "Preset Action", 1159f);
            voicePresetSaveButton = EnsureRuntimeButton(content, voicePresetSaveButton, "VoicePresetSaveButton", "Save", true);
            voicePresetDeleteButton = EnsureRuntimeButton(content, voicePresetDeleteButton, "VoicePresetDeleteButton", "Delete", false);
            SetTopRectColumnRuntime(voicePresetSaveButton.transform, 176f, 22f, 1149f, 42f, 0f, 0.50f, 8f);
            SetTopRectColumnRuntime(voicePresetDeleteButton.transform, 176f, 22f, 1149f, 42f, 0.50f, 1f, 8f);
            voicePresetSaveButton.onClick.RemoveListener(SaveVoicePreset);
            voicePresetSaveButton.onClick.AddListener(SaveVoicePreset);
            voicePresetDeleteButton.onClick.RemoveListener(DeleteVoicePreset);
            voicePresetDeleteButton.onClick.AddListener(DeleteVoicePreset);
        }

        private Button EnsureRuntimeButton(Transform content, Button current, string name, string labelText, bool saveStyle)
        {
            if (current == null)
            {
                var existing = UiTreeUtility.FindDeepChild(settingsRoot.transform, name);
                current = existing != null ? existing.GetComponent<Button>() : null;
            }
            if (current == null)
            {
                var source = saveStyle ? cameraSaveButton : cameraDeleteButton;
                if (source == null)
                {
                    source = applyButton;
                }
                var clone = Instantiate(source.gameObject, content, false);
                clone.name = name;
                current = clone.GetComponent<Button>();
            }

            current.transform.SetParent(content, false);
            var label = current.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = labelText;
            }
            return current;
        }

        private void EnsureIrodoriVoiceInstructInput(Transform content)
        {
            var existingDropdown = UiTreeUtility.FindDeepChild(settingsRoot.transform, "IrodoriVoiceGenderDropdown");
            if (irodoriVoiceGenderDropdown == null)
            {
                irodoriVoiceGenderDropdown = existingDropdown != null ? existingDropdown.GetComponent<Dropdown>() : null;
            }

            CreateOrMoveRuntimeLabel(content, "IrodoriVoiceGenderLabel", "Irodori Base", 1454f);
            if (irodoriVoiceGenderDropdown == null)
            {
                if (ttsModeDropdown != null)
                {
                    var clone = Instantiate(ttsModeDropdown.gameObject, content, false);
                    clone.name = "IrodoriVoiceGenderDropdown";
                    irodoriVoiceGenderDropdown = clone.GetComponent<Dropdown>();
                }
            }

            if (irodoriVoiceGenderDropdown != null)
            {
                irodoriVoiceGenderDropdown.transform.SetParent(content, false);
                SetTopRectRuntime(irodoriVoiceGenderDropdown.transform, 176f, 1444f, 22f, 54f);
                RefreshIrodoriVoiceGenderOptions();
            }

            var existingInput = UiTreeUtility.FindDeepChild(settingsRoot.transform, "IrodoriVoiceInstructInput");
            if (irodoriVoiceInstructInput == null)
            {
                irodoriVoiceInstructInput = existingInput != null ? existingInput.GetComponent<InputField>() : null;
            }

            CreateOrMoveRuntimeLabel(content, "IrodoriVoiceInstructLabel", "Irodori Voice", 1524f);
            if (irodoriVoiceInstructInput == null)
            {
                irodoriVoiceInstructInput = CreateRuntimeInputField(content, "IrodoriVoiceInstructInput");
            }

            irodoriVoiceInstructInput.transform.SetParent(content, false);
            irodoriVoiceInstructInput.lineType = InputField.LineType.MultiLineSubmit;
            SetTopRectRuntime(irodoriVoiceInstructInput.transform, 176f, 1514f, 22f, 72f);
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
