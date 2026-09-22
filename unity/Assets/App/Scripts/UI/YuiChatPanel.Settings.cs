using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Audio;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        public void ApplyRuntimeSettings(
            string nextBackendUrl,
            int nextSpeakerId,
            float nextVoiceVolume,
            float nextSpeedScale,
            float nextPitchScale,
            float nextIntonationScale,
            float nextSynthesisVolumeScale,
            float nextPrePhonemeLength,
            float nextPostPhonemeLength,
            string nextConversationMode = null,
            string nextTtsMode = null,
            string nextIrodoriVoiceGender = null,
            string nextIrodoriVoiceInstruct = null,
            string nextMicrophoneDevice = null,
            string nextLookCameraDevice = null)
        {
            if (!string.IsNullOrWhiteSpace(nextBackendUrl))
            {
                if (!string.Equals(backendUrl, nextBackendUrl.Trim(), StringComparison.Ordinal))
                {
                    cachedProviderStatus = null;
                    providerStatusUrl = null;
                    routingBackendHealth = null;
                    lastBackendSuccessAt = -999f;
                    backendConfigLoaded = false;
                    ttsProviderOptions = Array.Empty<string>();
                }
                backendUrl = nextBackendUrl.Trim();
                client = new YuiBackendClient(backendUrl);
                ConfigureAiRuntimeRouter();
                PlayerPrefs.SetString(BackendUrlKey, backendUrl);
            }

            ttsMode = NormalizeTtsMode(nextTtsMode ?? ttsMode);
            var safeTuning = YuiTtsTuningPrefs.Sanitize(ttsMode, new YuiSavedTtsTuning(
                nextSpeakerId,
                nextSpeedScale,
                nextPitchScale,
                nextIntonationScale,
                nextSynthesisVolumeScale,
                nextPrePhonemeLength,
                nextPostPhonemeLength));
            var voiceSettings = new YuiVoiceSettings(
                safeTuning.SpeakerId,
                safeTuning.SpeedScale,
                safeTuning.PitchScale,
                safeTuning.IntonationScale,
                safeTuning.SynthesisVolumeScale,
                safeTuning.PrePhonemeLength,
                safeTuning.PostPhonemeLength);
            speakerId = voiceSettings.SpeakerId;
            speedScale = voiceSettings.SpeedScale;
            pitchScale = voiceSettings.PitchScale;
            intonationScale = voiceSettings.IntonationScale;
            synthesisVolumeScale = voiceSettings.SynthesisVolumeScale;
            prePhonemeLength = voiceSettings.PrePhonemeLength;
            postPhonemeLength = voiceSettings.PostPhonemeLength;
            var previousConversationMode = conversationMode;
            conversationMode = NormalizeConversationMode(nextConversationMode ?? conversationMode);
            var conversationModeChanged = !string.Equals(previousConversationMode, conversationMode, StringComparison.OrdinalIgnoreCase);
            ConfigureAiRuntimeRouter();
            irodoriVoiceGender = NormalizeIrodoriVoiceGender(nextIrodoriVoiceGender ?? irodoriVoiceGender);
            irodoriVoiceInstruct = NormalizeIrodoriVoiceInstruct(nextIrodoriVoiceInstruct ?? irodoriVoiceInstruct);
            preferredMicrophoneDevice = nextMicrophoneDevice ?? preferredMicrophoneDevice;
            if (preferredMicrophoneDevice == "Default")
            {
                preferredMicrophoneDevice = "";
            }
            preferredLookCameraDevice = NormalizeLookCameraDevice(nextLookCameraDevice ?? preferredLookCameraDevice);
            PlayerPrefs.SetInt(SpeakerIdKey, speakerId);
            PlayerPrefs.SetFloat(VoiceSpeedKey, speedScale);
            PlayerPrefs.SetFloat(VoicePitchKey, pitchScale);
            PlayerPrefs.SetFloat(VoiceIntonationKey, intonationScale);
            PlayerPrefs.SetFloat(VoiceSynthesisVolumeKey, synthesisVolumeScale);
            PlayerPrefs.SetFloat(VoicePrePhonemeLengthKey, prePhonemeLength);
            PlayerPrefs.SetFloat(VoicePostPhonemeLengthKey, postPhonemeLength);
            PlayerPrefs.SetString(ConversationModeKey, conversationMode);
            PlayerPrefs.SetString(TtsModeKey, ttsMode);
            PlayerPrefs.SetInt(VoiceTuningSchemaVersionKey, CurrentVoiceTuningSchemaVersion);
            YuiTtsTuningPrefs.SaveForMode(ttsMode, new YuiSavedTtsTuning(
                speakerId,
                speedScale,
                pitchScale,
                intonationScale,
                synthesisVolumeScale,
                prePhonemeLength,
                postPhonemeLength));
            PlayerPrefs.SetString(IrodoriVoiceGenderKey, irodoriVoiceGender);
            PlayerPrefs.SetString(IrodoriVoiceInstructKey, irodoriVoiceInstruct);
            PlayerPrefs.SetString(MicrophoneDeviceKey, preferredMicrophoneDevice);
            PlayerPrefs.SetString(LookCameraDeviceKey, preferredLookCameraDevice);

            var volume = Mathf.Clamp01(nextVoiceVolume);
            PlayerPrefs.SetFloat(VoiceVolumeKey, volume);
            if (audioSource != null)
            {
                audioSource.volume = volume;
            }

            if (chatdollKitVoicevoxTts != null)
            {
                ConfigureChatdollKitVoicevoxTts();
            }

            if (!isRecording)
            {
                activeMicrophoneDevice = SelectMicrophoneDevice();
            }

            PlayerPrefs.Save();
            if (conversationModeChanged)
            {
                SyncRealtimeActiveBackendModeWithConversation();
                if (isRecording || realtimeStreamActive || realtimeSocket != null)
                {
                    StopRealtimeForModeChange();
                }
            }

            if (conversationModeChanged && !string.Equals(conversationMode, YuiConversationModes.Stable, StringComparison.OrdinalIgnoreCase))
            {
                AppendLog("System", YuiConversationModes.ExperimentalWarningText(conversationMode));
            }
            SaveCharacterProfile();
            EnsureBackendMonitorIfNeeded();
            SetStatus("Settings saved");
        }

        private void LoadSavedRuntimeSettings()
        {
            backendUrl = PlayerPrefs.GetString(BackendUrlKey, backendUrl);
            openAiApiKey = YuiApiKeyStore.Read();
            openAiModel = YuiDirectOpenAiClient.NormalizeModel(PlayerPrefs.GetString(OpenAiModelKey, openAiModel));
            autoAiFallbackEnabled = PlayerPrefs.GetInt(AutoAiFallbackEnabledKey, 1) == 1;
            speakerId = PlayerPrefs.GetInt(SpeakerIdKey, speakerId);
            speedScale = PlayerPrefs.GetFloat(VoiceSpeedKey, speedScale);
            pitchScale = PlayerPrefs.GetFloat(VoicePitchKey, pitchScale);
            intonationScale = PlayerPrefs.GetFloat(VoiceIntonationKey, intonationScale);
            synthesisVolumeScale = PlayerPrefs.GetFloat(VoiceSynthesisVolumeKey, synthesisVolumeScale);
            prePhonemeLength = PlayerPrefs.GetFloat(VoicePrePhonemeLengthKey, prePhonemeLength);
            postPhonemeLength = PlayerPrefs.GetFloat(VoicePostPhonemeLengthKey, postPhonemeLength);
            conversationMode = NormalizeConversationMode(PlayerPrefs.GetString(ConversationModeKey, DefaultConversationMode()));
            chatInteractionMode = YuiChatRequestModes.Normalize(
                PlayerPrefs.GetString(ChatInteractionModeKey, YuiChatRequestModes.Talk));
            SyncRealtimeActiveBackendModeWithConversation();
            ttsMode = NormalizeTtsMode(PlayerPrefs.GetString(TtsModeKey, DefaultTtsMode()));
            MigrateVoiceTuningIfNeeded();
            var tuning = YuiTtsTuningPrefs.LoadForMode(ttsMode, new YuiSavedTtsTuning(
                speakerId,
                speedScale,
                pitchScale,
                intonationScale,
                synthesisVolumeScale,
                prePhonemeLength,
                postPhonemeLength));
            speakerId = tuning.SpeakerId;
            speedScale = tuning.SpeedScale;
            pitchScale = tuning.PitchScale;
            intonationScale = tuning.IntonationScale;
            synthesisVolumeScale = tuning.SynthesisVolumeScale;
            prePhonemeLength = tuning.PrePhonemeLength;
            postPhonemeLength = tuning.PostPhonemeLength;
            irodoriVoiceGender = NormalizeIrodoriVoiceGender(PlayerPrefs.GetString(IrodoriVoiceGenderKey, irodoriVoiceGender));
            irodoriVoiceInstruct = NormalizeIrodoriVoiceInstruct(PlayerPrefs.GetString(IrodoriVoiceInstructKey, irodoriVoiceInstruct));
            preferredMicrophoneDevice = PlayerPrefs.GetString(MicrophoneDeviceKey, preferredMicrophoneDevice);
            preferredLookCameraDevice = NormalizeLookCameraDevice(PlayerPrefs.GetString(LookCameraDeviceKey, preferredLookCameraDevice));
            secretMode = PlayerPrefs.GetInt(SecretModeKey, 0) == 1;
            characterName = PlayerPrefs.GetString(CharacterNameKey, characterName);
            customInstruction = PlayerPrefs.GetString(CustomInstructionKey, customInstruction);
            var defaultAvatarSlot = GetDefaultAvatarSlot();
            var savedAvatarSlot = YuiAvatarSelectionPrefs.Read(defaultAvatarSlot);
            savedAvatarSlot = UpgradeDefaultAvatarSlot(savedAvatarSlot, defaultAvatarSlot);
            avatarSlot = NormalizeAvatarSlot(savedAvatarSlot);
            if (!string.Equals(savedAvatarSlot, avatarSlot, StringComparison.OrdinalIgnoreCase))
            {
                PlayerPrefs.SetString(AvatarSlotPrefsKey, avatarSlot);
                PlayerPrefs.Save();
            }
            SelectCharacterProfile(true);
        }

        public void SetDirectOpenAiSettings(string apiKey, string model)
        {
            openAiApiKey = string.IsNullOrWhiteSpace(apiKey) ? string.Empty : apiKey.Trim();
            openAiModel = YuiDirectOpenAiClient.NormalizeModel(model);
            directOpenAiClient = null;
            var keySaved = YuiApiKeyStore.Write(openAiApiKey);
            PlayerPrefs.SetString(OpenAiModelKey, openAiModel);
            PlayerPrefs.Save();
            ConfigureAiRuntimeRouter();
            if (!keySaved) ShowKeyStorageError();
            SetStatus(keySaved ? "Settings saved" : "API key not saved");
        }

        public void SetAutoAiFallbackEnabled(bool enabled)
        {
            autoAiFallbackEnabled = enabled;
            PlayerPrefs.SetInt(AutoAiFallbackEnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            ConfigureAiRuntimeRouter();
            SetStatus("Settings saved");
        }

        private void MigrateVoiceTuningIfNeeded()
        {
            if (PlayerPrefs.GetInt(VoiceTuningSchemaVersionKey, 0) >= CurrentVoiceTuningSchemaVersion)
            {
                return;
            }

            var rawSavedTtsMode = PlayerPrefs.GetString(TtsModeKey, string.Empty);
            var savedTtsMode = NormalizeTtsMode(rawSavedTtsMode);
#if UNITY_IOS
            var voicevoxMode = "voicevox-native";
#else
            var voicevoxMode = "server";
#endif
            var shouldMoveSavedModeToVoicevox = string.IsNullOrWhiteSpace(rawSavedTtsMode)
                || string.Equals(rawSavedTtsMode, "local", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawSavedTtsMode, "local-ai", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawSavedTtsMode, "liquid-audio", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawSavedTtsMode, "on-device-audio", StringComparison.OrdinalIgnoreCase)
                || string.Equals(savedTtsMode, "local", StringComparison.OrdinalIgnoreCase)
                || string.Equals(savedTtsMode, "local-ai", StringComparison.OrdinalIgnoreCase);
            var voicevoxDefaults = YuiTtsTuningPrefs.DefaultForMode("server");
            var aivisDefaults = YuiTtsTuningPrefs.DefaultForMode("aivis");
            YuiTtsTuningPrefs.SaveForMode("server", voicevoxDefaults);
            YuiTtsTuningPrefs.SaveForMode("aivis", aivisDefaults);
            var activeDefaults = shouldMoveSavedModeToVoicevox ? voicevoxDefaults : YuiTtsTuningPrefs.DefaultForMode(savedTtsMode);
            if (shouldMoveSavedModeToVoicevox)
            {
                PlayerPrefs.SetString(TtsModeKey, voicevoxMode);
                ttsMode = voicevoxMode;
            }

            PlayerPrefs.SetInt(SpeakerIdKey, activeDefaults.SpeakerId);
            PlayerPrefs.SetFloat(VoiceSpeedKey, activeDefaults.SpeedScale);
            PlayerPrefs.SetFloat(VoicePitchKey, activeDefaults.PitchScale);
            PlayerPrefs.SetFloat(VoiceIntonationKey, activeDefaults.IntonationScale);
            PlayerPrefs.SetFloat(VoiceSynthesisVolumeKey, activeDefaults.SynthesisVolumeScale);
            PlayerPrefs.SetFloat(VoicePrePhonemeLengthKey, activeDefaults.PrePhonemeLength);
            PlayerPrefs.SetFloat(VoicePostPhonemeLengthKey, activeDefaults.PostPhonemeLength);
            PlayerPrefs.SetInt(VoiceTuningSchemaVersionKey, CurrentVoiceTuningSchemaVersion);
            PlayerPrefs.Save();
        }

        private static string DefaultConversationMode()
        {
            return YuiConversationModes.LocalAi;
        }

        private static string DefaultTtsMode()
        {
#if UNITY_IOS
            return "voicevox-native";
#else
            return "server";
#endif
        }

        public void SetCustomInstruction(string value)
        {
            customInstruction = (value ?? string.Empty).Trim();
            SaveCharacterProfile();
            PlayerPrefs.SetString(CustomInstructionKey, customInstruction);
            PlayerPrefs.Save();
            SetStatus("Settings saved");
        }

        public void SetCharacterName(string value)
        {
            characterName = string.IsNullOrWhiteSpace(value) ? "Yui" : value.Trim();
            SaveCharacterProfile();
            PlayerPrefs.SetString(CharacterNameKey, characterName);
            PlayerPrefs.Save();
            SetStatus("Settings saved");
        }

        public async void SetAvatarSlot(string value) => await SetAvatarSlotAsync(value);

        public async Task<bool> SetAvatarSlotAsync(string value)
        {
            try
            {
            var selected = NormalizeAvatarSlot(value);
            if (selected != avatarSlot && !CanChangeCharacter()) return false;
            if (YuiAvatarSlots.IsCustomVrm(selected))
            {
                var path = runtimeVrmImporter?.GetCustomVrmPath(selected);
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                {
                    SetStatus("This appearance is empty. Import an avatar first.");
                    return false;
                }
                if (avatarSwitcher == null || !avatarSwitcher.HasCustomAvatar || avatarSwitcher.CustomAvatarSlot != selected)
                {
                    var id = runtimeVrmImporter.GetCharacterId(selected);
                    var loaded = await runtimeVrmImporter.ImportFromPathAsync(path, true, selected,
                        id.StartsWith("builtin:", StringComparison.Ordinal) ? null : id);
                    if (!loaded) { SetStatus(runtimeVrmImporter.LastImportMessage); return false; }
                }
            }
            avatarSlot = selected;
            PlayerPrefs.SetString(AvatarSlotPrefsKey, avatarSlot);
            PlayerPrefs.Save();
            ApplyAvatarSlot(true);
            SelectCharacterProfile();
            return true;
            }
            catch (Exception ex) { SetStatus("Could not load this appearance: " + ex.Message); return false; }
        }

        public async void ImportCustomVrmFromFilePicker() => await ImportAvatarAsync();
        public async void ImportCustomVrmIntoSlot(string requestedSlot) => await ImportAvatarAsync(requestedSlot);

        public async System.Threading.Tasks.Task<bool> ImportAvatarAsync(string requestedSlot = null, string characterId = null)
        {
            if (!CanChangeCharacter()) return false;
            if (runtimeVrmImporter == null)
                runtimeVrmImporter = GetComponent<YuiRuntimeVrmImporter>() ?? YuiSceneObjectFinder.FindFirst<YuiRuntimeVrmImporter>();
            if (runtimeVrmImporter == null)
            {
                ShowAvatarError("Custom avatar importer is not configured", () => ImportCustomVrmFromFilePicker());
                return false;
            }
            var targetSlot = YuiAvatarSlots.IsCustomVrm(requestedSlot) ? requestedSlot : YuiAvatarSlots.CustomVrm1;
            var imported = await runtimeVrmImporter.ImportFromFilePickerAsync(targetSlot, characterId);
            if (!imported)
            {
                if (!runtimeVrmImporter.LastImportCanceled)
                    ShowAvatarError(runtimeVrmImporter.LastImportMessage, () => { _ = ImportAvatarAsync(targetSlot, characterId); });
                return false;
            }
            SetAvatarSlot(targetSlot);
            RefreshCharacterSettings();
            SetStatus(runtimeVrmImporter.LastImportMessage);
            return true;
        }

        private void RefreshCharacterSettings()
        {
            var settings = YuiSceneObjectFinder.FindFirst<YuiSettingsOverlay>();
            if (settings == null || !settings.RefreshCharacterSelection()) FocusDesktopComposer();
        }

        private void ShowAvatarError(string message, UnityEngine.Events.UnityAction retry)
        {
            var root = CreateSavedDataPanel("Could not load avatar");
            SavedDataText(root, YuiUiLocalization.Text(message));
            ComposerButton(root, "Retry", "Try again", () => { Destroy(root.gameObject); retry?.Invoke(); }, .04f, .06f, .60f, .18f);
            ComposerButton(root, "Library", "My characters", ShowAvatarLibrary, .64f, .06f, .96f, .18f);
        }

        public void ClearCustomVrmSlot(string slot)
        {
            slot = YuiAvatarSlots.IsCustomVrm(slot) ? YuiAvatarSlots.Normalize(slot) : YuiAvatarSlots.CustomVrm1;
            if (runtimeVrmImporter == null)
            {
                runtimeVrmImporter = GetComponent<YuiRuntimeVrmImporter>() ?? YuiSceneObjectFinder.FindFirst<YuiRuntimeVrmImporter>();
            }

            runtimeVrmImporter?.ClearCustomVrmSlot(slot);
            if (string.Equals(avatarSlot, slot, StringComparison.OrdinalIgnoreCase))
            {
                avatarSlot = GetDefaultAvatarSlot();
                PlayerPrefs.SetString(AvatarSlotPrefsKey, avatarSlot);
                PlayerPrefs.Save();
                ApplyAvatarSlot(false);
                SelectCharacterProfile();
            }

            SetStatus($"{GetCustomVrmDisplayName(slot)} cleared");
        }

        public string[] GetAvatarSlotOptions()
        {
            var hasDemoAvatar = YuiBuildProfile.Current != YuiBuildProfile.Public && avatarSwitcher != null && avatarSwitcher.HasDemoAvatar;
            if (!hasDemoAvatar)
            {
                var options = new System.Collections.Generic.List<string> { "Demo Avatar" };
                for (var index = 1; index <= 3; index++) options.Add(GetCustomVrmOptionLabel(YuiAvatarSlots.CustomVrmSlot(index)));
                if (!string.IsNullOrWhiteSpace(runtimeVrmImporter?.GetCustomVrmPath(YuiAvatarSlots.CustomVrm4)))
                    options.Add(GetCustomVrmOptionLabel(YuiAvatarSlots.CustomVrm4));
                return options.ToArray();
            }

            return new[]
            {
                "Personal Avatar",
                "Demo Avatar",
                GetCustomVrmOptionLabel(YuiAvatarSlots.CustomVrm1),
                GetCustomVrmOptionLabel(YuiAvatarSlots.CustomVrm2),
                GetCustomVrmOptionLabel(YuiAvatarSlots.CustomVrm3),
                GetCustomVrmOptionLabel(YuiAvatarSlots.CustomVrm4)
            };
        }

        public string GetAvatarSlotValueForOptionIndex(int index)
        {
            var options = GetAvatarSlotOptions();
            if (index < 0 || index >= options.Length)
            {
                return GetDefaultAvatarSlot();
            }

            var hasDemoAvatar = YuiBuildProfile.Current != YuiBuildProfile.Public && avatarSwitcher != null && avatarSwitcher.HasDemoAvatar;
            if (hasDemoAvatar)
            {
                if (index == 0)
                {
                    return YuiAvatarSlots.DemoAvatar;
                }

                if (index == 1)
                {
                    return YuiAvatarSlots.UnityChanDefault;
                }

                return YuiAvatarSlots.CustomVrmSlot(index - 1);
            }

            if (index == 0)
            {
                return YuiAvatarSlots.UnityChanDefault;
            }

            return YuiAvatarSlots.CustomVrmSlot(index);
        }

        public int GetAvatarSlotOptionIndex(string slot)
        {
            var normalized = NormalizeAvatarSlot(slot);
            var options = GetAvatarSlotOptions();
            for (var i = 0; i < options.Length; i++)
            {
                var optionSlot = GetAvatarSlotValueForOptionIndex(i);
                if (string.Equals(optionSlot, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }

        private string GetCustomVrmOptionLabel(string slot)
        {
            var fallback = $"Custom VRM {YuiAvatarSlots.CustomVrmIndex(slot)}";
            var path = runtimeVrmImporter?.GetCustomVrmPath(slot);
            if (string.IsNullOrWhiteSpace(path)) return fallback + " · Empty";
            if (!File.Exists(path)) return fallback + " · File missing";
            return GetCustomVrmDisplayName(slot);
        }

        public string GetCustomVrmDisplayName(string slot)
        {
            var fallback = $"Custom VRM {YuiAvatarSlots.CustomVrmIndex(slot)}";
            var saved = PlayerPrefs.GetString(CustomVrmNamePrefsKey(slot), fallback);
            var id = PlayerPrefs.GetString("Yui.CharacterId." + slot, "");
            var entry = YuiAvatarLibrary.Read().Find(item => item.id == id);
            var appearance = entry?.appearances?.Find(item => item.file == entry.file);
            return !string.IsNullOrWhiteSpace(appearance?.name) ? appearance.name
                : !string.IsNullOrWhiteSpace(entry?.name) ? entry.name : saved;
        }

        public void SetCustomVrmDisplayName(string slot, string value)
        {
            if (!YuiAvatarSlots.IsCustomVrm(slot))
            {
                return;
            }

            var index = YuiAvatarSlots.CustomVrmIndex(slot);
            var fallback = $"Custom VRM {index}";
            var name = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            var id = PlayerPrefs.GetString("Yui.CharacterId." + slot, "");
            var entry = YuiAvatarLibrary.Read().Find(item => item.id == id);
            if (entry != null) new YuiAvatarLibraryStore(YuiAvatarLibrary.DirectoryPath).RenameAppearance(entry.id, entry.file, name);
            else PlayerPrefs.SetString(CustomVrmNamePrefsKey(slot), name);
            PlayerPrefs.Save();
        }

        private static string CustomVrmNamePrefsKey(string slot)
        {
            return $"{YuiPrefsKeys.CustomVrmNamePrefix}.{YuiAvatarSlots.CustomVrmIndex(slot)}";
        }

        public string[] GetConversationModeOptions()
        {
            return YuiConversationModes.DropdownLabels;
        }

        public void SetSecretMode(bool enabled)
        {
            if (enabled != secretMode) { retryChatMessage = null; if (HasStoppableComposerOperation) StopComposerOperation(); }
            secretMode = enabled;
            if (!secretMode) MigrateRecentDialogue();
            if(savedDataPanel!=null) Destroy(savedDataPanel);
            _=RestoreConversationViewAsync();
            PlayerPrefs.SetInt(SecretModeKey, secretMode ? 1 : 0);
            PlayerPrefs.Save();
            UpdateSecretModeUi();
            SetStatus(currentStatus);
        }

        public async void ClearConversationCache()
        {
            await ClearConversationCacheAsync();
        }

        public async Task ClearConversationCacheAsync()
        {
            if (client == null)
            {
                client = new YuiBackendClient(backendUrl);
            }

            try
            {
                SetStatus("Clearing...");
                var result = await client.ClearConversationsAsync(userId, cancellationTokenSource.Token);
                await RestoreConversationViewAsync();
                SetStatus("Backend history cleared");
                Debug.Log(
                    $"Yui session cleared: conversations={result?.Conversations ?? 0}, cache={result?.ChatResponses ?? 0}, memories={result?.Memories ?? 0}");
            }
            catch (Exception ex)
            {
                SetStatus("Clear failed");
                var errorMessage = ex is YuiBackendException backendException
                    ? backendException.UserMessage
                    : ex.Message;
                AppendLog("System", errorMessage);
                Debug.LogError(ex);
            }
        }

        public string[] GetMicrophoneDeviceOptions()
        {
            if (microphoneDeviceSelector == null)
            {
                microphoneDeviceSelector = new YuiMicrophoneDeviceSelector(preferredRecordingFrequency);
            }
            return microphoneDeviceSelector.GetOptions();
        }

        public string[] GetLookCameraDeviceOptions()
        {
            var devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                Debug.Log("Yui Look cameras: none detected");
                return new[] { "Disabled" };
            }

            var options = new string[devices.Length + 1];
            options[0] = "Disabled";
            for (var i = 0; i < devices.Length; i++)
            {
                options[i + 1] = string.IsNullOrWhiteSpace(devices[i].name)
                    ? $"Camera {i + 1}"
                    : devices[i].name;
            }
            Debug.Log("Yui Look cameras: " + string.Join(", ", options));
            return options;
        }


        private bool IsRealtimeConversationMode()
        {
            return YuiConversationModes.IsRealtime(conversationMode);
        }

        private bool IsRealtimeVoicevoxMode()
        {
            return YuiConversationModes.IsRealtimeVoicevox(conversationMode);
        }

        private bool IsRealtimeTextTtsMode()
        {
            return YuiConversationModes.IsRealtimeTextTts(conversationMode);
        }

        private bool IsRealtimeTranslateMode()
        {
            return YuiConversationModes.IsRealtimeTranslate(conversationMode);
        }

        private string RealtimeBackendMode()
        {
            return YuiConversationModes.BackendMode(conversationMode);
        }

        private void SyncRealtimeActiveBackendModeWithConversation()
        {
            realtimeActiveBackendMode = RealtimeBackendMode();
        }

        private string RealtimeInstructionsForMode(string mode)
        {
            return YuiConversationModes.InstructionsForMode(mode, characterName, customInstruction);
        }

        private bool IsTtsMode(string mode)
        {
            return string.Equals(ttsMode, mode, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeTtsMode(string mode)
        {
            if (YuiPhysicalAI.LocalAI.YuiPlatformSpeechBridge.CanSynthesize && string.Equals(mode, "local-ai", StringComparison.OrdinalIgnoreCase))
                return "local-ai";
            if (string.Equals(mode, "aivis-native", StringComparison.OrdinalIgnoreCase))
            {
                return "aivis-native";
            }

            if (string.Equals(mode, "voicevox-native", StringComparison.OrdinalIgnoreCase))
            {
                return "voicevox-native";
            }

#if UNITY_IOS || UNITY_ANDROID
            if (string.Equals(mode, "local-ai", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "liquid-audio", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "on-device-audio", StringComparison.OrdinalIgnoreCase))
            {
                return "server";
            }
#endif
            if (string.Equals(mode, "server", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "local", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "server-http", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "aivis", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "aivis-native", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "voicevox-native", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "local-ai", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "liquid-audio", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "on-device-audio", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "silent", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(mode, "liquid-audio", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mode, "on-device-audio", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mode, "local-ai", StringComparison.OrdinalIgnoreCase))
                {
                    return "server";
                }

                if (string.Equals(mode, "local", StringComparison.OrdinalIgnoreCase))
                {
                    return "local";
                }

                if (string.Equals(mode, "server-http", StringComparison.OrdinalIgnoreCase))
                {
                    return "server-http";
                }

                return mode.ToLowerInvariant();
            }

            return "server";
        }

        private static string NormalizeIrodoriVoiceInstruct(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "若い女性の、明るく可愛いアニメ調の声で話してください。"
                : value.Trim();
        }

        private static string NormalizeIrodoriVoiceGender(string value)
        {
            return string.Equals(value, "male", StringComparison.OrdinalIgnoreCase)
                ? "male"
                : "female";
        }

        private static string NormalizeConversationMode(string mode)
        {
            return YuiConversationModes.Normalize(mode);
        }

        private static string NormalizeLookCameraDevice(string device)
        {
            if (string.IsNullOrWhiteSpace(device)
                || string.Equals(device, "Default", StringComparison.OrdinalIgnoreCase)
                || string.Equals(device, "Disabled", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            return device.Trim();
        }

        private static string NormalizeAvatarSlot(string value)
        {
            return YuiAvatarSlots.NormalizeForProfile(value, YuiBuildProfile.Current == YuiBuildProfile.Public);
        }

        private void ToggleSecretMode()
        {
            SetSecretMode(!secretMode);
        }

        private void UpdateSecretModeUi()
        {
            if (secretModeButtonText != null)
            {
                secretModeButtonText.text = "S";
                secretModeButtonText.color = Color.white;
            }

            if (secretModeButton != null)
            {
                var image = secretModeButton.GetComponent<Image>();
                if (image != null)
                {
                    image.color = secretMode
                        ? YuiUiTheme.Selected
                        : YuiUiTheme.Field;
                }
            }

            if (secretModeIndicatorText != null)
            {
                secretModeIndicatorText.gameObject.SetActive(false);
            }

            RenderStatus();
        }

        private RequestContext CreateChatContext()
        {
            var context = new RequestContext();
            if (!secretMode)
            {
                try { context.Extra[YuiCharacterDialogueStore.ContextKey] = DialogueStore.Context(ChatCharacterId(), chatInteractionMode); }
                catch (Exception ex) { Debug.LogWarning("Recent character dialogue: " + ex.Message); }
            }
            if (latestVision != null)
            {
                context.VisionResultId = latestVision.VisionResultId;
                context.ScreenContext = latestVision.Summary;
            }

            pendingVisionImageAttachment.ApplyTo(context);

            if (EnableDormantAppAwarenessPrototype && appAwarenessEnabled && currentForegroundApp != null && currentForegroundApp.IsAvailable)
            {
                context.Extra["foreground_app"] = new Dictionary<string, object>
                {
                    ["category"] = currentForegroundApp.Category,
                    ["display_name"] = currentForegroundApp.DisplayName,
                    ["process_name"] = currentForegroundApp.ProcessName
                };
            }

            return context;
        }

        private string FormatBackendStatus(HealthResponse health)
        {
            if (health == null)
            {
                return "Backend offline";
            }

            var status = string.IsNullOrWhiteSpace(health.Status) ? "unknown" : health.Status;
            if (!string.IsNullOrWhiteSpace(health.MinClientSchemaVersion)
                && string.CompareOrdinal(ClientSchemaVersion, health.MinClientSchemaVersion) < 0)
            {
                return "Update needed";
            }

            return string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase)
                ? "Connected"
                : "Backend degraded";
        }

        private void LogBackendDiagnostics(HealthResponse health)
        {
            if (health == null)
            {
                return;
            }

            var providerSummary = FormatDiagnostics("providers", health.Providers);
            var featureSummary = FormatDiagnostics("features", health.Features);
            Debug.Log(
                $"Yui backend diagnostics: version={health.Version}, schema={health.ApiSchemaVersion}, min_client={health.MinClientSchemaVersion}, database={health.Database}, {providerSummary}, {featureSummary}");
        }

        private static string FormatDiagnostics<TValue>(string label, Dictionary<string, TValue> values)
        {
            if (values == null || values.Count == 0)
            {
                return $"{label}=unknown";
            }

            var builder = new StringBuilder();
            builder.Append(label);
            builder.Append('=');
            var first = true;
            foreach (var pair in values)
            {
                if (!first)
                {
                    builder.Append(", ");
                }

                builder.Append(pair.Key);
                builder.Append(':');
                builder.Append(pair.Value);
                first = false;
            }

            return builder.ToString();
        }

    }
}
