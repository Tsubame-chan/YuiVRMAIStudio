using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Audio;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiSettingsOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject settingsRoot;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button advancedButton;
        [SerializeField] private Button voicePreviewButton;
        [SerializeField] private Button microphoneTestButton;
        [SerializeField] private Button customVrmImportButton;
        [SerializeField] private Button cameraAdjustButton;
        [SerializeField] private Button cameraAutoButton;
        [SerializeField] private Button cameraSaveButton;
        [SerializeField] private Button cameraDeleteButton;
        [SerializeField] private GameObject cameraAdjustRoot;
        [SerializeField] private Button cameraAdjustDoneButton;
        [SerializeField] private Button clearHistoryButton;
        [SerializeField] private GameObject clearConfirmRoot;
        [SerializeField] private Button clearConfirmButton;
        [SerializeField] private Button clearCancelButton;
        [SerializeField] private GameObject advancedRoot;
        [SerializeField] private InputField backendUrlInput;
        [SerializeField] private InputField openAiApiKeyInput;
        [SerializeField] private InputField openAiModelInput;
        [SerializeField] private Toggle autoAiFallbackToggle;
        [SerializeField] private Dropdown voicePresetDropdown;
        [SerializeField] private InputField voicePresetNameInput;
        [SerializeField] private Button voicePresetSaveButton;
        [SerializeField] private Button voicePresetDeleteButton;
        [SerializeField] private Dropdown speakerDropdown;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Text volumeValueText;
        [SerializeField] private Slider speedSlider;
        [SerializeField] private Text speedValueText;
        [SerializeField] private Slider pitchSlider;
        [SerializeField] private Text pitchValueText;
        [SerializeField] private Slider intonationSlider;
        [SerializeField] private Text intonationValueText;
        [SerializeField] private Slider synthesisVolumeSlider;
        [SerializeField] private Text synthesisVolumeValueText;
        [SerializeField] private Slider prePhonemeSlider;
        [SerializeField] private Text prePhonemeValueText;
        [SerializeField] private Slider postPhonemeSlider;
        [SerializeField] private Text postPhonemeValueText;
        [SerializeField] private Dropdown conversationModeDropdown;
        [SerializeField] private Dropdown ttsModeDropdown;
        [SerializeField] private Dropdown irodoriVoiceGenderDropdown;
        [SerializeField] private InputField irodoriVoiceInstructInput;
        [SerializeField] private Dropdown microphoneDropdown;
        [SerializeField] private Dropdown lookCameraDropdown;
        [SerializeField] private Dropdown backgroundDropdown;
        [SerializeField] private Dropdown avatarDropdown;
        [SerializeField] private Dropdown cameraPresetDropdown;
        [SerializeField] private Dropdown resolutionDropdown;
        [SerializeField] private InputField characterNameInput;
        [SerializeField] private InputField customInstructionInput;
        [SerializeField] private YuiChatPanel chatPanel;
        [SerializeField] private YuiBackgroundManager backgroundManager;
        [SerializeField] private YuiConsoleVisibilityController consoleVisibilityController;
        [SerializeField] private YuiWindowResolutionController windowResolutionController;
        private Image microphoneTestLevelFill;
        private Text microphoneTestStatusText;
        private Text localAiAssetStatusText;
        private Button localAiAssetRepairButton;
        private YuiMicrophoneDeviceSelector microphoneTestDeviceSelector;
        private YuiUnityMicrophoneRecorder microphoneTestRecorder;
        private YuiMacEditorMicrophoneRecorder microphoneTestMacFallback;
        private InputField customVrmNameInput;
        private Button customVrmClearButton;
        private string microphoneTestDevice;
        private int microphoneTestFrequency = 44100;
        private float microphoneTestStartedAt = -1f;
        private readonly float[] microphoneTestSamples = new float[256];
        private bool advancedVisible;
        private bool isPreviewingVoice;
        private string lastTtsModeValue = "server";


        private void Awake()
        {
            if (chatPanel == null)
            {
                chatPanel = YuiSceneObjectFinder.FindFirst<YuiChatPanel>();
            }

            if (backgroundManager == null)
            {
                backgroundManager = YuiSceneObjectFinder.FindFirst<YuiBackgroundManager>();
            }

            if (windowResolutionController == null)
            {
                windowResolutionController = YuiSceneObjectFinder.FindFirst<YuiWindowResolutionController>();
            }

            if (consoleVisibilityController == null)
            {
                consoleVisibilityController = YuiSceneObjectFinder.FindFirst<YuiConsoleVisibilityController>();
            }

            Bind();
            YuiToolbarIconUtility.ApplySettingsIcon(openButton);
            Hide();
        }

        private void OnDestroy()
        {
            StopMicrophoneMonitor();
            Unbind();
        }

        private void Update()
        {
            UpdateMicrophoneMonitor();
        }

        public void Configure(
            GameObject root,
            Button open,
            Button close,
            Button apply,
            Button advanced,
            Button voicePreview,
            Button microphoneTest,
            Button customVrmImport,
            Button cameraAdjust,
            Button cameraAuto,
            Button cameraSave,
            Button cameraDelete,
            GameObject cameraAdjustPanel,
            Button cameraAdjustDone,
            Button clearButton,
            GameObject clearConfirm,
            Button clearConfirmAction,
            Button clearCancelAction,
            GameObject advancedPanel,
            InputField backendInput,
            Dropdown speaker,
            Slider volume,
            Text volumeValue,
            Slider speed,
            Text speedValue,
            Slider pitch,
            Text pitchValue,
            Slider intonation,
            Text intonationValue,
            Slider synthesisVolume,
            Text synthesisVolumeValue,
            Slider prePhoneme,
            Text prePhonemeValue,
            Slider postPhoneme,
            Text postPhonemeValue,
            Dropdown conversationMode,
            Dropdown ttsMode,
            Dropdown microphone,
            Dropdown lookCamera,
            Dropdown background,
            Dropdown avatar,
            Dropdown cameraPreset,
            Dropdown resolution,
            InputField characterName,
            InputField customInstruction,
            YuiChatPanel panel,
            YuiBackgroundManager backgrounds,
            YuiConsoleVisibilityController consoleController,
            YuiWindowResolutionController windowResolution)
        {
            Unbind();

            settingsRoot = root;
            openButton = open;
            closeButton = close;
            applyButton = apply;
            advancedButton = advanced;
            voicePreviewButton = voicePreview;
            microphoneTestButton = microphoneTest;
            customVrmImportButton = customVrmImport;
            cameraAdjustButton = cameraAdjust;
            cameraAutoButton = cameraAuto;
            cameraSaveButton = cameraSave;
            cameraDeleteButton = cameraDelete;
            cameraAdjustRoot = cameraAdjustPanel;
            cameraAdjustDoneButton = cameraAdjustDone;
            clearHistoryButton = clearButton;
            clearConfirmRoot = clearConfirm;
            clearConfirmButton = clearConfirmAction;
            clearCancelButton = clearCancelAction;
            advancedRoot = advancedPanel;
            backendUrlInput = backendInput;
            speakerDropdown = speaker;
            volumeSlider = volume;
            volumeValueText = volumeValue;
            speedSlider = speed;
            speedValueText = speedValue;
            pitchSlider = pitch;
            pitchValueText = pitchValue;
            intonationSlider = intonation;
            intonationValueText = intonationValue;
            synthesisVolumeSlider = synthesisVolume;
            synthesisVolumeValueText = synthesisVolumeValue;
            prePhonemeSlider = prePhoneme;
            prePhonemeValueText = prePhonemeValue;
            postPhonemeSlider = postPhoneme;
            postPhonemeValueText = postPhonemeValue;
            conversationModeDropdown = conversationMode;
            ttsModeDropdown = ttsMode;
            microphoneDropdown = microphone;
            lookCameraDropdown = lookCamera;
            backgroundDropdown = background;
            avatarDropdown = avatar;
            cameraPresetDropdown = cameraPreset;
            resolutionDropdown = resolution;
            characterNameInput = characterName;
            customInstructionInput = customInstruction;
            chatPanel = panel;
            backgroundManager = backgrounds;
            consoleVisibilityController = consoleController;
            windowResolutionController = windowResolution;

            Bind();
            YuiToolbarIconUtility.ApplySettingsIcon(openButton);
            Hide();
        }

        public async void Show()
        {
            if (settingsRoot != null)
            {
                settingsRoot.SetActive(true);
                settingsRoot.transform.SetAsLastSibling();
            }

            EnsureOverlayCanvas(settingsRoot, 5000);
            ResolveRuntimeMeterReferences();
            if (chatPanel != null)
            {
                using var capabilityRefresh = new CancellationTokenSource(1500);
                await chatPanel.RefreshCapabilitySnapshotAsync(capabilityRefresh.Token);
            }

            RepairMissingRuntimeUi();
            ApplyResponsiveOverlayLayout();
            RefreshFields();
            RefreshLocalAiAssetStatus();
            HideClearConfirm();
            if (settingsRoot != null)
            {
                settingsRoot.SetActive(true);
                settingsRoot.transform.SetAsLastSibling();
            }

            Canvas.ForceUpdateCanvases();
        }

        public void Hide()
        {
            StopMicrophoneMonitor();
            HideClearConfirm();
            SetCameraAdjustVisible(false);
            if (settingsRoot != null)
            {
                settingsRoot.SetActive(false);
            }

            isPreviewingVoice = false;
            SetVoicePreviewInteractable(true);
        }

    }
}
