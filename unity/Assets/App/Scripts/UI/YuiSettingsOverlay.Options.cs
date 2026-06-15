using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiSettingsOverlay
    {
        private static readonly VoiceOption[] VoiceOptions =
        {
            new VoiceOption("冥鳴ひまり / ノーマル", 14),
            new VoiceOption("四国めたん / ノーマル", 2),
            new VoiceOption("四国めたん / あまあま", 0),
            new VoiceOption("四国めたん / ツンツン", 6),
            new VoiceOption("四国めたん / セクシー", 4),
            new VoiceOption("四国めたん / ささやき", 36),
            new VoiceOption("四国めたん / ヒソヒソ", 37),
            new VoiceOption("ずんだもん / ノーマル", 3),
            new VoiceOption("ずんだもん / あまあま", 1),
            new VoiceOption("ずんだもん / ツンツン", 7),
            new VoiceOption("ずんだもん / セクシー", 5),
            new VoiceOption("ずんだもん / ささやき", 22),
            new VoiceOption("ずんだもん / ヒソヒソ", 38),
            new VoiceOption("ずんだもん / ヘロヘロ", 75),
            new VoiceOption("ずんだもん / なみだめ", 76),
        };
        private void RefreshFields()
        {
            if (chatPanel != null)
            {
                if (backendUrlInput != null)
                {
                    backendUrlInput.text = chatPanel.BackendUrl;
                }
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
                    speedSlider.value = chatPanel.VoiceSpeedScale;
                }
                if (pitchSlider != null)
                {
                    pitchSlider.value = chatPanel.VoicePitchScale;
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
                RefreshTtsModeOptions();
                if (ttsModeDropdown != null)
                {
                    ttsModeDropdown.value = TtsModeIndex(chatPanel.TtsMode);
                    ttsModeDropdown.RefreshShownValue();
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
            SetAdvancedVisible(false);
        }
        private void EnsureVoiceOptions()
        {
            if (speakerDropdown == null || speakerDropdown.options.Count == VoiceOptions.Length)
            {
                return;
            }

            speakerDropdown.options.Clear();
            foreach (var option in VoiceOptions)
            {
                speakerDropdown.options.Add(new Dropdown.OptionData(option.Label));
            }
        }

        private void RefreshTtsModeOptions()
        {
            if (ttsModeDropdown == null || ttsModeDropdown.options.Count == 3)
            {
                return;
            }

            ttsModeDropdown.options.Clear();
            ttsModeDropdown.options.Add(new Dropdown.OptionData("Local VOICEVOX"));
            ttsModeDropdown.options.Add(new Dropdown.OptionData("Backend TTS"));
            ttsModeDropdown.options.Add(new Dropdown.OptionData("Silent"));
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
            foreach (var option in options)
            {
                conversationModeDropdown.options.Add(new Dropdown.OptionData(option));
            }
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

        private string TtsModeValue()
        {
            if (ttsModeDropdown == null)
            {
                return "local";
            }

            switch (ttsModeDropdown.value)
            {
                case 1:
                    return "server";
                case 2:
                    return "silent";
                default:
                    return "local";
            }
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

        private static int TtsModeIndex(string mode)
        {
            if (string.Equals(mode, "server", System.StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            if (string.Equals(mode, "silent", System.StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return 0;
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

        private static int VoiceIdAt(int index, int fallback)
        {
            return index >= 0 && index < VoiceOptions.Length ? VoiceOptions[index].Id : fallback;
        }

        private static int VoiceIndexForId(int speakerId)
        {
            for (var i = 0; i < VoiceOptions.Length; i++)
            {
                if (VoiceOptions[i].Id == speakerId)
                {
                    return i;
                }
            }

            return 0;
        }

        private struct VoiceOption
        {
            public string Label;
            public int Id;

            public VoiceOption(string label, int id)
            {
                Label = label;
                Id = id;
            }
        }
    }
}
