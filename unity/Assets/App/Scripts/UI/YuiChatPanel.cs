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
    public sealed partial class YuiChatPanel : MonoBehaviour
    {
        private const string BackendUrlKey = YuiPrefsKeys.BackendUrl;
        private const string OpenAiApiKeyKey = YuiPrefsKeys.OpenAiApiKey;
        private const string OpenAiModelKey = YuiPrefsKeys.OpenAiModel;
        private const string AutoAiFallbackEnabledKey = YuiPrefsKeys.AutoAiFallbackEnabled;
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
        private const string VoiceTuningSchemaVersionKey = YuiPrefsKeys.VoiceTuningSchemaVersion;
        private const string IrodoriVoiceGenderKey = YuiPrefsKeys.IrodoriVoiceGender;
        private const string IrodoriVoiceInstructKey = YuiPrefsKeys.IrodoriVoiceInstruct;
        private const string MicrophoneDeviceKey = YuiPrefsKeys.MicrophoneDevice;
        private const string LookCameraDeviceKey = YuiPrefsKeys.LookCameraDevice;
        private const string SecretModeKey = YuiPrefsKeys.SecretMode;
        private const string CustomInstructionKey = YuiPrefsKeys.CustomInstruction;
        private const string CharacterNameKey = YuiPrefsKeys.CharacterName;
        private const string AvatarSlotKey = YuiPrefsKeys.AvatarSlot;
        private const string ClientSchemaVersion = "2026-05-10";
        private const int CurrentVoiceTuningSchemaVersion = 8;
        private const bool EnableDormantAppAwarenessPrototype = false;
        private static readonly bool EnableBackendDiagnosticsLog = false;

        [Header("Backend")]
        [SerializeField] private string backendUrl = "http://127.0.0.1:8000";
        [SerializeField] private string openAiApiKey = "";
        [SerializeField] private string openAiModel = YuiDirectOpenAiClient.DefaultModel;
        [SerializeField] private bool autoAiFallbackEnabled = true;
        [SerializeField] private string userId = "local_user";
        [SerializeField] private int speakerId = 14;
        [SerializeField] private float speedScale = 1.0f;
        [SerializeField] private float pitchScale = 0.0f;
        [SerializeField] private float intonationScale = 1.0f;
        [SerializeField] private float synthesisVolumeScale = 1.0f;
        [SerializeField] private float prePhonemeLength = 0.1f;
        [SerializeField] private float postPhonemeLength = 0.1f;
        [SerializeField] private string conversationMode = "stable";
        [SerializeField] private string ttsMode = "server";
        [SerializeField] private string irodoriVoiceGender = "female";
        [SerializeField] private string irodoriVoiceInstruct = "若い女性の、明るく可愛いアニメ調の声で話してください。";
        [SerializeField] private string characterName = "Yui";
        [SerializeField] private string customInstruction = "";
        [SerializeField] private string avatarSlot = YuiBuildProfile.DefaultAvatarSlot;

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
        [SerializeField] private float lookCameraWarmupSeconds = 1.1f;
        [SerializeField] private int lookCameraMaxCandidateFrames = 8;
        [SerializeField] private bool appAwarenessEnabled = false;
        [SerializeField] private float appAwarenessPollInterval = 2f;

        private CancellationTokenSource cancellationTokenSource;
        private YuiBackendClient client;
        private YuiMicrophoneDeviceSelector microphoneDeviceSelector;
        private YuiUnityMicrophoneRecorder unityMicrophoneRecorder;
        private YuiMacEditorRealtimeMicrophoneStreamer macEditorRealtimeMicrophoneStreamer;
        private bool isSending;
        private bool isRecording;
        private AudioClip recordingClip;
        private YuiMacEditorMicrophoneRecorder macEditorMicrophoneRecorder;
        private float macEditorMicrophoneFallbackRms;
        private float macEditorMicrophoneFallbackPeak;
        private string activeMicrophoneDevice;
        private int activeRecordingFrequency;
        private float recordingStartedAt;
        private readonly float[] microphoneSampleBuffer = new float[256];
        private VisionResponse latestVision;
        private readonly YuiPendingVisionImageAttachment pendingVisionImageAttachment = new YuiPendingVisionImageAttachment();
        private bool secretMode;
        private string currentStatus = "Ready";
        private bool localVoicevoxUnavailable;
        private ClientWebSocket realtimeSocket;
        private CancellationTokenSource realtimeCancellationTokenSource;
        private CancellationTokenSource realtimeVoicevoxSpeechCancellationTokenSource;
        private readonly SemaphoreSlim realtimeSendLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim aivisNativeSynthesisLock = new SemaphoreSlim(1, 1);
        private readonly Queue<byte[]> realtimeAudioPcmQueue = new Queue<byte[]>();
        private readonly List<byte> realtimeAudioPcmBuffer = new List<byte>(48000);
        private readonly List<byte> realtimeTranslatePcmBuffer = new List<byte>(96000);
        private readonly YuiRealtimeVadGate realtimeVadGate = new YuiRealtimeVadGate(YuiRealtimeTuning.ClientVadFor(false));
        private readonly object realtimeAudioLock = new object();
        private readonly StringBuilder realtimeTextBuffer = new StringBuilder();
        private readonly StringBuilder realtimeVoicevoxPendingText = new StringBuilder();
        private readonly Queue<string> realtimeVoicevoxSpeechQueue = new Queue<string>();
        private readonly object realtimeVoicevoxLock = new object();
        private bool realtimeStreamActive;
        private bool realtimeAssistantTurnActive;
        private bool realtimeWaitingForResponse;
        private bool realtimeRestarting;
        private bool realtimeVoicevoxSpeechActive;
        private string realtimeActiveBackendMode = "voice";
        private int realtimeLastSamplePosition;
        private float realtimeNextChunkAt;
        private int realtimeCompletedTurns;
        private int realtimeVoicevoxGeneration;
        private System.Diagnostics.Stopwatch realtimeVoicevoxTurnTimer;
        private long realtimeVoicevoxFirstTextMs = -1;
        private long realtimeVoicevoxDoneMs = -1;
        private YuiWindowsForegroundAppMonitor appMonitor;
        private YuiForegroundAppInfo currentForegroundApp = new YuiForegroundAppInfo();
        private string currentForegroundAppKey = "";
        private string appContextStatus = "";
        private float nextAppAwarenessPollAt;
        private float displayedMicrophoneLevel;
        private float lastBackendSuccessAt = -999f;
        private bool backendMonitorStarted;
        private bool httpTtsAvailable;
        private bool backendConfigLoaded;
        private ProviderStatusResponse cachedProviderStatus;
        private float lastProviderStatusSuccessAt = -999f;
        private YuiDirectOpenAiClient directOpenAiClient;
        private IReadOnlyList<string> chatProviderOptions = Array.Empty<string>();
        private IReadOnlyList<string> visionProviderOptions = Array.Empty<string>();
        private IReadOnlyList<string> ttsProviderOptions = Array.Empty<string>();
        private IReadOnlyList<TtsVoiceOption> backendAivisVoiceOptions = Array.Empty<TtsVoiceOption>();
        private IReadOnlyList<string> sttProviderOptions = Array.Empty<string>();
        private YuiLocalAiDownloadOverlay localAiDownloadOverlay;

        public string BackendUrl => backendUrl;
        public string OpenAiApiKey => openAiApiKey;
        public string OpenAiModel => openAiModel;
        public bool AutoAiFallbackEnabled => autoAiFallbackEnabled;
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
        public string IrodoriVoiceGender => irodoriVoiceGender;
        public string IrodoriVoiceInstruct => irodoriVoiceInstruct;
        public bool HttpTtsAvailable => httpTtsAvailable;
        public bool BackendConfigLoaded => backendConfigLoaded;
        public IReadOnlyList<string> ChatProviderOptions => chatProviderOptions;
        public IReadOnlyList<string> VisionProviderOptions => visionProviderOptions;
        public IReadOnlyList<string> TtsProviderOptions => ttsProviderOptions;
        public IReadOnlyList<TtsVoiceOption> BackendAivisVoiceOptions => backendAivisVoiceOptions;
        public IReadOnlyList<string> SttProviderOptions => sttProviderOptions;
        public string PreferredMicrophoneDevice => preferredMicrophoneDevice;
        public string PreferredLookCameraDevice => preferredLookCameraDevice;
        public bool SecretMode => secretMode;
        public string CharacterName => characterName;
        public string CustomInstruction => customInstruction;
        public string AvatarSlot => avatarSlot;
        public string LocalAiAssetStatusText => localAiDownloadOverlay != null
            ? localAiDownloadOverlay.CurrentStatusText
            : "Local AI data: not checked";

        private void Awake()
        {
            ConfigureMobileLogStackTraces();
            YuiMemoryDiagnostics.RegisterLowMemoryHandler();
            YuiMemoryDiagnostics.LogSnapshot("awake");
            LoadSavedRuntimeSettings();
            client = new YuiBackendClient(backendUrl);
            ConfigureAiRuntimeRouter();
            microphoneDeviceSelector = new YuiMicrophoneDeviceSelector(preferredRecordingFrequency);
            unityMicrophoneRecorder = new YuiUnityMicrophoneRecorder();
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
                if (!appMonitor.IsSupported)
                {
                    appAwarenessEnabled = false;
                    appContextStatus = "";
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
            YuiToolbarIconUtility.ApplySecretIcon(secretModeButton);
            UpdateSecretModeUi();
            SetStatus("Ready");
        }

        private static void ConfigureMobileLogStackTraces()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.ScriptOnly);
#endif
        }

    }
}
