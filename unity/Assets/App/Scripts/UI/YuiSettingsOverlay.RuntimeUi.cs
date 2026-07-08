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
            PrepareDropdownTemplateRuntime(conversationModeDropdown, 286f, 34f);
            RefreshConversationModeOptions();
            EnsureDirectOpenAiAdvancedInputs(content);
            EnsureCustomVrmNameInput(content);
            EnsureVoicePresetControls(content);
            EnsureIrodoriVoiceInstructInput(content);
            EnsureLocalAiAssetControls(content);
            Debug.Log("Yui settings UI repaired: ensured Experimental / Mode dropdown.");
        }

        private void EnsureDirectOpenAiAdvancedInputs(Transform fallbackContent)
        {
            var parent = advancedRoot != null ? advancedRoot.transform : fallbackContent;
            if (parent == null)
            {
                return;
            }
            if (advancedRoot != null)
            {
                EnsureAdvancedApiPanelFrame(advancedRoot.transform);
            }
            EnsureAdvancedPanelBacking(parent);

            if (backendUrlInput == null)
            {
                var existing = UiTreeUtility.FindDeepChild(settingsRoot.transform, "BackendInput");
                backendUrlInput = existing != null ? existing.GetComponent<InputField>() : null;
            }
            if (openAiApiKeyInput == null)
            {
                var existing = UiTreeUtility.FindDeepChild(settingsRoot.transform, "OpenAiApiKeyInput");
                openAiApiKeyInput = existing != null ? existing.GetComponent<InputField>() : null;
            }
            if (openAiModelInput == null)
            {
                var existing = UiTreeUtility.FindDeepChild(settingsRoot.transform, "OpenAiModelInput");
                openAiModelInput = existing != null ? existing.GetComponent<InputField>() : null;
            }

            CreateOrMoveRuntimeLabel(parent, "BackendLabel", "Backend URL", 20f);
            if (backendUrlInput == null)
            {
                backendUrlInput = CreateRuntimeInputField(parent, "BackendInput");
            }
            backendUrlInput.transform.SetParent(parent, false);
            backendUrlInput.contentType = InputField.ContentType.Standard;
            backendUrlInput.lineType = InputField.LineType.SingleLine;
            MakeRuntimeInputReadable(backendUrlInput);
            SetTopRectRuntime(backendUrlInput.transform, 176f, 10f, 22f, 42f);

            CreateOrMoveRuntimeLabel(parent, "OpenAiApiKeyLabel", "OpenAI API Key", 78f);
            if (openAiApiKeyInput == null)
            {
                openAiApiKeyInput = CreateRuntimeInputField(parent, "OpenAiApiKeyInput");
            }
            openAiApiKeyInput.transform.SetParent(parent, false);
            openAiApiKeyInput.contentType = InputField.ContentType.Password;
            openAiApiKeyInput.lineType = InputField.LineType.SingleLine;
            MakeRuntimeInputReadable(openAiApiKeyInput);
            SetTopRectRuntime(openAiApiKeyInput.transform, 176f, 68f, 22f, 42f);

            CreateOrMoveRuntimeLabel(parent, "OpenAiModelLabel", "OpenAI Model", 136f);
            if (openAiModelInput == null)
            {
                openAiModelInput = CreateRuntimeInputField(parent, "OpenAiModelInput");
            }
            openAiModelInput.transform.SetParent(parent, false);
            openAiModelInput.contentType = InputField.ContentType.Standard;
            openAiModelInput.lineType = InputField.LineType.SingleLine;
            MakeRuntimeInputReadable(openAiModelInput);
            SetTopRectRuntime(openAiModelInput.transform, 176f, 126f, 22f, 42f);

            CreateOrMoveRuntimeLabel(parent, "AutoAiFallbackLabel", "Auto Fallback", 194f);
            autoAiFallbackToggle = EnsureRuntimeToggle(parent, autoAiFallbackToggle, "AutoAiFallbackToggle", "Backend offline -> Local Gemma");
            SetTopRectRuntime(autoAiFallbackToggle.transform, 176f, 184f, 22f, 42f);
        }

        private static void EnsureAdvancedApiPanelFrame(Transform panel)
        {
            SetAnchorsRuntime(panel, new Vector2(0.07f, 0.56f), new Vector2(0.93f, 0.86f));
        }

        private static void EnsureAdvancedPanelBacking(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            var backing = parent.Find("AdvancedApiBacking");
            if (backing == null)
            {
                var backingObject = new GameObject("AdvancedApiBacking", typeof(RectTransform), typeof(Image));
                backingObject.transform.SetParent(parent, false);
                backing = backingObject.transform;
            }

            var rect = backing.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            var image = backing.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.055f, 0.06f, 0.075f, 0.98f);
                image.raycastTarget = true;
            }

            backing.SetAsFirstSibling();
        }

        private static void MakeRuntimeInputReadable(InputField input)
        {
            if (input == null)
            {
                return;
            }

            var image = input.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.96f, 0.97f, 1f, 1f);
            }

            var textColor = new Color(0.08f, 0.10f, 0.14f, 1f);
            if (input.textComponent != null)
            {
                input.textComponent.color = textColor;
                input.textComponent.fontSize = Mathf.Max(14, input.textComponent.fontSize);
                input.textComponent.alignment = TextAnchor.MiddleLeft;
                input.textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
                input.textComponent.verticalOverflow = VerticalWrapMode.Truncate;
                input.textComponent.resizeTextForBestFit = false;
                SetTopRectRuntime(input.textComponent.transform, 12f, 0f, 12f, 42f);
            }

            if (input.placeholder is Text placeholderText)
            {
                placeholderText.color = new Color(0.34f, 0.38f, 0.46f, 1f);
                placeholderText.horizontalOverflow = HorizontalWrapMode.Overflow;
                placeholderText.verticalOverflow = VerticalWrapMode.Truncate;
                placeholderText.resizeTextForBestFit = false;
                SetTopRectRuntime(placeholderText.transform, 12f, 0f, 12f, 42f);
            }

            input.caretColor = textColor;
            input.selectionColor = new Color(0.26f, 0.49f, 0.90f, 0.35f);
            if (input.GetComponent<RectMask2D>() == null)
            {
                input.gameObject.AddComponent<RectMask2D>();
            }
        }

        private static Toggle EnsureRuntimeToggle(Transform parent, Toggle current, string name, string labelText)
        {
            if (current == null && parent != null)
            {
                var existing = UiTreeUtility.FindDeepChild(parent, name);
                current = existing != null ? existing.GetComponent<Toggle>() : null;
            }
            if (current == null)
            {
                var root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
                root.transform.SetParent(parent, false);

                var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
                backgroundObject.transform.SetParent(root.transform, false);
                var backgroundImage = backgroundObject.GetComponent<Image>();
                backgroundImage.color = new Color(0.96f, 0.97f, 1f, 1f);
                SetTopRectRuntime(backgroundObject.transform, 0f, 8f, 0f, 8f);
                var backgroundRect = backgroundObject.GetComponent<RectTransform>();
                backgroundRect.anchorMin = new Vector2(0f, 0.5f);
                backgroundRect.anchorMax = new Vector2(0f, 0.5f);
                backgroundRect.sizeDelta = new Vector2(26f, 26f);
                backgroundRect.anchoredPosition = new Vector2(13f, 0f);

                var checkmarkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Text));
                checkmarkObject.transform.SetParent(backgroundObject.transform, false);
                var checkmarkText = checkmarkObject.GetComponent<Text>();
                checkmarkText.font = BuiltinUiFont();
                checkmarkText.fontSize = 20;
                checkmarkText.text = "✓";
                checkmarkText.alignment = TextAnchor.MiddleCenter;
                checkmarkText.color = new Color(0.10f, 0.32f, 0.78f, 1f);
                SetTopRectRuntime(checkmarkObject.transform, 0f, 0f, 0f, 0f);

                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(root.transform, false);
                var label = labelObject.GetComponent<Text>();
                label.font = BuiltinUiFont();
                label.fontSize = 14;
                label.text = labelText;
                label.alignment = TextAnchor.MiddleLeft;
                label.color = Color.white;
                SetTopRectRuntime(labelObject.transform, 36f, 0f, 0f, 42f);

                current = root.GetComponent<Toggle>();
                current.targetGraphic = backgroundImage;
                current.graphic = checkmarkText;
                current.isOn = true;
            }

            current.transform.SetParent(parent, false);
            var text = current.GetComponentInChildren<Text>(true);
            if (text != null && text.gameObject.name == "Label")
            {
                text.text = labelText;
            }
            return current;
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

        private void EnsureLocalAiAssetControls(Transform content)
        {
            CreateOrMoveRuntimeLabel(content, "LocalAiAssetSectionLabel", "Local AI Data", 1614f);
            if (localAiAssetStatusText == null)
            {
                var existing = UiTreeUtility.FindDeepChild(settingsRoot.transform, "LocalAiAssetStatusText");
                localAiAssetStatusText = existing != null ? existing.GetComponent<Text>() : null;
            }

            if (localAiAssetStatusText == null)
            {
                localAiAssetStatusText = CreateRuntimeStatusText(content, "LocalAiAssetStatusText");
            }

            localAiAssetStatusText.transform.SetParent(content, false);
            SetTopRectRuntime(localAiAssetStatusText.transform, 176f, 1604f, 22f, 42f);

            localAiAssetRepairButton = EnsureRuntimeButton(content, localAiAssetRepairButton, "LocalAiAssetRepairButton", "Repair / Download", true);
            optionalTtsDownloadButton = EnsureRuntimeButton(content, optionalTtsDownloadButton, "OptionalTtsDownloadButton", "Additional Voices", true);
            SetTopRectColumnRuntime(localAiAssetRepairButton.transform, 176f, 22f, 1654f, 42f, 0f, 0.50f, 8f);
            SetTopRectColumnRuntime(optionalTtsDownloadButton.transform, 176f, 22f, 1654f, 42f, 0.50f, 1f, 8f);
            localAiAssetRepairButton.onClick.RemoveListener(RequestLocalAiAssetRepair);
            localAiAssetRepairButton.onClick.AddListener(RequestLocalAiAssetRepair);
            optionalTtsDownloadButton.onClick.RemoveListener(RequestOptionalTtsDownload);
            optionalTtsDownloadButton.onClick.AddListener(RequestOptionalTtsDownload);
            RefreshLocalAiAssetStatus();
        }

        private static Text CreateRuntimeStatusText(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(parent, false);
            var text = root.GetComponent<Text>();
            text.font = BuiltinUiFont();
            text.fontSize = 14;
            text.color = new Color(0.88f, 0.92f, 1f, 1f);
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private void RefreshLocalAiAssetStatus()
        {
            if (localAiAssetStatusText != null)
            {
                localAiAssetStatusText.text = chatPanel != null
                    ? chatPanel.LocalAiAssetStatusText
                    : "Local AI data: not checked";
            }
        }

        private void RequestLocalAiAssetRepair()
        {
            chatPanel?.RequestLocalAiAssetRepairDownload();
            RefreshLocalAiAssetStatus();
        }

        private void RequestOptionalTtsDownload()
        {
            chatPanel?.RequestOptionalTtsAssetDownload();
            RefreshLocalAiAssetStatus();
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
