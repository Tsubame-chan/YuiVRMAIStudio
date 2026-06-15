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
            string nextMicrophoneDevice = null,
            string nextLookCameraDevice = null)
        {
            if (!string.IsNullOrWhiteSpace(nextBackendUrl))
            {
                backendUrl = nextBackendUrl.Trim();
                client = new YuiBackendClient(backendUrl);
                PlayerPrefs.SetString(BackendUrlKey, backendUrl);
            }

            var voiceSettings = new YuiVoiceSettings(
                nextSpeakerId,
                nextSpeedScale,
                nextPitchScale,
                nextIntonationScale,
                nextSynthesisVolumeScale,
                nextPrePhonemeLength,
                nextPostPhonemeLength);
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
            ttsMode = NormalizeTtsMode(nextTtsMode ?? ttsMode);
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
            SetStatus("Settings saved");
        }

        private void LoadSavedRuntimeSettings()
        {
            backendUrl = PlayerPrefs.GetString(BackendUrlKey, backendUrl);
            speakerId = PlayerPrefs.GetInt(SpeakerIdKey, speakerId);
            speedScale = PlayerPrefs.GetFloat(VoiceSpeedKey, speedScale);
            pitchScale = PlayerPrefs.GetFloat(VoicePitchKey, pitchScale);
            intonationScale = PlayerPrefs.GetFloat(VoiceIntonationKey, intonationScale);
            synthesisVolumeScale = PlayerPrefs.GetFloat(VoiceSynthesisVolumeKey, synthesisVolumeScale);
            prePhonemeLength = PlayerPrefs.GetFloat(VoicePrePhonemeLengthKey, prePhonemeLength);
            postPhonemeLength = PlayerPrefs.GetFloat(VoicePostPhonemeLengthKey, postPhonemeLength);
            conversationMode = NormalizeConversationMode(PlayerPrefs.GetString(ConversationModeKey, conversationMode));
            SyncRealtimeActiveBackendModeWithConversation();
            ttsMode = NormalizeTtsMode(PlayerPrefs.GetString(TtsModeKey, ttsMode));
            preferredMicrophoneDevice = PlayerPrefs.GetString(MicrophoneDeviceKey, preferredMicrophoneDevice);
            preferredLookCameraDevice = NormalizeLookCameraDevice(PlayerPrefs.GetString(LookCameraDeviceKey, preferredLookCameraDevice));
            secretMode = PlayerPrefs.GetInt(SecretModeKey, 0) == 1;
            characterName = PlayerPrefs.GetString(CharacterNameKey, characterName);
            customInstruction = PlayerPrefs.GetString(CustomInstructionKey, customInstruction);
            var savedAvatarSlot = PlayerPrefs.GetString(AvatarSlotPrefsKey, avatarSlot);
            savedAvatarSlot = UpgradeDefaultAvatarSlot(savedAvatarSlot);
            avatarSlot = NormalizeAvatarSlot(savedAvatarSlot);
            if (!string.Equals(savedAvatarSlot, avatarSlot, StringComparison.OrdinalIgnoreCase))
            {
                PlayerPrefs.SetString(AvatarSlotPrefsKey, avatarSlot);
                PlayerPrefs.Save();
            }
        }

        public void SetCustomInstruction(string value)
        {
            customInstruction = (value ?? string.Empty).Trim();
            PlayerPrefs.SetString(CustomInstructionKey, customInstruction);
            PlayerPrefs.Save();
            SetStatus("Settings saved");
        }

        public void SetCharacterName(string value)
        {
            characterName = string.IsNullOrWhiteSpace(value) ? "Yui" : value.Trim();
            PlayerPrefs.SetString(CharacterNameKey, characterName);
            PlayerPrefs.Save();
            SetStatus("Settings saved");
        }

        public void SetAvatarSlot(string value)
        {
            avatarSlot = NormalizeAvatarSlot(value);
            PlayerPrefs.SetString(AvatarSlotPrefsKey, avatarSlot);
            PlayerPrefs.Save();
            ApplyAvatarSlot(true);
        }

        public async void ImportCustomVrmFromFilePicker()
        {
            if (runtimeVrmImporter == null)
            {
                runtimeVrmImporter = GetComponent<YuiRuntimeVrmImporter>() ?? YuiSceneObjectFinder.FindFirst<YuiRuntimeVrmImporter>();
            }

            if (runtimeVrmImporter == null)
            {
                SetStatus("Custom VRM importer is not configured");
                return;
            }

            SetStatus("Opening VRM...");
            var targetSlot = YuiAvatarSlots.IsCustomVrm(avatarSlot)
                ? avatarSlot
                : YuiAvatarSlots.CustomVrm1;
            var imported = await runtimeVrmImporter.ImportFromFilePickerAsync(targetSlot);
            if (!imported)
            {
                SetStatus(string.IsNullOrWhiteSpace(runtimeVrmImporter.LastImportMessage)
                    ? "Custom VRM import canceled or failed"
                    : runtimeVrmImporter.LastImportMessage);
                return;
            }

            avatarSlot = targetSlot;
            PlayerPrefs.SetString(AvatarSlotPrefsKey, avatarSlot);
            PlayerPrefs.Save();
            SetStatus(string.IsNullOrWhiteSpace(runtimeVrmImporter.LastImportMessage)
                ? "Custom VRM loaded"
                : runtimeVrmImporter.LastImportMessage);
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
                avatarSlot = YuiAvatarSlots.UnityChanDefault;
                PlayerPrefs.SetString(AvatarSlotPrefsKey, avatarSlot);
                PlayerPrefs.Save();
                ApplyAvatarSlot(false);
            }

            SetStatus($"{GetCustomVrmDisplayName(slot)} cleared");
        }

        public string[] GetAvatarSlotOptions()
        {
            return new[]
            {
                "UnityChan Default",
                GetCustomVrmDisplayName(YuiAvatarSlots.CustomVrm1),
                GetCustomVrmDisplayName(YuiAvatarSlots.CustomVrm2),
                GetCustomVrmDisplayName(YuiAvatarSlots.CustomVrm3),
                GetCustomVrmDisplayName(YuiAvatarSlots.CustomVrm4)
            };
        }

        public string GetAvatarSlotValueForOptionIndex(int index)
        {
            var options = GetAvatarSlotOptions();
            if (index < 0 || index >= options.Length)
            {
                return GetDefaultAvatarSlot();
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

        public string GetCustomVrmDisplayName(string slot)
        {
            var index = YuiAvatarSlots.CustomVrmIndex(slot);
            var fallback = $"Custom VRM {index}";
            var saved = PlayerPrefs.GetString(CustomVrmNamePrefsKey(slot), fallback);
            return string.IsNullOrWhiteSpace(saved) ? fallback : saved.Trim();
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
            PlayerPrefs.SetString(CustomVrmNamePrefsKey(slot), name);
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
            secretMode = enabled;
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
                chatLogView?.Clear();
                SetStatus("History cleared");
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
            return YuiConversationModes.InstructionsForMode(mode, characterName);
        }

        private bool IsTtsMode(string mode)
        {
            return string.Equals(ttsMode, mode, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeTtsMode(string mode)
        {
            if (string.Equals(mode, "server", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "silent", StringComparison.OrdinalIgnoreCase))
            {
                return mode.ToLowerInvariant();
            }

            return "local";
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
            return YuiAvatarSlots.Normalize(value);
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
                        ? new Color(0.12f, 0.36f, 0.34f, 0.96f)
                        : new Color(0.08f, 0.10f, 0.13f, 0.78f);
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
            if (latestVision != null)
            {
                context.VisionResultId = latestVision.VisionResultId;
                context.ScreenContext = latestVision.Summary;
            }

            if (!string.IsNullOrEmpty(latestVisionImageDataUrl))
            {
                context.Extra["image_data_url"] = latestVisionImageDataUrl;
                context.Extra["image_detail"] = "auto";
            }

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
