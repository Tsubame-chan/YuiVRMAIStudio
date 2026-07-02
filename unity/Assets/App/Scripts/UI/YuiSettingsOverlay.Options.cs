using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiSettingsOverlay
    {
        private void RefreshFields()
        {
            if (chatPanel != null)
            {
                if (backendUrlInput != null)
                {
                    backendUrlInput.text = chatPanel.BackendUrl;
                }
                if (openAiApiKeyInput != null)
                {
                    openAiApiKeyInput.text = chatPanel.OpenAiApiKey;
                }
                if (openAiModelInput != null)
                {
                    openAiModelInput.text = chatPanel.OpenAiModel;
                }
                if (autoAiFallbackToggle != null)
                {
                    autoAiFallbackToggle.SetIsOnWithoutNotify(chatPanel.AutoAiFallbackEnabled);
                }
                RefreshTtsModeOptions();
                if (ttsModeDropdown != null)
                {
                    ttsModeDropdown.value = TtsModeIndex(chatPanel.TtsMode);
                    ttsModeDropdown.RefreshShownValue();
                }
                lastTtsModeValue = TtsModeValue();
                ConfigureVoiceSliderRanges();
                if (speakerDropdown != null)
                {
                    EnsureVoiceOptions();
                    speakerDropdown.value = VoiceIndexForId(chatPanel.SpeakerId);
                    speakerDropdown.RefreshShownValue();
                }
                if (volumeSlider != null)
                {
                    volumeSlider.value = chatPanel.VoiceVolume;
                }
                if (speedSlider != null)
                {
                    speedSlider.value = YuiTtsTuning.SafeSpeedForMode(TtsModeValue(), chatPanel.VoiceSpeedScale);
                }
                if (pitchSlider != null)
                {
                    pitchSlider.value = YuiTtsTuning.SafePitchForMode(TtsModeValue(), chatPanel.VoicePitchScale);
                }
                if (intonationSlider != null)
                {
                    intonationSlider.value = chatPanel.VoiceIntonationScale;
                }
                if (synthesisVolumeSlider != null)
                {
                    synthesisVolumeSlider.value = chatPanel.VoiceSynthesisVolumeScale;
                }
                if (prePhonemeSlider != null)
                {
                    prePhonemeSlider.value = chatPanel.VoicePrePhonemeLength;
                }
                if (postPhonemeSlider != null)
                {
                    postPhonemeSlider.value = chatPanel.VoicePostPhonemeLength;
                }
                RefreshConversationModeOptions();
                if (conversationModeDropdown != null)
                {
                    conversationModeDropdown.value = ConversationModeIndex(chatPanel.ConversationMode);
                    conversationModeDropdown.RefreshShownValue();
                }
                RefreshIrodoriVoiceGenderOptions();
                if (irodoriVoiceGenderDropdown != null)
                {
                    irodoriVoiceGenderDropdown.value = IrodoriVoiceGenderIndex(chatPanel.IrodoriVoiceGender);
                    irodoriVoiceGenderDropdown.RefreshShownValue();
                }
                if (irodoriVoiceInstructInput != null)
                {
                    irodoriVoiceInstructInput.text = chatPanel.IrodoriVoiceInstruct;
                }
                RefreshMicrophoneOptions();
                if (microphoneDropdown != null)
                {
                    microphoneDropdown.value = MicrophoneIndex(chatPanel.PreferredMicrophoneDevice);
                    microphoneDropdown.RefreshShownValue();
                }
                RefreshLookCameraOptions();
                if (lookCameraDropdown != null)
                {
                    lookCameraDropdown.value = LookCameraIndex(chatPanel.PreferredLookCameraDevice);
                    lookCameraDropdown.RefreshShownValue();
                }
                RefreshAvatarOptions();
                if (avatarDropdown != null)
                {
                    avatarDropdown.value = chatPanel.GetAvatarSlotOptionIndex(chatPanel.AvatarSlot);
                    avatarDropdown.RefreshShownValue();
                }
                RefreshCustomVrmNameInput();
                if (characterNameInput != null)
                {
                    characterNameInput.text = chatPanel.CharacterName;
                }
                if (customInstructionInput != null)
                {
                    customInstructionInput.text = chatPanel.CustomInstruction;
                }
            }

            if (backgroundDropdown != null && backgroundManager != null)
            {
                backgroundDropdown.value = (int)backgroundManager.Preset;
            }

            RefreshResolutionOptions();
            RefreshCameraPresetOptions();
            RefreshVoicePresetOptions();
            if (resolutionDropdown != null && windowResolutionController != null)
            {
                resolutionDropdown.value = windowResolutionController.PresetIndex;
                resolutionDropdown.RefreshShownValue();
            }

            if (cameraPresetDropdown != null)
            {
                cameraPresetDropdown.SetValueWithoutNotify(0);
                cameraPresetDropdown.RefreshShownValue();
            }

            UpdateVolumeLabel(volumeSlider != null ? volumeSlider.value : 1f);
            UpdateSpeedLabel(speedSlider != null ? speedSlider.value : 1f);
            UpdatePitchLabel(pitchSlider != null ? pitchSlider.value : 0f);
            UpdateIntonationLabel(intonationSlider != null ? intonationSlider.value : 1f);
            UpdateSynthesisVolumeLabel(synthesisVolumeSlider != null ? synthesisVolumeSlider.value : 1f);
            UpdatePrePhonemeLabel(prePhonemeSlider != null ? prePhonemeSlider.value : 0.1f);
            UpdatePostPhonemeLabel(postPhonemeSlider != null ? postPhonemeSlider.value : 0.1f);
            RefreshTtsSpecificVoiceControls();
            SetAdvancedVisible(false);
        }

        private void ConfigureVoiceSliderRanges()
        {
            if (speedSlider != null)
            {
                speedSlider.minValue = YuiTtsTuning.SpeedMinForMode(TtsModeValue());
                speedSlider.maxValue = YuiTtsTuning.SpeedMaxForMode(TtsModeValue());
                speedSlider.value = YuiTtsTuning.SafeSpeedForMode(TtsModeValue(), speedSlider.value);
            }

            if (pitchSlider != null)
            {
                pitchSlider.minValue = YuiTtsTuning.PitchMinForMode(TtsModeValue());
                pitchSlider.maxValue = YuiTtsTuning.PitchMaxForMode(TtsModeValue());
                pitchSlider.value = YuiTtsTuning.SafePitchForMode(TtsModeValue(), pitchSlider.value);
            }
        }
        private void EnsureVoiceOptions()
        {
            if (speakerDropdown == null)
            {
                return;
            }

            var options = ActiveVoiceOptions();
            if (speakerDropdown.options.Count == options.Count)
            {
                var same = true;
                for (var i = 0; i < options.Count; i++)
                {
                    if (speakerDropdown.options[i].text != options[i].Label)
                    {
                        same = false;
                        break;
                    }
                }

                if (same)
                {
                    return;
                }
            }

            speakerDropdown.options.Clear();
            foreach (var option in options)
            {
                speakerDropdown.options.Add(new Dropdown.OptionData(option.Label));
            }
        }

        private void RefreshTtsModeOptions()
        {
            if (ttsModeDropdown == null)
            {
                return;
            }

            var includeHttpTts = ShouldShowHttpTtsOption();
            var httpTtsAvailableOrUnknown = chatPanel == null
                || !chatPanel.BackendConfigLoaded
                || chatPanel.HttpTtsAvailable;
            ttsModeDropdown.options.Clear();
            var options = YuiTtsModeOptions.Labels(
                ShouldShowLocalAiTtsOption(),
                ShouldShowNativeAivisOption(),
                ShouldShowNativeVoicevoxOption(),
                includeHttpTts,
                httpTtsAvailableOrUnknown);
            var capabilitySnapshot = chatPanel != null ? chatPanel.CurrentCapabilitySnapshot() : null;
            for (var i = 0; i < options.Length; i++)
            {
                var mode = YuiTtsModeOptions.ModeFromIndex(
                    i,
                    ShouldShowLocalAiTtsOption(),
                    ShouldShowNativeAivisOption(),
                    ShouldShowNativeVoicevoxOption(),
                    includeHttpTts);
                var label = YuiCapabilityDiagnostics.DecorateTtsLabel(options[i], mode, capabilitySnapshot);
                ttsModeDropdown.options.Add(new Dropdown.OptionData(label));
            }
        }

        private void RefreshConversationModeOptions()
        {
            if (conversationModeDropdown == null)
            {
                return;
            }

            var options = chatPanel != null
                ? chatPanel.GetConversationModeOptions()
                : YuiConversationModes.DropdownLabels;
            if (conversationModeDropdown.options.Count == options.Length)
            {
                var same = true;
                for (var i = 0; i < options.Length; i++)
                {
                    if (conversationModeDropdown.options[i].text != options[i])
                    {
                        same = false;
                        break;
                    }
                }
                if (same)
                {
                    return;
                }
            }

            conversationModeDropdown.options.Clear();
            var capabilitySnapshot = chatPanel != null ? chatPanel.CurrentCapabilitySnapshot() : null;
            for (var i = 0; i < options.Length; i++)
            {
                var mode = YuiConversationModes.FromDropdownIndex(i);
                var label = YuiCapabilityDiagnostics.DecorateConversationLabel(options[i], mode, capabilitySnapshot);
                conversationModeDropdown.options.Add(new Dropdown.OptionData(label));
            }
        }

        private void RefreshIrodoriVoiceGenderOptions()
        {
            if (irodoriVoiceGenderDropdown == null)
            {
                return;
            }

            if (irodoriVoiceGenderDropdown.options.Count == 2
                && irodoriVoiceGenderDropdown.options[0].text == "Female base"
                && irodoriVoiceGenderDropdown.options[1].text == "Male base")
            {
                return;
            }

            irodoriVoiceGenderDropdown.options.Clear();
            irodoriVoiceGenderDropdown.options.Add(new Dropdown.OptionData("Female base"));
            irodoriVoiceGenderDropdown.options.Add(new Dropdown.OptionData("Male base"));
        }

        private void RefreshMicrophoneOptions()
        {
            if (microphoneDropdown == null)
            {
                return;
            }

            var options = chatPanel != null
                ? chatPanel.GetMicrophoneDeviceOptions()
                : new[] { "Default" };
            if (microphoneDropdown.options.Count == options.Length)
            {
                var same = true;
                for (var i = 0; i < options.Length; i++)
                {
                    if (microphoneDropdown.options[i].text != options[i])
                    {
                        same = false;
                        break;
                    }
                }
                if (same)
                {
                    return;
                }
            }

            microphoneDropdown.options.Clear();
            foreach (var option in options)
            {
                microphoneDropdown.options.Add(new Dropdown.OptionData(option));
            }
        }

        private void RefreshLookCameraOptions()
        {
            if (lookCameraDropdown == null)
            {
                return;
            }

            var options = chatPanel != null
                ? chatPanel.GetLookCameraDeviceOptions()
                : new[] { "Disabled" };
            if (lookCameraDropdown.options.Count == options.Length)
            {
                var same = true;
                for (var i = 0; i < options.Length; i++)
                {
                    if (lookCameraDropdown.options[i].text != options[i])
                    {
                        same = false;
                        break;
                    }
                }
                if (same)
                {
                    return;
                }
            }

            lookCameraDropdown.options.Clear();
            foreach (var option in options)
            {
                lookCameraDropdown.options.Add(new Dropdown.OptionData(option));
            }
        }

        private void RefreshAvatarOptions()
        {
            if (avatarDropdown == null)
            {
                return;
            }

            var options = chatPanel != null
                ? chatPanel.GetAvatarSlotOptions()
                : new[] { "UnityChan Default", "Custom VRM 1", "Custom VRM 2", "Custom VRM 3", "Custom VRM 4" };
            if (avatarDropdown.options.Count == options.Length)
            {
                var same = true;
                for (var i = 0; i < options.Length; i++)
                {
                    if (avatarDropdown.options[i].text != options[i])
                    {
                        same = false;
                        break;
                    }
                }
                if (same)
                {
                    return;
                }
            }

            avatarDropdown.options.Clear();
            foreach (var option in options)
            {
                avatarDropdown.options.Add(new Dropdown.OptionData(option));
            }
        }

        private void RefreshCustomVrmNameInput()
        {
            if (customVrmNameInput == null || chatPanel == null)
            {
                return;
            }

            var slot = AvatarSlotValue();
            var isCustom = YuiAvatarSlots.IsCustomVrm(slot);
            customVrmNameInput.interactable = isCustom;
            customVrmNameInput.text = isCustom ? chatPanel.GetCustomVrmDisplayName(slot) : string.Empty;
        }

        private void SaveCustomVrmDisplayNameFromInput()
        {
            if (customVrmNameInput == null || chatPanel == null)
            {
                return;
            }

            var slot = AvatarSlotValue();
            if (!YuiAvatarSlots.IsCustomVrm(slot))
            {
                return;
            }

            chatPanel.SetCustomVrmDisplayName(slot, customVrmNameInput.text);
            RefreshAvatarOptions();
        }

        private void RefreshResolutionOptions()
        {
            if (resolutionDropdown == null)
            {
                return;
            }

            var options = YuiWindowResolutionController.Options;
            if (resolutionDropdown.options.Count == options.Length)
            {
                var labelsMatch = true;
                for (var i = 0; i < options.Length; i++)
                {
                    if (resolutionDropdown.options[i].text != options[i].Label)
                    {
                        labelsMatch = false;
                        break;
                    }
                }

                if (labelsMatch)
                {
                    return;
                }
            }

            resolutionDropdown.options.Clear();
            foreach (var option in options)
            {
                resolutionDropdown.options.Add(new Dropdown.OptionData(option.Label));
            }
        }

        private void RefreshCameraPresetOptions()
        {
            if (cameraPresetDropdown == null || cameraPresetDropdown.options.Count == 5)
            {
                return;
            }

            cameraPresetDropdown.options.Clear();
            cameraPresetDropdown.options.Add(new Dropdown.OptionData("Auto"));
            cameraPresetDropdown.options.Add(new Dropdown.OptionData("Cam 1"));
            cameraPresetDropdown.options.Add(new Dropdown.OptionData("Cam 2"));
            cameraPresetDropdown.options.Add(new Dropdown.OptionData("Cam 3"));
            cameraPresetDropdown.options.Add(new Dropdown.OptionData("Cam 4"));
        }

        private void RefreshVoicePresetOptions()
        {
            if (voicePresetDropdown == null)
            {
                return;
            }

            var presets = YuiVoicePresetStore.Load();
            voicePresetDropdown.options.Clear();
            voicePresetDropdown.options.Add(new Dropdown.OptionData("Manual"));
            foreach (var preset in presets)
            {
                voicePresetDropdown.options.Add(new Dropdown.OptionData(preset.Name));
            }
            if (voicePresetDropdown.value >= voicePresetDropdown.options.Count)
            {
                voicePresetDropdown.SetValueWithoutNotify(0);
            }
            voicePresetDropdown.RefreshShownValue();
        }

        private YuiVoicePreset VoicePresetAt(int index)
        {
            var presets = YuiVoicePresetStore.Load();
            var presetIndex = index - 1;
            if (presetIndex < 0 || presetIndex >= presets.Count)
            {
                return null;
            }

            return presets[presetIndex];
        }

        private string TtsModeValue()
        {
            if (ttsModeDropdown == null)
            {
                return "server";
            }

            return YuiTtsModeOptions.ModeFromIndex(
                ttsModeDropdown.value,
                ShouldShowLocalAiTtsOption(),
                ShouldShowNativeAivisOption(),
                ShouldShowNativeVoicevoxOption(),
                ShouldShowHttpTtsOption());
        }

        private bool IsAivisTtsSelected()
        {
            var mode = TtsModeValue();
            return string.Equals(mode, "aivis", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "aivis-native", System.StringComparison.OrdinalIgnoreCase);
        }

        private string IrodoriVoiceGenderValue()
        {
            return irodoriVoiceGenderDropdown != null && irodoriVoiceGenderDropdown.value == 1
                ? "male"
                : "female";
        }

        private int ConversationModeIndex(string mode)
        {
            return YuiConversationModes.DropdownIndex(mode);
        }

        private string ConversationModeValue()
        {
            if (conversationModeDropdown == null)
            {
                return YuiConversationModes.Stable;
            }

            return YuiConversationModes.FromDropdownIndex(conversationModeDropdown.value);
        }

        private string MicrophoneValue()
        {
            if (microphoneDropdown == null
                || microphoneDropdown.value < 0
                || microphoneDropdown.value >= microphoneDropdown.options.Count)
            {
                return "Default";
            }

            return microphoneDropdown.options[microphoneDropdown.value].text;
        }

        private string LookCameraValue()
        {
            if (lookCameraDropdown == null
                || lookCameraDropdown.value < 0
                || lookCameraDropdown.value >= lookCameraDropdown.options.Count)
            {
                return "Disabled";
            }

            return lookCameraDropdown.options[lookCameraDropdown.value].text;
        }

        private string AvatarSlotValue()
        {
            if (avatarDropdown == null)
            {
                return YuiAvatarSlots.UnityChanDefault;
            }

            if (chatPanel != null)
            {
                return chatPanel.GetAvatarSlotValueForOptionIndex(avatarDropdown.value);
            }

            switch (avatarDropdown.value)
            {
                case 1:
                    return YuiAvatarSlots.CustomVrm1;
                case 2:
                    return YuiAvatarSlots.CustomVrm2;
                case 3:
                    return YuiAvatarSlots.CustomVrm3;
                case 4:
                    return YuiAvatarSlots.CustomVrm4;
                default:
                    return YuiAvatarSlots.UnityChanDefault;
            }
        }

        private int CameraPresetIndex()
        {
            return cameraPresetDropdown != null ? cameraPresetDropdown.value : 0;
        }

        private int TtsModeIndex(string mode)
        {
            return YuiTtsModeOptions.IndexFromMode(
                mode,
                ShouldShowLocalAiTtsOption(),
                ShouldShowNativeAivisOption(),
                ShouldShowNativeVoicevoxOption(),
                ShouldShowHttpTtsOption());
        }

        private static int IrodoriVoiceGenderIndex(string gender)
        {
            return string.Equals(gender, "male", System.StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        private bool ShouldShowHttpTtsOption()
        {
            return true;
        }

        private static bool ShouldShowLocalAiTtsOption()
        {
            return false;
        }

        private static bool ShouldShowNativeAivisOption()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            var status = YuiAivisNativeBridge.GetStatus();
            return status != null && status.RuntimeReady;
#else
            return false;
#endif
        }

        private static bool ShouldShowNativeVoicevoxOption()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private int MicrophoneIndex(string device)
        {
            if (microphoneDropdown == null || string.IsNullOrWhiteSpace(device))
            {
                return 0;
            }

            for (var i = 0; i < microphoneDropdown.options.Count; i++)
            {
                if (microphoneDropdown.options[i].text == device)
                {
                    return i;
                }
            }

            return 0;
        }

        private int LookCameraIndex(string device)
        {
            if (lookCameraDropdown == null || string.IsNullOrWhiteSpace(device))
            {
                return 0;
            }

            for (var i = 0; i < lookCameraDropdown.options.Count; i++)
            {
                if (lookCameraDropdown.options[i].text == device)
                {
                    return i;
                }
            }

            return 0;
        }

        private System.Collections.Generic.IReadOnlyList<YuiTtsVoiceOption> ActiveVoiceOptions()
        {
            return VoiceOptionsForMode(TtsModeValue());
        }

        private System.Collections.Generic.IReadOnlyList<YuiTtsVoiceOption> VoiceOptionsForMode(string mode)
        {
            return YuiTtsVoiceOptionCatalog.OptionsForMode(mode, chatPanel != null ? chatPanel.BackendAivisVoiceOptions : null);
        }

        private int VoiceIdAt(int index, int fallback)
        {
            return YuiTtsVoiceOptionCatalog.VoiceIdAt(ActiveVoiceOptions(), index, fallback);
        }

        private int VoiceIdAtForMode(string mode, int index, int fallback)
        {
            return YuiTtsVoiceOptionCatalog.VoiceIdAt(VoiceOptionsForMode(mode), index, fallback);
        }

        private int VoiceIndexForId(int speakerId)
        {
            return YuiTtsVoiceOptionCatalog.VoiceIndexForId(ActiveVoiceOptions(), speakerId);
        }

        private int VoiceIndexForIdForMode(string mode, int speakerId)
        {
            return YuiTtsVoiceOptionCatalog.VoiceIndexForId(VoiceOptionsForMode(mode), speakerId);
        }
    }
}
