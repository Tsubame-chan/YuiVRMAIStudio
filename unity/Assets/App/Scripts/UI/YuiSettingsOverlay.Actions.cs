using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiSettingsOverlay
    {
        private void Apply()
        {
            ApplyFieldsToRuntime(true);
            Hide();
        }

        private void ApplyFieldsToRuntime(bool applyDisplaySettings)
        {
            var backendUrl = backendUrlInput != null ? backendUrlInput.text : null;
            var speakerId = chatPanel != null ? chatPanel.SpeakerId : 14;
            if (speakerDropdown != null)
            {
                speakerId = VoiceIdAt(speakerDropdown.value, speakerId);
            }

            var volume = volumeSlider != null ? volumeSlider.value : 1f;
            if (chatPanel != null)
            {
                var conversationMode = ConversationModeValue();
                var ttsMode = TtsModeForConversationMode(conversationMode, TtsModeValue());
                var speed = speedSlider != null
                    ? YuiTtsTuning.SafeSpeedForMode(ttsMode, speedSlider.value)
                    : 1.0f;
                var pitch = pitchSlider != null
                    ? YuiTtsTuning.SafePitchForMode(ttsMode, pitchSlider.value)
                    : 0.0f;
                SaveCustomVrmDisplayNameFromInput();
                chatPanel.SetCharacterName(characterNameInput != null ? characterNameInput.text : chatPanel.CharacterName);
                chatPanel.SetCustomInstruction(customInstructionInput != null ? customInstructionInput.text : chatPanel.CustomInstruction);
                chatPanel.SetDirectOpenAiSettings(
                    openAiApiKeyInput != null ? openAiApiKeyInput.text : chatPanel.OpenAiApiKey,
                    openAiModelInput != null ? openAiModelInput.text : chatPanel.OpenAiModel);
                chatPanel.SetAutoAiFallbackEnabled(autoAiFallbackToggle == null || autoAiFallbackToggle.isOn);
                chatPanel.SetAvatarSlot(AvatarSlotValue());
                chatPanel.ApplyRuntimeSettings(
                    backendUrl,
                    speakerId,
                    volume,
                    speed,
                    pitch,
                    intonationSlider != null ? intonationSlider.value : 1.0f,
                    synthesisVolumeSlider != null ? synthesisVolumeSlider.value : 1.0f,
                    prePhonemeSlider != null ? prePhonemeSlider.value : 0.1f,
                    postPhonemeSlider != null ? postPhonemeSlider.value : 0.1f,
                    conversationMode,
                    ttsMode,
                    IrodoriVoiceGenderValue(),
                    irodoriVoiceInstructInput != null ? irodoriVoiceInstructInput.text : chatPanel.IrodoriVoiceInstruct,
                    MicrophoneValue(),
                    LookCameraValue());
            }

            if (applyDisplaySettings && backgroundManager != null && backgroundDropdown != null)
            {
                backgroundManager.SetPreset(backgroundDropdown.value);
            }

            if (applyDisplaySettings && windowResolutionController != null && resolutionDropdown != null)
            {
                windowResolutionController.SetPreset(resolutionDropdown.value);
            }
        }

        private static string TtsModeForConversationMode(string conversationMode, string selectedTtsMode)
        {
            if (YuiConversationModes.IsRealtimeVoicevox(conversationMode))
            {
                return "server";
            }

            if (YuiConversationModes.IsRealtimeAivis(conversationMode))
            {
                return "aivis";
            }

            return selectedTtsMode;
        }

        private void PreviewVoice()
        {
            if (isPreviewingVoice)
            {
                return;
            }

            ApplyFieldsToRuntime(false);
            if (chatPanel != null)
            {
                isPreviewingVoice = true;
                SetVoicePreviewInteractable(false);
                chatPanel.PreviewVoice(OnVoicePreviewFinished);
            }
        }

        private void TestMicrophone()
        {
            ApplyFieldsToRuntime(false);
            StartMicrophoneMonitor();
        }

        private void ImportCustomVrm()
        {
            if (chatPanel != null)
            {
                SaveCustomVrmDisplayNameFromInput();
                chatPanel.SetAvatarSlot(AvatarSlotValue());
                chatPanel.ImportCustomVrmFromFilePicker();
            }
        }

        private void ClearCustomVrm()
        {
            if (chatPanel == null)
            {
                return;
            }

            var slot = AvatarSlotValue();
            if (!YuiAvatarSlots.IsCustomVrm(slot))
            {
                return;
            }

            chatPanel.ClearCustomVrmSlot(slot);
            RefreshAvatarOptions();
            RefreshCustomVrmNameInput();
        }

        private void OnVoicePreviewFinished()
        {
            isPreviewingVoice = false;
            SetVoicePreviewInteractable(settingsRoot != null && settingsRoot.activeInHierarchy);
        }

        private void SetVoicePreviewInteractable(bool interactable)
        {
            if (voicePreviewButton != null)
            {
                voicePreviewButton.interactable = interactable;
            }
        }

        private void Bind()
        {
            EnsureVoiceOptions();
            RefreshConversationModeOptions();
            RefreshTtsModeOptions();
            RefreshMicrophoneOptions();
            RefreshLookCameraOptions();
            RefreshAvatarOptions();
            RefreshResolutionOptions();
            RefreshCameraPresetOptions();
            if (openButton != null)
            {
                openButton.onClick.AddListener(Show);
            }
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }
            if (applyButton != null)
            {
                applyButton.onClick.AddListener(Apply);
            }
            if (advancedButton != null)
            {
                advancedButton.onClick.AddListener(ToggleAdvanced);
            }
            if (voicePreviewButton != null)
            {
                voicePreviewButton.onClick.AddListener(PreviewVoice);
            }
            if (voicePresetDropdown != null)
            {
                voicePresetDropdown.onValueChanged.AddListener(OnVoicePresetDropdownChanged);
            }
            if (voicePresetSaveButton != null)
            {
                voicePresetSaveButton.onClick.AddListener(SaveVoicePreset);
            }
            if (voicePresetDeleteButton != null)
            {
                voicePresetDeleteButton.onClick.AddListener(DeleteVoicePreset);
            }
            if (ttsModeDropdown != null)
            {
                ttsModeDropdown.onValueChanged.AddListener(OnTtsModeDropdownChanged);
            }
            if (microphoneTestButton != null)
            {
                microphoneTestButton.onClick.AddListener(TestMicrophone);
            }
            if (customVrmImportButton != null)
            {
                customVrmImportButton.onClick.AddListener(ImportCustomVrm);
            }
            if (customVrmClearButton != null)
            {
                customVrmClearButton.onClick.AddListener(ClearCustomVrm);
            }
            if (cameraAdjustButton != null)
            {
                cameraAdjustButton.onClick.AddListener(BeginCameraAdjust);
            }
            if (cameraAutoButton != null)
            {
                cameraAutoButton.onClick.AddListener(ApplyAutoCamera);
            }
            if (cameraSaveButton != null)
            {
                cameraSaveButton.onClick.AddListener(SaveCameraPreset);
            }
            if (cameraDeleteButton != null)
            {
                cameraDeleteButton.onClick.AddListener(DeleteCameraPreset);
            }
            if (cameraPresetDropdown != null)
            {
                cameraPresetDropdown.onValueChanged.AddListener(ApplyCameraPreset);
            }
            if (cameraAdjustDoneButton != null)
            {
                cameraAdjustDoneButton.onClick.AddListener(EndCameraAdjust);
            }
            if (clearHistoryButton != null)
            {
                clearHistoryButton.onClick.AddListener(ShowClearConfirm);
            }
            if (clearConfirmButton != null)
            {
                clearConfirmButton.onClick.AddListener(ConfirmClearHistory);
            }
            if (clearCancelButton != null)
            {
                clearCancelButton.onClick.AddListener(HideClearConfirm);
            }
            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.AddListener(UpdateVolumeLabel);
            }
            if (speedSlider != null)
            {
                speedSlider.onValueChanged.AddListener(UpdateSpeedLabel);
            }
            if (pitchSlider != null)
            {
                pitchSlider.onValueChanged.AddListener(UpdatePitchLabel);
            }
            if (intonationSlider != null)
            {
                intonationSlider.onValueChanged.AddListener(UpdateIntonationLabel);
            }
            if (synthesisVolumeSlider != null)
            {
                synthesisVolumeSlider.onValueChanged.AddListener(UpdateSynthesisVolumeLabel);
            }
            if (prePhonemeSlider != null)
            {
                prePhonemeSlider.onValueChanged.AddListener(UpdatePrePhonemeLabel);
            }
            if (postPhonemeSlider != null)
            {
                postPhonemeSlider.onValueChanged.AddListener(UpdatePostPhonemeLabel);
            }
            if (avatarDropdown != null)
            {
                avatarDropdown.onValueChanged.AddListener(OnAvatarDropdownChanged);
            }
        }

        private void Unbind()
        {
            if (openButton != null)
            {
                openButton.onClick.RemoveListener(Show);
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }
            if (applyButton != null)
            {
                applyButton.onClick.RemoveListener(Apply);
            }
            if (advancedButton != null)
            {
                advancedButton.onClick.RemoveListener(ToggleAdvanced);
            }
            if (voicePreviewButton != null)
            {
                voicePreviewButton.onClick.RemoveListener(PreviewVoice);
            }
            if (voicePresetDropdown != null)
            {
                voicePresetDropdown.onValueChanged.RemoveListener(OnVoicePresetDropdownChanged);
            }
            if (voicePresetSaveButton != null)
            {
                voicePresetSaveButton.onClick.RemoveListener(SaveVoicePreset);
            }
            if (voicePresetDeleteButton != null)
            {
                voicePresetDeleteButton.onClick.RemoveListener(DeleteVoicePreset);
            }
            if (ttsModeDropdown != null)
            {
                ttsModeDropdown.onValueChanged.RemoveListener(OnTtsModeDropdownChanged);
            }
            if (microphoneTestButton != null)
            {
                microphoneTestButton.onClick.RemoveListener(TestMicrophone);
            }
            if (customVrmImportButton != null)
            {
                customVrmImportButton.onClick.RemoveListener(ImportCustomVrm);
            }
            if (customVrmClearButton != null)
            {
                customVrmClearButton.onClick.RemoveListener(ClearCustomVrm);
            }
            if (cameraAdjustButton != null)
            {
                cameraAdjustButton.onClick.RemoveListener(BeginCameraAdjust);
            }
            if (cameraAutoButton != null)
            {
                cameraAutoButton.onClick.RemoveListener(ApplyAutoCamera);
            }
            if (cameraSaveButton != null)
            {
                cameraSaveButton.onClick.RemoveListener(SaveCameraPreset);
            }
            if (cameraDeleteButton != null)
            {
                cameraDeleteButton.onClick.RemoveListener(DeleteCameraPreset);
            }
            if (cameraPresetDropdown != null)
            {
                cameraPresetDropdown.onValueChanged.RemoveListener(ApplyCameraPreset);
            }
            if (cameraAdjustDoneButton != null)
            {
                cameraAdjustDoneButton.onClick.RemoveListener(EndCameraAdjust);
            }
            if (clearHistoryButton != null)
            {
                clearHistoryButton.onClick.RemoveListener(ShowClearConfirm);
            }
            if (clearConfirmButton != null)
            {
                clearConfirmButton.onClick.RemoveListener(ConfirmClearHistory);
            }
            if (clearCancelButton != null)
            {
                clearCancelButton.onClick.RemoveListener(HideClearConfirm);
            }
            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.RemoveListener(UpdateVolumeLabel);
            }
            if (speedSlider != null)
            {
                speedSlider.onValueChanged.RemoveListener(UpdateSpeedLabel);
            }
            if (pitchSlider != null)
            {
                pitchSlider.onValueChanged.RemoveListener(UpdatePitchLabel);
            }
            if (intonationSlider != null)
            {
                intonationSlider.onValueChanged.RemoveListener(UpdateIntonationLabel);
            }
            if (synthesisVolumeSlider != null)
            {
                synthesisVolumeSlider.onValueChanged.RemoveListener(UpdateSynthesisVolumeLabel);
            }
            if (prePhonemeSlider != null)
            {
                prePhonemeSlider.onValueChanged.RemoveListener(UpdatePrePhonemeLabel);
            }
            if (postPhonemeSlider != null)
            {
                postPhonemeSlider.onValueChanged.RemoveListener(UpdatePostPhonemeLabel);
            }
            if (avatarDropdown != null)
            {
                avatarDropdown.onValueChanged.RemoveListener(OnAvatarDropdownChanged);
            }
        }
        private void OnAvatarDropdownChanged(int _)
        {
            RefreshCustomVrmNameInput();
        }

        private void UpdateVolumeLabel(float value)
        {
            if (volumeValueText != null)
            {
                volumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
            }
        }

        private void UpdateSpeedLabel(float value)
        {
            SetText(speedValueText, value.ToString("0.00") + "x");
        }

        private void UpdatePitchLabel(float value)
        {
            SetText(pitchValueText, value.ToString("+0.00;-0.00;0.00"));
        }

        private void UpdateIntonationLabel(float value)
        {
            SetText(intonationValueText, value.ToString("0.00") + "x");
        }

        private void UpdateSynthesisVolumeLabel(float value)
        {
            SetText(synthesisVolumeValueText, value.ToString("0.00") + "x");
        }

        private void UpdatePrePhonemeLabel(float value)
        {
            SetText(prePhonemeValueText, value.ToString("0.00") + "s");
        }

        private void UpdatePostPhonemeLabel(float value)
        {
            SetText(postPhonemeValueText, value.ToString("0.00") + "s");
        }

        private void OnVoicePresetDropdownChanged(int index)
        {
            var preset = VoicePresetAt(index);
            if (preset == null)
            {
                return;
            }

            ApplyVoicePresetToFields(preset);
        }

        private void OnTtsModeDropdownChanged(int _)
        {
            SaveCurrentVoiceFieldsForMode(lastTtsModeValue);
            var nextMode = TtsModeValue();
            lastTtsModeValue = nextMode;
            EnsureVoiceOptions();
            ConfigureVoiceSliderRanges();
            LoadVoiceFieldsForMode(nextMode);
            UpdateSpeedLabel(speedSlider != null ? speedSlider.value : 1f);
            UpdatePitchLabel(pitchSlider != null ? pitchSlider.value : 0f);
            UpdateIntonationLabel(intonationSlider != null ? intonationSlider.value : 1f);
            UpdateSynthesisVolumeLabel(synthesisVolumeSlider != null ? synthesisVolumeSlider.value : 1f);
            UpdatePrePhonemeLabel(prePhonemeSlider != null ? prePhonemeSlider.value : 0.1f);
            UpdatePostPhonemeLabel(postPhonemeSlider != null ? postPhonemeSlider.value : 0.1f);
            RefreshTtsSpecificVoiceControls();
        }

        private void SaveCurrentVoiceFieldsForMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                return;
            }

            YuiTtsTuningPrefs.SaveForMode(mode, new YuiSavedTtsTuning(
                VoiceIdAtForMode(mode, speakerDropdown != null ? speakerDropdown.value : 0, YuiTtsTuningPrefs.DefaultSpeakerForMode(mode)),
                speedSlider != null ? speedSlider.value : 1.0f,
                pitchSlider != null ? pitchSlider.value : 0.0f,
                intonationSlider != null ? intonationSlider.value : 1.0f,
                synthesisVolumeSlider != null ? synthesisVolumeSlider.value : 1.0f,
                prePhonemeSlider != null ? prePhonemeSlider.value : 0.1f,
                postPhonemeSlider != null ? postPhonemeSlider.value : 0.1f));
            PlayerPrefs.Save();
        }

        private void LoadVoiceFieldsForMode(string mode)
        {
            var tuning = YuiTtsTuningPrefs.LoadForMode(mode, YuiTtsTuningPrefs.DefaultForMode(mode));
            if (speakerDropdown != null)
            {
                speakerDropdown.SetValueWithoutNotify(VoiceIndexForIdForMode(mode, tuning.SpeakerId));
                speakerDropdown.RefreshShownValue();
            }
            SetSliderValue(speedSlider, tuning.SpeedScale);
            SetSliderValue(pitchSlider, tuning.PitchScale);
            SetSliderValue(intonationSlider, tuning.IntonationScale);
            SetSliderValue(synthesisVolumeSlider, tuning.SynthesisVolumeScale);
            SetSliderValue(prePhonemeSlider, tuning.PrePhonemeLength);
            SetSliderValue(postPhonemeSlider, tuning.PostPhonemeLength);
        }

        private void SaveVoicePreset()
        {
            var preset = CollectVoicePresetFromFields();
            YuiVoicePresetStore.Upsert(preset);
            RefreshVoicePresetOptions();
            if (voicePresetDropdown != null)
            {
                for (var index = 0; index < voicePresetDropdown.options.Count; index++)
                {
                    if (voicePresetDropdown.options[index].text == preset.Name)
                    {
                        voicePresetDropdown.SetValueWithoutNotify(index);
                        voicePresetDropdown.RefreshShownValue();
                        break;
                    }
                }
            }
        }

        private void DeleteVoicePreset()
        {
            if (voicePresetDropdown == null || voicePresetDropdown.value <= 0)
            {
                return;
            }

            var preset = VoicePresetAt(voicePresetDropdown.value);
            if (preset == null)
            {
                return;
            }

            YuiVoicePresetStore.Delete(preset.Name);
            voicePresetDropdown.SetValueWithoutNotify(0);
            RefreshVoicePresetOptions();
        }

        private YuiVoicePreset CollectVoicePresetFromFields()
        {
            var fallbackName = chatPanel != null && !string.IsNullOrWhiteSpace(chatPanel.CharacterName)
                ? chatPanel.CharacterName + " Voice"
                : "Voice Preset";
            var name = voicePresetNameInput != null && !string.IsNullOrWhiteSpace(voicePresetNameInput.text)
                ? voicePresetNameInput.text.Trim()
                : fallbackName;

            return new YuiVoicePreset
            {
                Name = name,
                TtsMode = TtsModeValue(),
                SpeakerId = VoiceIdAt(speakerDropdown != null ? speakerDropdown.value : 0, chatPanel != null ? chatPanel.SpeakerId : 14),
                VoiceVolume = volumeSlider != null ? volumeSlider.value : 1.0f,
                SpeedScale = speedSlider != null ? speedSlider.value : 1.0f,
                PitchScale = pitchSlider != null ? pitchSlider.value : 0.0f,
                IntonationScale = intonationSlider != null ? intonationSlider.value : 1.0f,
                SynthesisVolumeScale = synthesisVolumeSlider != null ? synthesisVolumeSlider.value : 1.0f,
                PrePhonemeLength = prePhonemeSlider != null ? prePhonemeSlider.value : 0.1f,
                PostPhonemeLength = postPhonemeSlider != null ? postPhonemeSlider.value : 0.1f,
                IrodoriVoiceGender = IrodoriVoiceGenderValue(),
                IrodoriVoiceInstruct = irodoriVoiceInstructInput != null ? irodoriVoiceInstructInput.text : string.Empty
            };
        }

        private void ApplyVoicePresetToFields(YuiVoicePreset preset)
        {
            if (voicePresetNameInput != null)
            {
                voicePresetNameInput.text = preset.Name;
            }
            if (ttsModeDropdown != null)
            {
                RefreshTtsModeOptions();
                ttsModeDropdown.SetValueWithoutNotify(TtsModeIndex(preset.TtsMode));
                ttsModeDropdown.RefreshShownValue();
            }
            lastTtsModeValue = TtsModeValue();
            ConfigureVoiceSliderRanges();
            var sanitized = YuiTtsTuningPrefs.Sanitize(lastTtsModeValue, new YuiSavedTtsTuning(
                preset.SpeakerId,
                preset.SpeedScale,
                preset.PitchScale,
                preset.IntonationScale,
                preset.SynthesisVolumeScale,
                preset.PrePhonemeLength,
                preset.PostPhonemeLength));
            if (speakerDropdown != null)
            {
                EnsureVoiceOptions();
                speakerDropdown.SetValueWithoutNotify(VoiceIndexForId(sanitized.SpeakerId));
                speakerDropdown.RefreshShownValue();
            }
            SetSliderValue(volumeSlider, preset.VoiceVolume <= 0f ? 1f : preset.VoiceVolume);
            SetSliderValue(speedSlider, sanitized.SpeedScale);
            SetSliderValue(pitchSlider, sanitized.PitchScale);
            SetSliderValue(intonationSlider, sanitized.IntonationScale);
            SetSliderValue(synthesisVolumeSlider, sanitized.SynthesisVolumeScale);
            SetSliderValue(prePhonemeSlider, sanitized.PrePhonemeLength);
            SetSliderValue(postPhonemeSlider, sanitized.PostPhonemeLength);
            if (irodoriVoiceGenderDropdown != null)
            {
                RefreshIrodoriVoiceGenderOptions();
                irodoriVoiceGenderDropdown.SetValueWithoutNotify(IrodoriVoiceGenderIndex(preset.IrodoriVoiceGender));
                irodoriVoiceGenderDropdown.RefreshShownValue();
            }
            if (irodoriVoiceInstructInput != null)
            {
                irodoriVoiceInstructInput.text = preset.IrodoriVoiceInstruct;
            }

            UpdateSpeedLabel(speedSlider != null ? speedSlider.value : 1f);
            UpdateVolumeLabel(volumeSlider != null ? volumeSlider.value : 1f);
            UpdatePitchLabel(pitchSlider != null ? pitchSlider.value : 0f);
            UpdateIntonationLabel(intonationSlider != null ? intonationSlider.value : 1f);
            UpdateSynthesisVolumeLabel(synthesisVolumeSlider != null ? synthesisVolumeSlider.value : 1f);
            UpdatePrePhonemeLabel(prePhonemeSlider != null ? prePhonemeSlider.value : 0.1f);
            UpdatePostPhonemeLabel(postPhonemeSlider != null ? postPhonemeSlider.value : 0.1f);
            RefreshTtsSpecificVoiceControls();
        }

        private static void SetSliderValue(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(Mathf.Clamp(value, slider.minValue, slider.maxValue));
            }
        }

        private void ApplyAutoCamera()
        {
            if (consoleVisibilityController != null)
            {
                consoleVisibilityController.FrameAvatarAsDefault();
            }

            if (cameraPresetDropdown != null)
            {
                cameraPresetDropdown.SetValueWithoutNotify(0);
                cameraPresetDropdown.RefreshShownValue();
            }
        }

        private void BeginCameraAdjust()
        {
            StopMicrophoneMonitor();
            HideClearConfirm();
            if (cameraPresetDropdown != null && cameraPresetDropdown.value <= 0 && cameraPresetDropdown.options.Count > 1)
            {
                cameraPresetDropdown.SetValueWithoutNotify(1);
                cameraPresetDropdown.RefreshShownValue();
            }

            if (settingsRoot != null)
            {
                settingsRoot.SetActive(false);
            }

            SetCameraAdjustVisible(true);
            if (consoleVisibilityController != null)
            {
                consoleVisibilityController.BeginCameraEditMode();
            }
        }

        private void EndCameraAdjust()
        {
            var presetIndex = CameraPresetIndex();
            if (consoleVisibilityController != null)
            {
                if (presetIndex <= 0 && cameraPresetDropdown != null && cameraPresetDropdown.options.Count > 1)
                {
                    presetIndex = 1;
                    cameraPresetDropdown.SetValueWithoutNotify(presetIndex);
                    cameraPresetDropdown.RefreshShownValue();
                }

                if (presetIndex > 0)
                {
                    consoleVisibilityController.SaveCameraPreset(presetIndex);
                }
            }

            SetCameraAdjustVisible(false);
            if (consoleVisibilityController != null)
            {
                consoleVisibilityController.EndCameraEditMode();
            }
        }

        private void SetCameraAdjustVisible(bool visible)
        {
            if (cameraAdjustRoot != null)
            {
                cameraAdjustRoot.SetActive(visible);
            }
        }

        private void ApplyCameraPreset(int presetIndex)
        {
            if (consoleVisibilityController != null)
            {
                consoleVisibilityController.ApplyCameraPreset(presetIndex);
            }
        }

        private void SaveCameraPreset()
        {
            var presetIndex = CameraPresetIndex();
            if (consoleVisibilityController != null && presetIndex > 0)
            {
                consoleVisibilityController.SaveCameraPreset(presetIndex);
            }
        }

        private void DeleteCameraPreset()
        {
            var presetIndex = CameraPresetIndex();
            if (consoleVisibilityController != null && presetIndex > 0)
            {
                consoleVisibilityController.DeleteCameraPreset(presetIndex);
            }
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private void ToggleAdvanced()
        {
            SetAdvancedVisible(!advancedVisible);
        }

        private void SetAdvancedVisible(bool visible)
        {
            advancedVisible = visible;
            if (advancedRoot != null)
            {
                advancedRoot.SetActive(visible);
            }
        }

        private void ShowClearConfirm()
        {
            if (clearConfirmRoot != null)
            {
                clearConfirmRoot.SetActive(true);
            }
        }

        private void HideClearConfirm()
        {
            if (clearConfirmRoot != null)
            {
                clearConfirmRoot.SetActive(false);
            }
        }

        private void ConfirmClearHistory()
        {
            HideClearConfirm();
            if (chatPanel != null)
            {
                chatPanel.ClearConversationCache();
            }
        }
    }
}
