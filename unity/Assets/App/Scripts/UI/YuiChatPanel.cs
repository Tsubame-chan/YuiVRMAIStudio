using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
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
    public sealed class YuiChatPanel : MonoBehaviour
    {
        private const string BackendUrlKey = YuiPrefsKeys.BackendUrl;
        private const string SpeakerIdKey = YuiPrefsKeys.SpeakerId;
        private const string VoiceVolumeKey = YuiPrefsKeys.VoiceVolume;
        private const string VoiceSpeedKey = YuiPrefsKeys.VoiceSpeed;
        private const string VoicePitchKey = YuiPrefsKeys.VoicePitch;
        private const string VoiceIntonationKey = YuiPrefsKeys.VoiceIntonation;
        private const string VoiceSynthesisVolumeKey = YuiPrefsKeys.VoiceSynthesisVolume;
        private const string VoicePrePhonemeLengthKey = YuiPrefsKeys.VoicePrePhonemeLength;
        private const string VoicePostPhonemeLengthKey = YuiPrefsKeys.VoicePostPhonemeLength;
        private const string ConversationModeKey = YuiPrefsKeys.ConversationMode;
        private const string TtsModeKey = YuiPrefsKeys.TtsMode;
        private const string MicrophoneDeviceKey = YuiPrefsKeys.MicrophoneDevice;
        private const string LookCameraDeviceKey = YuiPrefsKeys.LookCameraDevice;
        private const string SecretModeKey = YuiPrefsKeys.SecretMode;
        private const string CustomInstructionKey = YuiPrefsKeys.CustomInstruction;
        private const string CharacterNameKey = YuiPrefsKeys.CharacterName;
        private const string AvatarSlotKey = YuiPrefsKeys.AvatarSlot;
        private const string ClientSchemaVersion = "2026-05-10";
        private const bool EnableDormantAppAwarenessPrototype = false;
        private const bool EnableBackendDiagnosticsLog = false;
        private const int RealtimeSessionResetTurns = 0;

        [Header("Backend")]
        [SerializeField] private string backendUrl = "http://127.0.0.1:8000";
        [SerializeField] private string userId = "local_user";
        [SerializeField] private int speakerId = 14;
        [SerializeField] private float speedScale = 1.0f;
        [SerializeField] private float pitchScale = 0.0f;
        [SerializeField] private float intonationScale = 1.0f;
        [SerializeField] private float synthesisVolumeScale = 1.0f;
        [SerializeField] private float prePhonemeLength = 0.1f;
        [SerializeField] private float postPhonemeLength = 0.1f;
        [SerializeField] private string conversationMode = "stable";
        [SerializeField] private string ttsMode = "local";
        [SerializeField] private string characterName = "Yui";
        [SerializeField] private string customInstruction = "";
        [SerializeField] private string avatarSlot = YuiAvatarSlots.UnityChanDefault;

        [Header("UI")]
        [SerializeField] private InputField inputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private Text sendButtonText;
        [SerializeField] private Button lookButton;
        [SerializeField] private Text lookButtonText;
        [SerializeField] private Button importImageButton;
        [SerializeField] private Text importImageButtonText;
        [SerializeField] private Button recordButton;
        [SerializeField] private Text recordButtonText;
        [SerializeField] private Image microphoneLevelFill;
        [SerializeField] private Text microphoneDeviceText;
        [SerializeField] private Button secretModeButton;
        [SerializeField] private Text secretModeButtonText;
        [SerializeField] private Text secretModeIndicatorText;
        [SerializeField] private Text logText;
        [SerializeField] private Text statusText;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private YuiAvatarController avatarController;
        [SerializeField] private YuiAvatarSwitcher avatarSwitcher;
        [SerializeField] private YuiRuntimeVrmImporter runtimeVrmImporter;
        [SerializeField] private YuiChatdollKitController chatdollKitController;
        [SerializeField] private YuiChatdollVoicevoxTts chatdollKitVoicevoxTts;
        [SerializeField] private YuiChatLogView chatLogView;
        [SerializeField] private bool preferChatdollKitVoicevoxTts = true;
        [SerializeField] private bool forceTtsForNonEmptyReplies = true;
        [SerializeField] private int speechChunkMaxCharacters = 90;
        [SerializeField] private string preferredMicrophoneDevice = "";
        [SerializeField] private int maxRecordingSeconds = 60;
        [SerializeField] private int preferredRecordingFrequency = 44100;
        [SerializeField] private int visionImageMaxLongSide = 1280;
        [SerializeField] private int visionJpegQuality = 78;
        [SerializeField] private string preferredLookCameraDevice = "";
        [SerializeField] private int lookCameraRequestedWidth = 1280;
        [SerializeField] private int lookCameraRequestedHeight = 720;
        [SerializeField] private bool appAwarenessEnabled = false;
        [SerializeField] private float appAwarenessPollInterval = 2f;

        private CancellationTokenSource cancellationTokenSource;
        private YuiBackendClient client;
        private bool isSending;
        private bool isRecording;
        private AudioClip recordingClip;
        private string activeMicrophoneDevice;
        private int activeRecordingFrequency;
        private float recordingStartedAt;
        private readonly float[] microphoneSampleBuffer = new float[256];
        private VisionResponse latestVision;
        private string latestVisionImageDataUrl;
        private bool secretMode;
        private string currentStatus = "Ready";
        private bool localVoicevoxUnavailable;
        private ClientWebSocket realtimeSocket;
        private CancellationTokenSource realtimeCancellationTokenSource;
        private CancellationTokenSource realtimeVoicevoxSpeechCancellationTokenSource;
        private readonly SemaphoreSlim realtimeSendLock = new SemaphoreSlim(1, 1);
        private readonly Queue<byte[]> realtimeAudioPcmQueue = new Queue<byte[]>();
        private readonly List<byte> realtimeAudioPcmBuffer = new List<byte>(48000);
        private readonly object realtimeAudioLock = new object();
        private readonly StringBuilder realtimeTextBuffer = new StringBuilder();
        private readonly StringBuilder realtimeVoicevoxPendingText = new StringBuilder();
        private readonly Queue<string> realtimeVoicevoxSpeechQueue = new Queue<string>();
        private readonly object realtimeVoicevoxLock = new object();
        private bool realtimeStreamActive;
        private bool realtimeAssistantTurnActive;
        private bool realtimeRestarting;
        private bool realtimeVoicevoxSpeechActive;
        private string realtimeActiveBackendMode = "voice";
        private int realtimeLastSamplePosition;
        private float realtimeNextChunkAt;
        private int realtimeCompletedTurns;
        private int realtimeSentAudioChunks;
        private int realtimeVoicevoxGeneration;
        private System.Diagnostics.Stopwatch realtimeVoicevoxTurnTimer;
        private long realtimeVoicevoxFirstTextMs = -1;
        private long realtimeVoicevoxDoneMs = -1;
        private YuiWindowsForegroundAppMonitor appMonitor;
        private YuiForegroundAppInfo currentForegroundApp = new YuiForegroundAppInfo();
        private string currentForegroundAppKey = "";
        private string appContextStatus = "";
        private float nextAppAwarenessPollAt;

        public string BackendUrl => backendUrl;
        public int SpeakerId => speakerId;
        public float VoiceVolume => audioSource != null ? audioSource.volume : PlayerPrefs.GetFloat(VoiceVolumeKey, 1f);
        public float VoiceSpeedScale => speedScale;
        public float VoicePitchScale => pitchScale;
        public float VoiceIntonationScale => intonationScale;
        public float VoiceSynthesisVolumeScale => synthesisVolumeScale;
        public float VoicePrePhonemeLength => prePhonemeLength;
        public float VoicePostPhonemeLength => postPhonemeLength;
        public string ConversationMode => conversationMode;
        public string TtsMode => ttsMode;
        public string PreferredMicrophoneDevice => preferredMicrophoneDevice;
        public string PreferredLookCameraDevice => preferredLookCameraDevice;
        public bool SecretMode => secretMode;
        public string CharacterName => characterName;
        public string CustomInstruction => customInstruction;
        public string AvatarSlot => avatarSlot;

        private void Awake()
        {
            LoadSavedRuntimeSettings();
            client = new YuiBackendClient(backendUrl);
            cancellationTokenSource = new CancellationTokenSource();
            EnsureUiReferences();
            ApplyReadableFont();
            if (avatarSwitcher == null)
            {
                avatarSwitcher = GetComponent<YuiAvatarSwitcher>() ?? YuiSceneObjectFinder.FindFirst<YuiAvatarSwitcher>();
            }
            if (runtimeVrmImporter == null)
            {
                runtimeVrmImporter = GetComponent<YuiRuntimeVrmImporter>() ?? YuiSceneObjectFinder.FindFirst<YuiRuntimeVrmImporter>();
            }
            if (EnableDormantAppAwarenessPrototype && appAwarenessEnabled && appMonitor == null)
            {
                appMonitor = GetComponent<YuiWindowsForegroundAppMonitor>();
                if (appMonitor == null)
                {
                    appMonitor = gameObject.AddComponent<YuiWindowsForegroundAppMonitor>();
                }
            }
            ApplyAvatarSlot(false);

            if (sendButton != null)
            {
                sendButton.onClick.AddListener(SendCurrentInput);
            }

            if (inputField != null)
            {
                inputField.onSubmit.AddListener(_ => SendCurrentInput());
            }

            if (recordButton != null)
            {
                recordButton.onClick.AddListener(ToggleRecording);
            }

            if (lookButton != null)
            {
                lookButton.onClick.AddListener(CaptureScreenAndAnalyze);
            }

            if (importImageButton != null)
            {
                importImageButton.onClick.AddListener(ImportImageAndAnalyze);
            }

            if (secretModeButton != null)
            {
                secretModeButton.onClick.AddListener(ToggleSecretMode);
            }

            SelectMicrophoneDevice();
            UpdateMicrophoneLevel(0f);
            UpdateSecretModeUi();
            SetStatus("Ready");
        }

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
            if (!string.Equals(previousConversationMode, conversationMode, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(conversationMode, "stable", StringComparison.OrdinalIgnoreCase))
            {
                AppendLog("System", RealtimeModeWarningText(conversationMode));
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
            ttsMode = NormalizeTtsMode(PlayerPrefs.GetString(TtsModeKey, ttsMode));
            preferredMicrophoneDevice = PlayerPrefs.GetString(MicrophoneDeviceKey, preferredMicrophoneDevice);
            preferredLookCameraDevice = NormalizeLookCameraDevice(PlayerPrefs.GetString(LookCameraDeviceKey, preferredLookCameraDevice));
            secretMode = PlayerPrefs.GetInt(SecretModeKey, 0) == 1;
            characterName = PlayerPrefs.GetString(CharacterNameKey, characterName);
            customInstruction = PlayerPrefs.GetString(CustomInstructionKey, customInstruction);
            var savedAvatarSlot = PlayerPrefs.GetString(AvatarSlotPrefsKey, avatarSlot);
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
            var hasDemoAvatar = avatarSwitcher != null && avatarSwitcher.HasDemoAvatar;
            if (!hasDemoAvatar)
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

            return new[]
            {
                "Demo Avatar",
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

            var hasDemoAvatar = avatarSwitcher != null && avatarSwitcher.HasDemoAvatar;
            if (hasDemoAvatar)
            {
                if (index == 0)
                {
                    return YuiAvatarSlots.DemoKikyo;
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
            return new[]
            {
                "Stable",
                "Realtime Voice (Experimental)",
                "Realtime VOICEVOX (Experimental)",
                "Realtime Translate (Experimental)"
            };
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
            var devices = Microphone.devices;
            if (devices == null || devices.Length == 0)
            {
                return new[] { "Default" };
            }

            var options = new string[devices.Length + 1];
            options[0] = "Default";
            Array.Copy(devices, 0, options, 1, devices.Length);
            return options;
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

        public async void PreviewVoice()
        {
            await PreviewVoiceAsync(null);
        }

        public async void PreviewVoice(Action onFinished)
        {
            await PreviewVoiceAsync(onFinished);
        }

        public async Task PreviewVoiceAsync(Action onFinished = null)
        {
            if (audioSource == null)
            {
                SetStatus("Voice unavailable");
                onFinished?.Invoke();
                return;
            }

            if (IsTtsMode("silent"))
            {
                SetStatus("TTS is silent");
                onFinished?.Invoke();
                return;
            }

            try
            {
                SetStatus("Previewing voice...");
                var clip = await SynthesizeSpeechClipAsync(
                    "こんにちは、ユイです。声の設定はこんな感じです。",
                    "normal",
                    "voice-preview-" + Guid.NewGuid().ToString("N"),
                    cancellationTokenSource.Token);
                if (clip == null)
                {
                    SetStatus("Preview failed");
                    return;
                }

                var previousClip = audioSource.clip;
                audioSource.Stop();
                audioSource.clip = clip;
                DestroyOwnedAudioClip(previousClip, clip);
                audioSource.Play();
                SetStatus("Voice preview");
                while (audioSource != null && audioSource.isPlaying && !cancellationTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(30, cancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                SetStatus("Preview failed");
                var errorMessage = ex is YuiBackendException backendException
                    ? backendException.UserMessage
                    : ex.Message;
                AppendLog("System", errorMessage);
                Debug.LogError(ex);
            }
            finally
            {
                onFinished?.Invoke();
            }
        }

        public void TestMicrophone()
        {
            var device = SelectMicrophoneDevice();
            if (string.IsNullOrEmpty(device))
            {
                SetStatus("Mic: none");
                AppendLog("System", "マイクが見つかりません。WindowsとUnityのマイク設定を確認してください。");
                return;
            }

            Microphone.GetDeviceCaps(device, out var minFrequency, out var maxFrequency);
            var frequencyText = minFrequency == 0 && maxFrequency == 0
                ? $"{preferredRecordingFrequency}Hz"
                : $"{minFrequency}-{maxFrequency}Hz";
            SetMicrophoneDeviceText($"Mic: {device}");
            SetStatus($"Mic OK: {device}");
            Debug.Log($"Yui mic test: device='{device}', caps={frequencyText}");
        }

        private void Update()
        {
            UpdateAppAwareness();
            PlayNextRealtimeQueuedClip();
            if (!isRecording || recordingClip == null || string.IsNullOrEmpty(activeMicrophoneDevice))
            {
                return;
            }

            var elapsed = Time.realtimeSinceStartup - recordingStartedAt;
            var realtimeMode = IsRealtimeConversationMode();
            SetStatus(realtimeMode
                ? $"Realtime listening... {FormatElapsedTime(elapsed)}"
                : $"Recording... {Mathf.FloorToInt(elapsed)}/{maxRecordingSeconds}s");
            if ((!realtimeMode && elapsed >= maxRecordingSeconds - 0.05f)
                || !Microphone.IsRecording(activeMicrophoneDevice))
            {
                Debug.LogWarning($"Recording reached max length or stopped by device. elapsed={elapsed:F1}s, maxSeconds={maxRecordingSeconds}");
                if (elapsed >= maxRecordingSeconds - 0.05f)
                {
                    AppendLog("System", "入力制限の1分を超過しました。ここまでの音声で送信します。");
                }
                _ = StopRecordingAndSendAsync();
                return;
            }

            var position = Microphone.GetPosition(activeMicrophoneDevice);
            if (position <= microphoneSampleBuffer.Length)
            {
                UpdateMicrophoneLevel(0f);
                return;
            }

            recordingClip.GetData(microphoneSampleBuffer, position - microphoneSampleBuffer.Length);
            var sum = 0f;
            for (var index = 0; index < microphoneSampleBuffer.Length; index++)
            {
                sum += microphoneSampleBuffer[index] * microphoneSampleBuffer[index];
            }

            var rms = Mathf.Sqrt(sum / microphoneSampleBuffer.Length);
            var level = rms > 0.005f ? Mathf.Max(0.06f, rms * 32f) : 0f;
            UpdateMicrophoneLevel(Mathf.Clamp01(level));

            var shouldHoldRealtimeMic = IsRealtimeVoicevoxMode()
                ? audioSource != null && audioSource.isPlaying
                : realtimeAssistantTurnActive || (audioSource != null && audioSource.isPlaying);
            if (realtimeMode && shouldHoldRealtimeMic)
            {
                realtimeLastSamplePosition = position;
                return;
            }

            if (realtimeMode && realtimeStreamActive && !realtimeRestarting && Time.realtimeSinceStartup >= realtimeNextChunkAt)
            {
                realtimeNextChunkAt = Time.realtimeSinceStartup + 0.12f;
                SendRealtimeMicrophoneDelta(position);
            }
        }

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

            if (sendButton == null)
            {
                var sendTransform = UiTreeUtility.FindDeepChild(transform, "SendButton");
                sendButton = sendTransform != null
                    ? sendTransform.GetComponent<Button>()
                    : GetComponentInChildren<Button>(true);
            }

            if (recordButton == null)
            {
                var recordTransform = UiTreeUtility.FindDeepChild(transform, "RecordButton");
                recordButton = recordTransform != null ? recordTransform.GetComponent<Button>() : null;
            }

            if (lookButton == null)
            {
                var lookTransform = UiTreeUtility.FindDeepChild(transform, "LookButton");
                lookButton = lookTransform != null ? lookTransform.GetComponent<Button>() : null;
            }

            if (importImageButton == null)
            {
                var importTransform = UiTreeUtility.FindDeepChild(transform, "ImportImageButton");
                importImageButton = importTransform != null ? importTransform.GetComponent<Button>() : null;
            }

            if (sendButtonText == null)
            {
                var labelTransform = UiTreeUtility.FindDeepChild(transform, "Label");
                sendButtonText = labelTransform != null
                    ? labelTransform.GetComponent<Text>()
                    : null;
            }

            if (recordButtonText == null)
            {
                var labelTransform = UiTreeUtility.FindDeepChild(transform, "RecordLabel");
                recordButtonText = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            }

            if (lookButtonText == null)
            {
                var labelTransform = UiTreeUtility.FindDeepChild(transform, "LookLabel");
                lookButtonText = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            }

            if (importImageButtonText == null)
            {
                var labelTransform = UiTreeUtility.FindDeepChild(transform, "ImportImageLabel");
                importImageButtonText = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            }

            if (secretModeButton == null)
            {
                var secretTransform = UiTreeUtility.FindDeepChild(transform, "SecretModeButton");
                secretModeButton = secretTransform != null ? secretTransform.GetComponent<Button>() : null;
            }

            if (secretModeButtonText == null)
            {
                var secretLabelTransform = UiTreeUtility.FindDeepChild(transform, "SecretModeLabel");
                secretModeButtonText = secretLabelTransform != null ? secretLabelTransform.GetComponent<Text>() : null;
            }

            if (secretModeIndicatorText == null)
            {
                var indicatorTransform = UiTreeUtility.FindDeepChild(transform, "SecretModeIndicator");
                secretModeIndicatorText = indicatorTransform != null ? indicatorTransform.GetComponent<Text>() : null;
            }

            if (microphoneLevelFill == null)
            {
                var levelTransform = UiTreeUtility.FindDeepChild(transform, "MicrophoneLevelFill");
                microphoneLevelFill = levelTransform != null ? levelTransform.GetComponent<Image>() : null;
            }

            if (microphoneDeviceText == null)
            {
                var deviceTransform = UiTreeUtility.FindDeepChild(transform, "MicrophoneDeviceText");
                microphoneDeviceText = deviceTransform != null ? deviceTransform.GetComponent<Text>() : null;
            }

            if (logText == null)
            {
                var logTransform = UiTreeUtility.FindDeepChild(transform, "ChatLogText");
                logText = logTransform != null ? logTransform.GetComponent<Text>() : null;
            }

            if (statusText == null)
            {
                var statusTransform = UiTreeUtility.FindDeepChild(transform, "StatusText");
                statusText = statusTransform != null ? statusTransform.GetComponent<Text>() : null;
            }

            NormalizeLogView();
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

        private async void Start()
        {
            _ = MonitorBackendAsync(cancellationTokenSource.Token);
            await CheckBackendOnceAsync(cancellationTokenSource.Token);
        }

        private async Task MonitorBackendAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                await CheckBackendOnceAsync(cancellationToken);
            }
        }

        private async Task CheckBackendOnceAsync(CancellationToken cancellationToken)
        {
            try
            {
                var health = await client.GetHealthAsync(cancellationToken);
                if (!isSending)
                {
                    SetStatus(FormatBackendStatus(health));
                }

                if (EnableBackendDiagnosticsLog)
                {
                    LogBackendDiagnostics(health);
                }

                if (chatLogView == null || chatLogView.IsEmpty)
                {
                    await LoadRecentConversationsAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                if (!isSending)
                {
                    SetStatus("Backend offline");
                }

                Debug.LogWarning($"Backend health check failed: {ex.Message}");
            }
        }

        private async Task LoadRecentConversationsAsync(CancellationToken cancellationToken)
        {
            if (secretMode)
            {
                return;
            }

            var recent = await client.GetRecentConversationsAsync(userId, 12, cancellationToken);
            if (recent?.Items == null || recent.Items.Count == 0)
            {
                return;
            }

            foreach (var item in recent.Items)
            {
                var speaker = item.Role == "assistant" ? "Yui" : "You";
                AppendLog(speaker, item.Message);
            }
        }

        private void OnDestroy()
        {
            if (sendButton != null)
            {
                sendButton.onClick.RemoveListener(SendCurrentInput);
            }

            if (recordButton != null)
            {
                recordButton.onClick.RemoveListener(ToggleRecording);
            }

            if (lookButton != null)
            {
                lookButton.onClick.RemoveListener(CaptureScreenAndAnalyze);
            }

            if (importImageButton != null)
            {
                importImageButton.onClick.RemoveListener(ImportImageAndAnalyze);
            }

            if (secretModeButton != null)
            {
                secretModeButton.onClick.RemoveListener(ToggleSecretMode);
            }

            if (isRecording)
            {
                Microphone.End(activeMicrophoneDevice);
            }

            cancellationTokenSource?.Cancel();
            realtimeCancellationTokenSource?.Cancel();
            realtimeVoicevoxSpeechCancellationTokenSource?.Cancel();
            realtimeSocket?.Dispose();
            cancellationTokenSource?.Dispose();
            realtimeCancellationTokenSource?.Dispose();
            realtimeVoicevoxSpeechCancellationTokenSource?.Dispose();
        }

        private void SendCurrentInput()
        {
            if (inputField == null || isSending)
            {
                return;
            }

            var message = inputField.text.Trim();
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            inputField.text = string.Empty;
            _ = SendMessageAsync(message);
        }

        private void ToggleRecording()
        {
            if (isSending)
            {
                return;
            }

            if (isRecording)
            {
                _ = StopRecordingAndSendAsync();
                return;
            }

            StartRecording();
        }

        private void CaptureScreenAndAnalyze()
        {
            if (isSending)
            {
                return;
            }

            _ = CaptureScreenAndAnalyzeAsync();
        }

        private void ImportImageAndAnalyze()
        {
            if (isSending)
            {
                return;
            }

            _ = ImportImageAndAnalyzeFromPickerAsync();
        }

        private async Task ImportImageAndAnalyzeFromPickerAsync()
        {
            AppendLog("System", "画像ファイル選択を開きます...");
            var result = await YuiFilePicker.OpenImageFileAsync();
            if (!result.Opened)
            {
                if (!string.IsNullOrWhiteSpace(result.UserMessage))
                {
                    AppendLog("System", result.UserMessage);
                }

                AppendLog("System", "画像ファイル選択はキャンセルされました。");
                return;
            }

            if (string.IsNullOrWhiteSpace(result.Path))
            {
                return;
            }

            await ImportImageAndAnalyzeAsync(result.Path);
        }

        private async Task CaptureScreenAndAnalyzeAsync()
        {
            if (!await TryCaptureCameraAndAnalyzeAsync())
            {
                SetStatus("Look camera not selected");
                AppendLog("System", "Look用カメラが未設定です。Settings > Camera > Device で使用するカメラを選んでください。");
            }

            SetInteractable(true);
        }

        private async Task<bool> TryCaptureCameraAndAnalyzeAsync()
        {
            var devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                return false;
            }

            var selectedDevice = SelectLookCameraDevice(devices);
            if (string.IsNullOrWhiteSpace(selectedDevice))
            {
                return false;
            }

            Texture2D frame = null;
            try
            {
                isSending = true;
                SetInteractable(false);
                SetStatus("Looking through camera...");
                AppendLog("System", $"カメラを見ています... {selectedDevice}");

                frame = await CaptureCameraFrameAsync(selectedDevice, true);
                if (frame == null)
                {
                    AppendLog("System", "指定解像度では有効なカメラ画像を取得できませんでした。デフォルト解像度で再試行します。");
                    frame = await CaptureCameraFrameAsync(selectedDevice, false);
                }

                if (frame == null)
                {
                    AppendLog("System", "カメラ画像を取得できませんでした。Camo/OBSなどの仮想カメラは、出力元アプリで映像が動いていることを確認してください。");
                    SetStatus("Camera unavailable");
                    return true;
                }

                AppendLog("System", $"カメラ画像を取得しました: {frame.width}x{frame.height}");
                var imageBytes = YuiVisionImageUtility.EncodeTextureForVision(
                    frame,
                    visionImageMaxLongSide,
                    visionJpegQuality);
                latestVisionImageDataUrl = YuiVisionImageUtility.ToImageDataUrl(
                    imageBytes,
                    "image/jpeg");
                latestVision = await client.AnalyzeImageAsync(
                    imageBytes,
                    "camera.jpg",
                    "camera",
                    "image/jpeg",
                    cancellationTokenSource.Token);

                AppendLog("Vision", latestVision.Summary);
                SetStatus("Ready");
                return true;
            }
            catch (YuiBackendException ex)
            {
                SetStatus("Error");
                AppendLog("System", $"カメラ画像を解析できませんでした。{ex.UserMessage}");
                Debug.LogError(ex);
                return true;
            }
            catch (Exception ex)
            {
                SetStatus("Error");
                AppendLog("System", "カメラ画像を解析できませんでした。カメラ権限、OpenAI Vision設定、Backendログを確認してください。");
                Debug.LogError(ex);
                return true;
            }
            finally
            {
                if (frame != null)
                {
                    Destroy(frame);
                }

                isSending = false;
                SetInteractable(true);
            }
        }

        private async Task<Texture2D> CaptureCameraFrameAsync(string selectedDevice, bool useRequestedSize)
        {
            WebCamTexture webcam = null;
            try
            {
                webcam = useRequestedSize
                    ? new WebCamTexture(
                        selectedDevice,
                        Mathf.Max(320, lookCameraRequestedWidth),
                        Mathf.Max(240, lookCameraRequestedHeight),
                        15)
                    : new WebCamTexture(selectedDevice);
                webcam.Play();

                var startedAt = Time.realtimeSinceStartup;
                Color32[] pixels = null;
                while (Time.realtimeSinceStartup - startedAt < 6f)
                {
                    await Task.Delay(120, cancellationTokenSource.Token);
                    if (webcam.width <= 16 || webcam.height <= 16 || !webcam.didUpdateThisFrame)
                    {
                        continue;
                    }

                    pixels = webcam.GetPixels32();
                    if (pixels == null || pixels.Length == 0 || IsProbablyBlackFrame(pixels))
                    {
                        continue;
                    }

                    var frame = new Texture2D(webcam.width, webcam.height, TextureFormat.RGB24, false);
                    frame.SetPixels32(pixels);
                    frame.Apply(false, false);
                    Debug.Log($"Yui Look camera frame captured: device={selectedDevice}, size={webcam.width}x{webcam.height}, requested={useRequestedSize}");
                    return frame;
                }

                if (webcam.width > 16 && webcam.height > 16 && pixels != null && pixels.Length > 0)
                {
                    Debug.LogWarning($"Yui Look camera returned only black frames: device={selectedDevice}, size={webcam.width}x{webcam.height}, requested={useRequestedSize}");
                }

                return null;
            }
            finally
            {
                if (webcam != null)
                {
                    webcam.Stop();
                    Destroy(webcam);
                }
            }
        }

        private static bool IsProbablyBlackFrame(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
            {
                return true;
            }

            var step = Mathf.Max(1, pixels.Length / 2048);
            var samples = 0;
            var brightSamples = 0;
            long total = 0;
            var maxChannel = 0;
            for (var i = 0; i < pixels.Length; i += step)
            {
                var pixel = pixels[i];
                var brightness = pixel.r + pixel.g + pixel.b;
                total += brightness;
                maxChannel = Math.Max(maxChannel, Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)));
                if (brightness > 48)
                {
                    brightSamples++;
                }
                samples++;
            }

            if (samples == 0)
            {
                return true;
            }

            var average = total / (samples * 3f);
            return average < 4f && maxChannel < 20 && brightSamples < 4;
        }

        private string SelectLookCameraDevice(WebCamDevice[] devices)
        {
            preferredLookCameraDevice = NormalizeLookCameraDevice(preferredLookCameraDevice);
            if (string.IsNullOrWhiteSpace(preferredLookCameraDevice))
            {
                return null;
            }

            foreach (var device in devices)
            {
                if (device.name == preferredLookCameraDevice)
                {
                    return device.name;
                }
            }

            AppendLog("System", $"選択中のLook用カメラが見つかりません: {preferredLookCameraDevice}");
            return null;
        }

        private async Task ImportImageAndAnalyzeAsync(string path)
        {
            try
            {
                isSending = true;
                SetInteractable(false);
                SetStatus("Analyzing image...");
                AppendLog("System", $"画像を見ています... {Path.GetFileName(path)}");

                var mimeType = YuiVisionImageUtility.ResolveImageMimeType(path);
                if (string.IsNullOrEmpty(mimeType))
                {
                    AppendLog("System", "対応している画像形式は PNG / JPG / WebP / HEIC / HEIF です。");
                    SetStatus("Ready");
                    return;
                }

                var originalBytes = File.ReadAllBytes(path);
                var imageBytes = YuiVisionImageUtility.TryEncodeImageForVision(
                    originalBytes,
                    visionImageMaxLongSide,
                    visionJpegQuality,
                    out var optimizedBytes)
                    ? optimizedBytes
                    : originalBytes;
                if (optimizedBytes != null)
                {
                    mimeType = "image/jpeg";
                }
                latestVisionImageDataUrl = YuiVisionImageUtility.ToImageDataUrl(imageBytes, mimeType);
                latestVision = await client.AnalyzeImageAsync(
                    imageBytes,
                    Path.GetFileName(path),
                    "general",
                    mimeType,
                    cancellationTokenSource.Token);

                AppendLog("Vision", latestVision.Summary);
                SetStatus("Ready");
            }
            catch (YuiBackendException ex)
            {
                SetStatus("Error");
                AppendLog("System", $"画像を解析できませんでした。{ex.UserMessage}");
                Debug.LogError(ex);
            }
            catch (Exception ex)
            {
                SetStatus("Error");
                AppendLog("System", "画像を解析できませんでした。ファイル形式、OpenAI Vision設定、Backendログを確認してください。");
                Debug.LogError(ex);
            }
            finally
            {
                isSending = false;
                SetInteractable(true);
            }
        }

        private void StartRecording()
        {
            var device = SelectMicrophoneDevice();
            if (string.IsNullOrEmpty(device))
            {
                AppendLog("System", "マイクが見つかりません。WindowsとUnityのマイク設定を確認してください。");
                return;
            }

            if (!TryStartMicrophone(device))
            {
                foreach (var fallbackDevice in Microphone.devices)
                {
                    if (fallbackDevice == device)
                    {
                        continue;
                    }

                    if (TryStartMicrophone(fallbackDevice))
                    {
                        break;
                    }
                }
            }

            if (recordingClip == null)
            {
                AppendLog("System", "マイクを開始できませんでした。Consoleの `Unity microphones:` に出た名前を Preferred Microphone Device に指定してみてください。");
                return;
            }

            SetMicrophoneDeviceText($"Mic: {activeMicrophoneDevice}");
            isRecording = true;
            recordingStartedAt = Time.realtimeSinceStartup;
            SetInteractable(false);
            SetStatus(IsRealtimeConversationMode() ? "Realtime listening... 00:00" : $"Recording... 0/{maxRecordingSeconds}s");
            SetRecordButtonText("Stop");
            if (IsRealtimeConversationMode())
            {
                _ = StartRealtimeStreamAsync();
            }
        }

        private bool TryStartMicrophone(string device)
        {
            activeMicrophoneDevice = device;
            activeRecordingFrequency = ResolveRecordingFrequency(device);
            var realtimeMode = IsRealtimeConversationMode();
            var clipLengthSeconds = realtimeMode ? 10 : maxRecordingSeconds;
            Debug.Log($"Starting microphone device='{activeMicrophoneDevice}', frequency={activeRecordingFrequency}, maxSeconds={clipLengthSeconds}, realtime={realtimeMode}");

            try
            {
                recordingClip = Microphone.Start(
                    activeMicrophoneDevice,
                    realtimeMode,
                    clipLengthSeconds,
                    activeRecordingFrequency);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Starting microphone failed for '{device}': {ex.Message}");
                recordingClip = null;
            }

            if (recordingClip != null)
            {
                return true;
            }

            Debug.LogWarning($"Starting microphone returned null for '{device}'.");
            return false;
        }

        private async Task StopRecordingAndSendAsync()
        {
            var samplePosition = Microphone.GetPosition(activeMicrophoneDevice);
            var wasStillRecording = Microphone.IsRecording(activeMicrophoneDevice);
            Microphone.End(activeMicrophoneDevice);
            isRecording = false;
            SetRecordButtonText("Rec");
            UpdateMicrophoneLevel(0f);
            if (samplePosition <= 0 && !wasStillRecording && recordingClip != null)
            {
                samplePosition = recordingClip.samples;
            }

            if (IsRealtimeConversationMode())
            {
                try
                {
                    isSending = true;
                    SetInteractable(false);
                    SetStatus("Realtime responding...");
                    await StopRealtimeStreamAsync();
                }
                catch (Exception ex)
                {
                    SetStatus("Realtime error");
                    AppendLog("System", ex.Message);
                    Debug.LogError(ex);
                }
                finally
                {
                    isSending = false;
                    SetInteractable(true);
                }
                return;
            }

            if (recordingClip == null || samplePosition <= 0)
            {
                SetStatus("Ready");
                SetInteractable(true);
                AppendLog("System", $"音声を録音できませんでした。device={activeMicrophoneDevice}");
                return;
            }

            try
            {
                isSending = true;
                SetInteractable(false);
                SetStatus("Transcribing...");
                var wavBytes = WavUtility.FromAudioClip(recordingClip, samplePosition);
                var durationMs = Mathf.RoundToInt(samplePosition * 1000f / activeRecordingFrequency);
                var transcript = await client.TranscribeAudioAsync(
                    wavBytes,
                    "ptt_recording.wav",
                    durationMs,
                    cancellationTokenSource.Token);

                var message = transcript.Text?.Trim();
                if (string.IsNullOrEmpty(message))
                {
                    AppendLog("System", "音声を文字起こしできませんでした。");
                    return;
                }

                await SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                SetStatus("Error");
                var errorMessage = ex is YuiBackendException backendException
                    ? backendException.UserMessage
                    : ex.Message;
                AppendLog("System", errorMessage);
                Debug.LogError(ex);
            }
            finally
            {
                isSending = false;
                SetInteractable(true);
            }
        }

        private async Task SendRealtimeRecordingAsync(byte[] wavBytes)
        {
            SetStatus("Realtime...");
            SetPendingLine(CharacterName, "Realtime接続中...");
            AppendLog("You", "(voice)");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var mode = RealtimeBackendMode();
            var response = await client.SendRealtimeAudioAsync(
                wavBytes,
                mode,
                RealtimeInstructionsForMode(mode),
                "realtime_recording.wav",
                cancellationTokenSource.Token);
            Debug.Log($"Yui realtime audio latency: {timer.ElapsedMilliseconds} ms, events={string.Join(",", response.Events ?? new System.Collections.Generic.List<string>())}");

            ClearPendingLine();
            if (!string.IsNullOrWhiteSpace(response.Text))
            {
                AppendLog(CharacterName, response.Text.Trim());
            }

            var clip = YuiBackendClient.Pcm16Base64ToAudioClip(
                response.AudioBase64,
                response.SampleRate,
                "YuiRealtimeAudio");
            if (clip != null && audioSource != null)
            {
                SetStatus("Speaking...");
                var previousClip = audioSource.clip;
                audioSource.Stop();
                audioSource.clip = clip;
                DestroyOwnedAudioClip(previousClip, clip);
                audioSource.Play();
            }

            SetStatus("Ready");
        }

        private async Task StartRealtimeStreamAsync()
        {
            await CloseRealtimeStreamAsync();
            realtimeCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
            realtimeSocket = new ClientWebSocket();
            realtimeTextBuffer.Clear();
            realtimeVoicevoxPendingText.Clear();
            realtimeVoicevoxSpeechCancellationTokenSource?.Cancel();
            realtimeVoicevoxSpeechCancellationTokenSource?.Dispose();
            realtimeVoicevoxSpeechCancellationTokenSource = null;
            lock (realtimeVoicevoxLock)
            {
                realtimeVoicevoxSpeechQueue.Clear();
            }
            realtimeAssistantTurnActive = false;
            realtimeVoicevoxSpeechActive = false;
            realtimeRestarting = false;
            realtimeCompletedTurns = 0;
            realtimeSentAudioChunks = 0;
            lock (realtimeAudioLock)
            {
                realtimeAudioPcmBuffer.Clear();
                realtimeAudioPcmQueue.Clear();
            }
            realtimeLastSamplePosition = 0;
            realtimeNextChunkAt = Time.realtimeSinceStartup + 0.05f;

            var uri = new Uri(ToWebSocketUrl("/realtime/stream"));
            try
            {
                SetStatus("Realtime connecting...");
                await realtimeSocket.ConnectAsync(uri, realtimeCancellationTokenSource.Token);
                var mode = RealtimeBackendMode();
                realtimeActiveBackendMode = mode;
                await SendRealtimeJsonAsync(new
                {
                    type = "start",
                    mode,
                    user_id = userId,
                    character_name = characterName,
                    instructions = RealtimeInstructionsForMode(mode)
                });
                realtimeStreamActive = true;
                _ = ReceiveRealtimeLoopAsync(realtimeCancellationTokenSource.Token);
                SetStatus("Realtime listening...");
            }
            catch (Exception ex)
            {
                realtimeStreamActive = false;
                realtimeAssistantTurnActive = false;
                SetStatus("Realtime failed");
                AppendLog("System", $"Realtime接続に失敗しました: {ex.Message}");
                Debug.LogError(ex);
            }
        }

        private async Task StopRealtimeStreamAsync()
        {
            realtimeStreamActive = false;
            realtimeAssistantTurnActive = false;
            if (realtimeSocket == null || realtimeSocket.State != WebSocketState.Open)
            {
                SetStatus("Ready");
                return;
            }

            if (realtimeSentAudioChunks > 0)
            {
                await SendRealtimeJsonAsync(new { type = "stop" });
            }
            else
            {
                await CloseRealtimeStreamAsync();
            }
        }

        private async Task CloseRealtimeStreamAsync()
        {
            realtimeStreamActive = false;
            realtimeAssistantTurnActive = false;
            try
            {
                if (realtimeSocket != null && realtimeSocket.State == WebSocketState.Open)
                {
                    await SendRealtimeJsonAsync(new { type = "close" });
                    await realtimeSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "closed",
                        CancellationToken.None);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
            finally
            {
                realtimeCancellationTokenSource?.Cancel();
                realtimeCancellationTokenSource?.Dispose();
                realtimeCancellationTokenSource = null;
                realtimeVoicevoxSpeechCancellationTokenSource?.Cancel();
                realtimeVoicevoxSpeechCancellationTokenSource?.Dispose();
                realtimeVoicevoxSpeechCancellationTokenSource = null;
                realtimeSocket?.Dispose();
                realtimeSocket = null;
            }
        }

        private void SendRealtimeMicrophoneDelta(int currentPosition)
        {
            if (recordingClip == null || currentPosition == realtimeLastSamplePosition)
            {
                return;
            }

            var sampleCount = currentPosition > realtimeLastSamplePosition
                ? currentPosition - realtimeLastSamplePosition
                : recordingClip.samples - realtimeLastSamplePosition + currentPosition;
            if (sampleCount < Mathf.Max(64, activeRecordingFrequency / 20))
            {
                return;
            }

            var data = new float[sampleCount * recordingClip.channels];
            if (currentPosition > realtimeLastSamplePosition)
            {
                recordingClip.GetData(data, realtimeLastSamplePosition);
            }
            else
            {
                var tailSamples = recordingClip.samples - realtimeLastSamplePosition;
                var tailData = new float[tailSamples * recordingClip.channels];
                var headData = new float[currentPosition * recordingClip.channels];
                recordingClip.GetData(tailData, realtimeLastSamplePosition);
                if (currentPosition > 0)
                {
                    recordingClip.GetData(headData, 0);
                }
                Array.Copy(tailData, 0, data, 0, tailData.Length);
                Array.Copy(headData, 0, data, tailData.Length, headData.Length);
            }
            realtimeLastSamplePosition = currentPosition;
            var pcm16 = ConvertToPcm16Mono24k(data, recordingClip.channels, activeRecordingFrequency);
            if (pcm16.Length == 0)
            {
                return;
            }

            _ = SendRealtimeJsonAsync(new
            {
                type = "audio",
                audio = Convert.ToBase64String(pcm16)
            });
            realtimeSentAudioChunks++;
        }

        private async Task SendRealtimeJsonAsync(object payload)
        {
            if (realtimeSocket == null || realtimeSocket.State != WebSocketState.Open)
            {
                return;
            }

            var json = JsonConvert.SerializeObject(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            await realtimeSendLock.WaitAsync();
            try
            {
                await realtimeSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    realtimeCancellationTokenSource != null
                        ? realtimeCancellationTokenSource.Token
                        : CancellationToken.None);
            }
            finally
            {
                realtimeSendLock.Release();
            }
        }

        private async Task ReceiveRealtimeLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[64 * 1024];
            var stream = new MemoryStream();
            try
            {
                while (!cancellationToken.IsCancellationRequested
                    && realtimeSocket != null
                    && realtimeSocket.State == WebSocketState.Open)
                {
                    stream.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await realtimeSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }
                        stream.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    var json = Encoding.UTF8.GetString(stream.ToArray());
                    HandleRealtimeMessage(JObject.Parse(json));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                SetStatus("Realtime error");
                AppendLog("System", $"Realtime受信に失敗しました: {ex.Message}");
                Debug.LogError(ex);
            }
        }

        private void HandleRealtimeMessage(JObject message)
        {
            var type = message.Value<string>("type");
            if (type == "ready")
            {
                realtimeActiveBackendMode = message.Value<string>("mode") ?? realtimeActiveBackendMode;
                var vad = message["turn_detection"] != null ? message["turn_detection"].ToString(Formatting.None) : "default";
                Debug.Log($"Yui realtime stream ready: mode={realtimeActiveBackendMode}, voice={message.Value<string>("voice")}, localMode={conversationMode}, vad={vad}");
                return;
            }

            if (type == "event")
            {
                var eventName = message.Value<string>("event");
                if (eventName == "response.created")
                {
                    realtimeAssistantTurnActive = true;
                    if (IsRealtimeVoicevoxMode() || string.Equals(realtimeActiveBackendMode, "voice_text", StringComparison.OrdinalIgnoreCase))
                    {
                        realtimeVoicevoxTurnTimer = System.Diagnostics.Stopwatch.StartNew();
                        realtimeVoicevoxFirstTextMs = -1;
                        realtimeVoicevoxDoneMs = -1;
                        realtimeVoicevoxPendingText.Clear();
                    }
                }
                else if (eventName == "input_audio_buffer.speech_started"
                    && (IsRealtimeVoicevoxMode() || string.Equals(realtimeActiveBackendMode, "voice_text", StringComparison.OrdinalIgnoreCase)))
                {
                    ClearRealtimeVoicevoxSpeechQueue();
                }
                Debug.Log($"Yui realtime event: {eventName}");
                return;
            }

            if (type == "text_delta")
            {
                var delta = message.Value<string>("delta") ?? string.Empty;
                realtimeTextBuffer.Append(delta);
                if (IsRealtimeVoicevoxMode() || string.Equals(realtimeActiveBackendMode, "voice_text", StringComparison.OrdinalIgnoreCase))
                {
                    if (realtimeVoicevoxTurnTimer != null && realtimeVoicevoxFirstTextMs < 0)
                    {
                        realtimeVoicevoxFirstTextMs = realtimeVoicevoxTurnTimer.ElapsedMilliseconds;
                    }
                }
                return;
            }

            if (type == "audio_delta")
            {
                if (IsRealtimeVoicevoxMode() || string.Equals(realtimeActiveBackendMode, "voice_text", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var audio = message.Value<string>("audio");
                if (!string.IsNullOrWhiteSpace(audio))
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(audio);
                        lock (realtimeAudioLock)
                        {
                            realtimeAudioPcmBuffer.AddRange(bytes);
                        }
                    }
                    catch (FormatException ex)
                    {
                        Debug.LogWarning($"Invalid realtime audio chunk: {ex.Message}");
                    }
                }
                return;
            }

            if (type == "done")
            {
                var text = realtimeTextBuffer.ToString().Trim();
                if (realtimeVoicevoxTurnTimer != null)
                {
                    realtimeVoicevoxDoneMs = realtimeVoicevoxTurnTimer.ElapsedMilliseconds;
                }
                if (!string.IsNullOrWhiteSpace(text))
                {
                    AppendLog(CharacterName, text);
                }
                realtimeTextBuffer.Clear();
                realtimeAssistantTurnActive = false;
                lock (realtimeAudioLock)
                {
                    if (IsRealtimeVoicevoxMode() || string.Equals(realtimeActiveBackendMode, "voice_text", StringComparison.OrdinalIgnoreCase))
                    {
                        realtimeAudioPcmBuffer.Clear();
                        realtimeAudioPcmQueue.Clear();
                    }
                    else if (realtimeAudioPcmBuffer.Count > 0)
                    {
                        realtimeAudioPcmQueue.Enqueue(realtimeAudioPcmBuffer.ToArray());
                        realtimeAudioPcmBuffer.Clear();
                    }
                }
                if (IsRealtimeVoicevoxMode() && !string.IsNullOrWhiteSpace(text))
                {
                    EnqueueRealtimeVoicevoxSpeech(text);
                }
                realtimeCompletedTurns++;
                if (isRecording
                    && realtimeStreamActive
                    && RealtimeSessionResetTurns > 0
                    && realtimeCompletedTurns >= RealtimeSessionResetTurns
                    && !realtimeRestarting)
                {
                    _ = RestartRealtimeStreamAfterPlaybackAsync();
                }
                if (isRecording && realtimeStreamActive)
                {
                    SetStatus("Realtime listening...");
                }
                else
                {
                    SetStatus("Ready");
                    _ = CloseRealtimeStreamAsync();
                }
                return;
            }

            if (type == "error")
            {
                var messageText = message.Value<string>("message") ?? "Realtime error";
                if (messageText.Contains("input_audio_buffer_commit_empty", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"Yui realtime ignored empty audio commit: {messageText}");
                    return;
                }
                realtimeAssistantTurnActive = false;
                AppendLog("System", messageText);
                SetStatus("Realtime error");
                _ = CloseRealtimeStreamAsync();
            }
        }

        private void EnqueueRealtimeVoicevoxSpeech(string text)
        {
            var speechText = YuiSpeechTextUtility.CleanSpeechText(text);
            if (string.IsNullOrWhiteSpace(speechText))
            {
                return;
            }

            realtimeVoicevoxPendingText.Clear();
            lock (realtimeVoicevoxLock)
            {
                realtimeVoicevoxSpeechQueue.Enqueue(speechText);
            }

            if (!realtimeVoicevoxSpeechActive)
            {
                _ = ProcessRealtimeVoicevoxQueueAsync();
            }
        }

        private void ClearRealtimeVoicevoxSpeechQueue()
        {
            realtimeVoicevoxPendingText.Clear();
            realtimeVoicevoxGeneration++;
            realtimeVoicevoxSpeechCancellationTokenSource?.Cancel();
            lock (realtimeVoicevoxLock)
            {
                realtimeVoicevoxSpeechQueue.Clear();
            }

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        private async Task ProcessRealtimeVoicevoxQueueAsync()
        {
            if (realtimeVoicevoxSpeechActive)
            {
                return;
            }

            realtimeVoicevoxSpeechActive = true;
            var generation = realtimeVoicevoxGeneration;
            try
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    if (generation != realtimeVoicevoxGeneration)
                    {
                        break;
                    }

                    string chunk;
                    lock (realtimeVoicevoxLock)
                    {
                        if (realtimeVoicevoxSpeechQueue.Count == 0)
                        {
                            break;
                        }
                        chunk = realtimeVoicevoxSpeechQueue.Dequeue();
                    }

                    await SpeakRealtimeVoicevoxChunkAsync(chunk, generation);
                }
            }
            finally
            {
                realtimeVoicevoxSpeechActive = false;
                var hasPendingSpeech = false;
                lock (realtimeVoicevoxLock)
                {
                    hasPendingSpeech = realtimeVoicevoxSpeechQueue.Count > 0;
                }

                if (hasPendingSpeech && !cancellationTokenSource.IsCancellationRequested)
                {
                    _ = ProcessRealtimeVoicevoxQueueAsync();
                }
            }
        }

        private async Task SpeakRealtimeVoicevoxChunkAsync(string text, int generation)
        {
            if (audioSource == null || string.IsNullOrWhiteSpace(text) || IsTtsMode("silent"))
            {
                return;
            }

            try
            {
                var speechText = YuiSpeechTextUtility.CleanSpeechText(text);
                if (string.IsNullOrWhiteSpace(speechText))
                {
                    return;
                }

                var chunkTimer = System.Diagnostics.Stopwatch.StartNew();
                realtimeVoicevoxSpeechCancellationTokenSource?.Dispose();
                realtimeVoicevoxSpeechCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    realtimeCancellationTokenSource != null
                        ? realtimeCancellationTokenSource.Token
                        : cancellationTokenSource.Token);
                var clip = await SynthesizeSpeechClipAsync(
                    speechText,
                    "normal",
                    "realtime-voicevox-" + Guid.NewGuid().ToString("N"),
                    realtimeVoicevoxSpeechCancellationTokenSource.Token);
                var synthMs = chunkTimer.ElapsedMilliseconds;
                if (clip == null)
                {
                    return;
                }
                if (generation != realtimeVoicevoxGeneration)
                {
                    DestroyOwnedAudioClip(clip, null);
                    return;
                }

                var previousClip = audioSource.clip;
                audioSource.Stop();
                audioSource.clip = clip;
                DestroyOwnedAudioClip(previousClip, clip);
                SetStatus("Speaking...");
                audioSource.Play();
                Debug.Log(
                    $"Yui realtime VOICEVOX playback start: text_first_ms={realtimeVoicevoxFirstTextMs}, response_done_ms={realtimeVoicevoxDoneMs}, synth_ms={synthMs}, chars={speechText.Length}");
                while (audioSource != null
                    && audioSource.isPlaying
                    && !cancellationTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(30, cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Realtime VOICEVOX synthesis cancelled before playback.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Realtime VOICEVOX playback failed: {ex.Message}");
            }
            finally
            {
                realtimeVoicevoxSpeechCancellationTokenSource?.Dispose();
                realtimeVoicevoxSpeechCancellationTokenSource = null;
            }
        }

        private async Task RestartRealtimeStreamAfterPlaybackAsync()
        {
            realtimeRestarting = true;
            try
            {
                while (isRecording
                    && realtimeStreamActive
                    && (HasRealtimeQueuedAudio() || realtimeVoicevoxSpeechActive || (audioSource != null && audioSource.isPlaying))
                    && !cancellationTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationTokenSource.Token);
                }

                if (!isRecording || cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                Debug.Log($"Yui realtime session refresh: turns={realtimeCompletedTurns}");
                SetStatus("Realtime refreshing...");
                await CloseRealtimeStreamAsync();
                await Task.Delay(150, cancellationTokenSource.Token);
                if (isRecording && !cancellationTokenSource.IsCancellationRequested)
                {
                    await StartRealtimeStreamAsync();
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                realtimeRestarting = false;
            }
        }

        private bool HasRealtimeQueuedAudio()
        {
            lock (realtimeAudioLock)
            {
                return realtimeAudioPcmQueue.Count > 0 || realtimeAudioPcmBuffer.Count > 0;
            }
        }

        private void PlayNextRealtimeQueuedClip()
        {
            if (audioSource == null || audioSource.isPlaying)
            {
                return;
            }

            byte[] pcmBytes = null;
            lock (realtimeAudioLock)
            {
                if (realtimeAudioPcmQueue.Count > 0)
                {
                    pcmBytes = realtimeAudioPcmQueue.Dequeue();
                }
            }
            if (pcmBytes == null || pcmBytes.Length == 0)
            {
                return;
            }

            var previousClip = audioSource.clip;
            var clip = Pcm16BytesToAudioClip(pcmBytes, 24000, "YuiRealtimeResponse");
            if (clip == null)
            {
                return;
            }
            audioSource.clip = clip;
            DestroyOwnedAudioClip(previousClip, clip);
            SetStatus("Speaking...");
            audioSource.Play();
        }

        private static AudioClip Pcm16BytesToAudioClip(byte[] pcm, int sampleRate, string clipName)
        {
            if (pcm == null || pcm.Length < 2)
            {
                return null;
            }

            var sampleCount = pcm.Length / 2;
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var value = BitConverter.ToInt16(pcm, i * 2);
                samples[i] = Mathf.Clamp(value / 32768f, -1f, 1f);
            }

            var clip = AudioClip.Create(
                string.IsNullOrWhiteSpace(clipName) ? "YuiRealtimeAudio" : clipName,
                sampleCount,
                1,
                sampleRate > 0 ? sampleRate : 24000,
                false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static string FormatElapsedTime(float seconds)
        {
            var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private static byte[] ConvertToPcm16Mono24k(float[] source, int channels, int sourceRate)
        {
            if (source == null || source.Length == 0 || channels <= 0 || sourceRate <= 0)
            {
                return Array.Empty<byte>();
            }

            var frameCount = source.Length / channels;
            var outputFrames = Mathf.Max(1, Mathf.RoundToInt(frameCount * 24000f / sourceRate));
            var bytes = new byte[outputFrames * 2];
            for (var i = 0; i < outputFrames; i++)
            {
                var sourceFrame = Mathf.Clamp(Mathf.RoundToInt(i * sourceRate / 24000f), 0, frameCount - 1);
                var sum = 0f;
                for (var channel = 0; channel < channels; channel++)
                {
                    sum += source[sourceFrame * channels + channel];
                }
                var sample = Mathf.Clamp(sum / channels, -1f, 1f);
                var value = (short)(sample * short.MaxValue);
                bytes[i * 2] = (byte)(value & 0xff);
                bytes[i * 2 + 1] = (byte)((value >> 8) & 0xff);
            }
            return bytes;
        }

        private string ToWebSocketUrl(string path)
        {
            var baseUrl = client != null ? client.BaseUrl : backendUrl;
            if (baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "wss://" + baseUrl.Substring("https://".Length).TrimEnd('/') + "/" + path.TrimStart('/');
            }
            if (baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return "ws://" + baseUrl.Substring("http://".Length).TrimEnd('/') + "/" + path.TrimStart('/');
            }
            return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        }

        private string SelectMicrophoneDevice()
        {
            var devices = Microphone.devices;
            if (devices == null || devices.Length == 0)
            {
                SetMicrophoneDeviceText("Mic: none");
                Debug.LogWarning("Unity Microphone.devices is empty.");
                return null;
            }

            var list = string.Join(", ", devices);
            Debug.Log($"Unity microphones: {list}");

            if (!string.IsNullOrWhiteSpace(preferredMicrophoneDevice))
            {
                foreach (var device in devices)
                {
                    if (device == preferredMicrophoneDevice)
                    {
                        SetMicrophoneDeviceText($"Mic: {device}");
                        return device;
                    }
                }

                Debug.LogWarning($"Preferred microphone was not found: {preferredMicrophoneDevice}");
            }

            SetMicrophoneDeviceText($"Mic: {devices[0]}");
            return devices[0];
        }

        private int ResolveRecordingFrequency(string device)
        {
            Microphone.GetDeviceCaps(device, out var minFrequency, out var maxFrequency);
            Debug.Log($"Microphone caps device='{device}', min={minFrequency}, max={maxFrequency}");
            if (minFrequency == 0 && maxFrequency == 0)
            {
                return preferredRecordingFrequency;
            }

            return Mathf.Clamp(preferredRecordingFrequency, minFrequency, maxFrequency);
        }

        private async System.Threading.Tasks.Task SendMessageAsync(string message)
        {
            Debug.Log($"Sending message to Yui backend: {message}");
            var totalTimer = System.Diagnostics.Stopwatch.StartNew();
            isSending = true;
            SetInteractable(false);
            AppendLog("You", message);
            SetStatus("Thinking...");
            SetPendingLine(CharacterName, "考え中...");

            try
            {
                SetStatus("Generating...");
                SetPendingLine(CharacterName, "返答生成中...");
                var chatRequestId = Guid.NewGuid().ToString("N");
                var chatTimer = System.Diagnostics.Stopwatch.StartNew();
                var chat = await client.SendChatAsync(
                    new ChatRequest
                    {
                        RequestId = chatRequestId,
                        UserId = userId,
                        Message = message,
                        Context = CreateChatContext(),
                        Secret = secretMode,
                        CustomInstruction = customInstruction,
                        CharacterName = characterName
                    },
                    cancellationTokenSource.Token);
                Debug.Log($"Yui chat latency: {chatTimer.ElapsedMilliseconds} ms");

                ClearPendingLine();
                AppendLog(CharacterName, chat.Text);
                Debug.Log($"Yui motion: face={chat.Face}, anim={chat.Animation}");
                if (avatarController != null)
                {
                    avatarController.ApplyResponse(chat);
                }
                if (chatdollKitController != null)
                {
                    chatdollKitController.ApplyResponse(chat);
                }

                await SpeakResponseAsync(chat, chatRequestId, cancellationTokenSource.Token);
                Debug.Log($"Yui total response latency: {totalTimer.ElapsedMilliseconds} ms");
                SetStatus("Ready");
            }
            catch (YuiBackendException ex) when (ex.StatusCode == 0)
            {
                ClearPendingLine();
                SetStatus("Backend offline");
                AppendLog(
                    "System",
                    $"Backendに接続できません。scripts/run_backend.ps1 を起動してください。url={ex.Url}");
                Debug.LogError(ex);
            }
            catch (Exception ex)
            {
                ClearPendingLine();
                SetStatus("Error");
                var errorMessage = ex is YuiBackendException backendException
                    ? backendException.UserMessage
                    : ex.Message;
                AppendLog("System", errorMessage);
                Debug.LogError(ex);
            }
            finally
            {
                isSending = false;
                SetInteractable(true);
                if (inputField != null)
                {
                    inputField.ActivateInputField();
                }
            }
        }

        private async Task SpeakResponseAsync(
            ChatResponse chat,
            string chatRequestId,
            CancellationToken cancellationToken,
            bool allowChunking = true)
        {
            if (audioSource == null)
            {
                return;
            }

            if (IsTtsMode("silent"))
            {
                Debug.Log("Yui TTS skipped: silent mode");
                return;
            }

            var shouldSpeak = chat.ShouldTts
                || (forceTtsForNonEmptyReplies && !string.IsNullOrWhiteSpace(chat.Text));
            Debug.Log(
                $"Yui TTS decision: should_tts={chat.ShouldTts}, force_non_empty={forceTtsForNonEmptyReplies}, should_speak={shouldSpeak}, text_length={(chat.Text ?? string.Empty).Length}");

            if (!shouldSpeak)
            {
                return;
            }

            var speechText = YuiSpeechTextUtility.CleanSpeechText(chat.Text);
            if (string.IsNullOrWhiteSpace(speechText))
            {
                return;
            }

            SetStatus("Speaking...");
            audioSource.Stop();

            var chunks = allowChunking
                ? YuiSpeechTextUtility.SplitSpeechText(speechText, speechChunkMaxCharacters)
                : new[] { speechText };
            Debug.Log($"Yui TTS chunks: {chunks.Length}");

            for (var index = 0; index < chunks.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunkTimer = System.Diagnostics.Stopwatch.StartNew();
                var clip = await SynthesizeSpeechClipAsync(
                    chunks[index],
                    chat.VoiceStyle,
                    $"{chatRequestId}-tts-{index}",
                    cancellationToken);
                Debug.Log($"Yui TTS chunk {index + 1}/{chunks.Length} latency: {chunkTimer.ElapsedMilliseconds} ms, chars={chunks[index].Length}");
                if (clip == null)
                {
                    continue;
                }

                while (audioSource.isPlaying && !cancellationToken.IsCancellationRequested)
                {
                    // Yielding every frame burns CPU. 30 ms is well below typical
                    // VOICEVOX chunk boundaries and stays imperceptible.
                    await Task.Delay(30, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var previousClip = audioSource.clip;
                audioSource.Stop();
                audioSource.clip = clip;
                DestroyOwnedAudioClip(previousClip, clip);
                audioSource.Play();
            }
        }

        private static void DestroyOwnedAudioClip(AudioClip previousClip, AudioClip nextClip)
        {
            if (previousClip == null || previousClip == nextClip)
            {
                return;
            }

            Destroy(previousClip);
        }

        private async Task<AudioClip> SynthesizeSpeechClipAsync(
            string text,
            string voiceStyle,
            string requestId,
            CancellationToken cancellationToken)
        {
            try
            {
                var canTryLocalVoicevox = !localVoicevoxUnavailable
                    && !IsTtsMode("server")
                    && !IsRemoteBackend()
                    && preferChatdollKitVoicevoxTts
                    && chatdollKitVoicevoxTts != null;
                if (canTryLocalVoicevox)
                {
                    var clip = await chatdollKitVoicevoxTts.SynthesizeAsync(
                        text,
                        voiceStyle,
                        cancellationToken);
                    if (clip != null)
                    {
                        Debug.Log("Yui TTS source: ChatdollKit VoicevoxSpeechSynthesizer");
                        return clip;
                    }

                    Debug.LogWarning("Local VOICEVOX TTS returned no audio clip; falling back to backend TTS.");
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                localVoicevoxUnavailable = true;
                Debug.LogWarning($"ChatdollKit VOICEVOX TTS failed; falling back to backend TTS: {ex.Message}");
            }

            Debug.Log("Yui TTS source: FastAPI backend direct audio");
            return await client.SynthesizeSpeechClipAsync(
                new TtsRequest
                {
                    RequestId = requestId,
                    Text = text,
                    SpeakerId = speakerId,
                    SpeedScale = speedScale,
                    PitchScale = pitchScale,
                    IntonationScale = intonationScale,
                    VolumeScale = synthesisVolumeScale,
                    PrePhonemeLength = prePhonemeLength,
                    PostPhonemeLength = postPhonemeLength
                },
                cancellationToken);
        }

        private bool IsRemoteBackend()
        {
            if (client == null || string.IsNullOrWhiteSpace(client.BaseUrl))
            {
                return false;
            }

            return !client.BaseUrl.Contains("127.0.0.1")
                && !client.BaseUrl.Contains("localhost");
        }

        private void ConfigureChatdollKitVoicevoxTts()
        {
            if (chatdollKitVoicevoxTts == null)
            {
                return;
            }

            chatdollKitVoicevoxTts.Configure(
                "http://127.0.0.1:50021",
                speakerId,
                speedScale,
                pitchScale,
                intonationScale,
                synthesisVolumeScale,
                prePhonemeLength,
                postPhonemeLength);
        }

        private bool IsTtsMode(string mode)
        {
            return string.Equals(ttsMode, mode, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsRealtimeConversationMode()
        {
            return string.Equals(conversationMode, "realtime_voice", StringComparison.OrdinalIgnoreCase)
                || string.Equals(conversationMode, "realtime_voicevox", StringComparison.OrdinalIgnoreCase)
                || string.Equals(conversationMode, "realtime_translate", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsRealtimeVoicevoxMode()
        {
            return string.Equals(conversationMode, "realtime_voicevox", StringComparison.OrdinalIgnoreCase);
        }

        private string RealtimeBackendMode()
        {
            if (string.Equals(conversationMode, "realtime_translate", StringComparison.OrdinalIgnoreCase))
            {
                return "translate";
            }

            if (IsRealtimeVoicevoxMode())
            {
                return "voice_text";
            }

            return "voice";
        }

        private string RealtimeInstructionsForMode(string mode)
        {
            if (string.Equals(mode, "translate", StringComparison.OrdinalIgnoreCase))
            {
                return "You are a realtime interpreter. Translate every Japanese utterance into natural English only. Do not answer questions, acknowledge setup requests, or add commentary. If the user asks for Japanese-to-English translation, translate that utterance instead of confirming the setup. Speak clearly with the brightest, most youthful feminine voice available.";
            }

            var name = string.IsNullOrWhiteSpace(characterName) ? "Yui" : characterName;
            if (string.Equals(mode, "voice_text", StringComparison.OrdinalIgnoreCase))
            {
                return $"{name}として、日本語で自然に会話してください。音声はUnity側のVOICEVOXで読み上げるので、テキストだけを返してください。返答は短めに、音声会話として聞き取りやすくしてください。Web検索、天気、最新情報、外部アプリ操作はこのモードではできません。求められた場合は、調べているふりをせず、このモードでは取得できないことを短く伝えてください。";
            }
            return $"{name}として、日本語で自然に会話してください。返答は短めに、音声会話として聞き取りやすくしてください。可能な範囲で、明るく若い女性らしい高めの声に寄せてください。Web検索、天気、最新情報、外部アプリ操作はこのモードではできません。求められた場合は、調べているふりをせず、このモードでは取得できないことを短く伝えてください。";
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
            if (string.Equals(mode, "realtime_voice", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "voice", StringComparison.OrdinalIgnoreCase))
            {
                return "realtime_voice";
            }

            if (string.Equals(mode, "realtime_voicevox", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "voice_text", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "voicevox", StringComparison.OrdinalIgnoreCase))
            {
                return "realtime_voicevox";
            }

            if (string.Equals(mode, "realtime_translate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "translate", StringComparison.OrdinalIgnoreCase))
            {
                return "realtime_translate";
            }

            return "stable";
        }

        private static string RealtimeModeLabel(string mode)
        {
            if (string.Equals(mode, "realtime_voice", StringComparison.OrdinalIgnoreCase))
            {
                return "Realtime Voice ON";
            }

            if (string.Equals(mode, "realtime_voicevox", StringComparison.OrdinalIgnoreCase))
            {
                return "Realtime VOICEVOX ON";
            }

            if (string.Equals(mode, "realtime_translate", StringComparison.OrdinalIgnoreCase))
            {
                return "Realtime Translate ON";
            }

            return string.Empty;
        }

        private static string RealtimeModeWarningText(string mode)
        {
            var label = RealtimeModeLabel(mode);
            return string.IsNullOrEmpty(label)
                ? string.Empty
                : $"{label}: 実験機能です。音声ストリーム接続中はAPIコストが増えやすいので、使う時だけオンにしてください。";
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

        private string GetDefaultAvatarSlot()
        {
            return YuiAvatarSlots.UnityChanDefault;
        }

        private static string AvatarSlotPrefsKey => $"{AvatarSlotKey}.{GetLocalPrefsScope()}";

        private static string GetLocalPrefsScope()
        {
            var source = string.IsNullOrWhiteSpace(Application.dataPath)
                ? Application.identifier
                : Application.dataPath;
            return StableHash(source ?? "default");
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                for (var i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash.ToString("x8");
            }
        }

        private void ApplyAvatarSlot(bool showStatus)
        {
            if (avatarSwitcher == null)
            {
                return;
            }

            var requestedSlot = avatarSlot;
            var waitForSavedCustomVrm = YuiAvatarSlots.IsCustomVrm(requestedSlot)
                && runtimeVrmImporter != null
                && runtimeVrmImporter.HasRestorableSavedCustomVrm
                && !avatarSwitcher.HasCustomAvatar;
            avatarSwitcher.SetAvatarSlot(avatarSlot, !waitForSavedCustomVrm);
            if (showStatus)
            {
                if (!string.Equals(requestedSlot, avatarSwitcher.ActiveSlot, StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus(YuiAvatarSlots.IsCustomVrm(requestedSlot)
                        ? "Load a Custom VRM first; using the default avatar."
                        : "Selected avatar is not available; using the default avatar.");
                }
                else
                {
                    SetStatus("Avatar setting saved");
                }
            }
        }

        private void AppendLog(string speaker, string text)
        {
            var displayText = speaker == "Yui" ? YuiSpeechTextUtility.CleanDisplayText(text) : text;
            Debug.Log($"{speaker}: {displayText}");
            chatLogView?.AppendLog(speaker, displayText);
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

        private void UpdateAppAwareness()
        {
            if (!EnableDormantAppAwarenessPrototype
                || !appAwarenessEnabled
                || appMonitor == null
                || Time.realtimeSinceStartup < nextAppAwarenessPollAt)
            {
                return;
            }

            nextAppAwarenessPollAt = Time.realtimeSinceStartup + Mathf.Max(0.5f, appAwarenessPollInterval);
            var app = appMonitor.GetForegroundApp();
            var nextKey = app.StableKey();
            if (nextKey == currentForegroundAppKey)
            {
                return;
            }

            currentForegroundApp = app;
            currentForegroundAppKey = nextKey;
            appContextStatus = app.IsAvailable ? app.StatusLabel() : "";
            if (app.IsAvailable)
            {
                Debug.Log($"Yui app awareness: category={app.Category}, process={app.ProcessName}, display={app.DisplayName}");
            }

            RenderStatus();
        }

        private void SetPendingLine(string speaker, string text)
        {
            chatLogView?.SetPendingLine(speaker, text);
        }

        private void ClearPendingLine()
        {
            chatLogView?.ClearPendingLine();
        }

        private void SetStatus(string status)
        {
            currentStatus = string.IsNullOrWhiteSpace(status) ? "Ready" : status;
            RenderStatus();
        }

        private void RenderStatus()
        {
            if (statusText != null)
            {
                statusText.supportRichText = true;
                statusText.color = Color.white;
                statusText.alignment = TextAnchor.MiddleLeft;
                var modeLabel = RealtimeModeLabel(conversationMode);
                var modePrefix = string.IsNullOrEmpty(modeLabel)
                    ? string.Empty
                    : $"<color=#f5c542><b>{modeLabel}</b></color>\n";
                statusText.text = secretMode
                    ? $"{modePrefix}<b>Secret Mode</b>\n{currentStatus}"
                    : $"{modePrefix}{currentStatus}";
                if (!string.IsNullOrWhiteSpace(appContextStatus))
                {
                    statusText.text += $"\n<color=#a8c7ff>{appContextStatus}</color>";
                }
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
                sendButtonText.text = interactable ? "Go" : "...";
            }

            if (recordButton != null)
            {
                recordButton.interactable = interactable || isRecording;
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
                inputField.interactable = interactable;
            }
        }

        private void SetRecordButtonText(string text)
        {
            if (recordButtonText != null)
            {
                recordButtonText.text = text;
            }
        }

        private void SetLookButtonText(string text)
        {
            if (lookButtonText != null)
            {
                lookButtonText.text = text;
            }
        }

        private void SetImportImageButtonText(string text)
        {
            if (importImageButtonText != null)
            {
                importImageButtonText.text = text;
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
            if (microphoneLevelFill != null)
            {
                value = Mathf.Clamp01(value);
                microphoneLevelFill.fillAmount = value;
                var rect = microphoneLevelFill.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(value, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
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
            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Meiryo", "Yu Gothic", "MS Gothic", "Arial" },
                20);

            if (font == null)
            {
                return;
            }

            foreach (var text in GetComponentsInChildren<Text>(true))
            {
                text.font = font;
            }
        }
    }
}

