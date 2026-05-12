using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiSettingsOverlay : MonoBehaviour
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
        private AudioClip microphoneTestClip;
        private InputField customVrmNameInput;
        private Button customVrmClearButton;
        private string microphoneTestDevice;
        private int microphoneTestFrequency = 44100;
        private float microphoneTestStartedAt = -1f;
        private readonly float[] microphoneTestSamples = new float[256];
        private bool advancedVisible;
        private bool isPreviewingVoice;
        private bool previewVoiceStartedThisOpen;
        private int resolutionPresetOnOpen = -1;

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
            Hide();
        }

        public void Show()
        {
            previewVoiceStartedThisOpen = false;
            if (settingsRoot != null)
            {
                settingsRoot.SetActive(true);
                settingsRoot.transform.SetAsLastSibling();
            }

            EnsureOverlayCanvas(settingsRoot, 5000);
            ResolveRuntimeMeterReferences();
            RepairMissingRuntimeUi();
            ApplyResponsiveOverlayLayout();
            RefreshFields();
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

        private void Apply()
        {
            ApplyFieldsToRuntime(true);
            Hide();
        }

        private void ApplyFieldsToRuntime(bool allowResolutionChange)
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
                SaveCustomVrmDisplayNameFromInput();
                chatPanel.SetCharacterName(characterNameInput != null ? characterNameInput.text : chatPanel.CharacterName);
                chatPanel.SetCustomInstruction(customInstructionInput != null ? customInstructionInput.text : chatPanel.CustomInstruction);
                var nextAvatarSlot = AvatarSlotValue();
                if (!string.Equals(nextAvatarSlot, chatPanel.AvatarSlot, System.StringComparison.OrdinalIgnoreCase))
                {
                    chatPanel.SetAvatarSlot(nextAvatarSlot);
                }
                chatPanel.ApplyRuntimeSettings(
                    backendUrl,
                    speakerId,
                    volume,
                    speedSlider != null ? speedSlider.value : 1.0f,
                    pitchSlider != null ? pitchSlider.value : 0.0f,
                    intonationSlider != null ? intonationSlider.value : 1.0f,
                    synthesisVolumeSlider != null ? synthesisVolumeSlider.value : 1.0f,
                    prePhonemeSlider != null ? prePhonemeSlider.value : 0.1f,
                    postPhonemeSlider != null ? postPhonemeSlider.value : 0.1f,
                    ConversationModeValue(),
                    TtsModeValue(),
                    MicrophoneValue(),
                    LookCameraValue());
            }

            if (backgroundManager != null && backgroundDropdown != null)
            {
                backgroundManager.SetPreset(backgroundDropdown.value);
            }

            if (allowResolutionChange
                && windowResolutionController != null
                && resolutionDropdown != null
                && resolutionDropdown.value != resolutionPresetOnOpen)
            {
                windowResolutionController.SetPreset(resolutionDropdown.value);
                resolutionPresetOnOpen = windowResolutionController.PresetIndex;
            }
        }

        private void PreviewVoice()
        {
            if (isPreviewingVoice)
            {
                return;
            }

            if (previewVoiceStartedThisOpen)
            {
                return;
            }

            ApplyFieldsToRuntime(false);
            if (chatPanel != null)
            {
                isPreviewingVoice = true;
                previewVoiceStartedThisOpen = true;
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
            Debug.Log("Yui settings UI repaired: ensured Experimental / Mode dropdown.");
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

        private void ApplyResponsiveOverlayLayout()
        {
            if (settingsRoot == null)
            {
                return;
            }

            var rootRect = settingsRoot.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
            }

            var rootImage = settingsRoot.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.color = new Color(0.02f, 0.025f, 0.03f, 0.72f);
            }

            var panel = settingsRoot.transform.Find("Panel");
            if (panel == null)
            {
                return;
            }

            SetAnchorsRuntime(panel, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.94f));
            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.075f, 0.08f, 0.095f, 1f);
            }

            EnsureOpaqueBacking(panel);

            SetAnchorsRuntime(panel.Find("Title"), new Vector2(0.06f, 0.92f), new Vector2(0.34f, 0.985f));
            SetAnchorsRuntime(advancedButton != null ? advancedButton.transform : panel.Find("AdvancedButton"), new Vector2(0.37f, 0.915f), new Vector2(0.55f, 0.985f));
            SetAnchorsRuntime(applyButton != null ? applyButton.transform : panel.Find("ApplyButton"), new Vector2(0.57f, 0.915f), new Vector2(0.75f, 0.985f));
            SetAnchorsRuntime(closeButton != null ? closeButton.transform : panel.Find("CloseButton"), new Vector2(0.86f, 0.915f), new Vector2(0.96f, 0.985f));

            var scroll = panel.Find("SettingsScroll");
            if (scroll != null)
            {
                SetAnchorsRuntime(scroll, new Vector2(0.04f, 0.045f), new Vector2(0.96f, 0.895f));
                var scrollImage = scroll.GetComponent<Image>();
                if (scrollImage != null)
                {
                    scrollImage.color = new Color(0f, 0f, 0f, 0.18f);
                }
            }

            var content = UiTreeUtility.FindDeepChild(settingsRoot.transform, "Content");
            var contentRect = content != null ? content.GetComponent<RectTransform>() : null;
            if (contentRect != null)
            {
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = new Vector2(0f, Mathf.Max(contentRect.sizeDelta.y, 2330f));
            }

            if (content != null)
            {
                ReflowSettingsRowsRuntime(content);
            }
        }

        private static void ReflowSettingsRowsRuntime(Transform content)
        {
            SetTopRectRuntime(content.Find("MicSection"), 18f, 18f, 22f, 34f);
            SetTopRectRuntime(content.Find("MicrophoneLabel"), 18f, 72f, 248f, 34f);
            SetTopRectRuntime(content.Find("MicrophoneDropdown"), 176f, 62f, 22f, 54f);
            SetTopRectRuntime(content.Find("MicrophoneTestButton"), 176f, 122f, 22f, 44f);
            SetTopRectRuntime(content.Find("MicrophoneTestMeter"), 176f, 172f, 22f, 18f);
            SetTopRectRuntime(content.Find("MicrophoneTestStatus"), 176f, 192f, 22f, 24f);

            SetTopRectRuntime(content.Find("InputCameraSection"), 18f, 250f, 22f, 34f);
            SetTopRectRuntime(content.Find("LookCameraLabel"), 18f, 304f, 248f, 34f);
            SetTopRectRuntime(content.Find("LookCameraDropdown"), 176f, 294f, 22f, 54f);

            SetTopRectRuntime(content.Find("ExperimentalSection"), 18f, 382f, 22f, 34f);
            SetTopRectRuntime(content.Find("ConversationModeLabel"), 18f, 436f, 248f, 34f);
            SetTopRectRuntime(content.Find("ConversationModeDropdown"), 176f, 426f, 22f, 54f);

            SetTopRectRuntime(content.Find("AvatarSection"), 18f, 512f, 22f, 34f);
            SetTopRectRuntime(content.Find("CustomVrmNameLabel"), 18f, 536f, 248f, 34f);
            SetTopRectRuntime(content.Find("CustomVrmNameInput"), 176f, 526f, 22f, 42f);
            SetTopRectColumnRuntime(content.Find("CustomVrmImportButton"), 176f, 22f, 574f, 42f, 0f, 0.50f, 8f);
            SetTopRectColumnRuntime(content.Find("CustomVrmClearButton"), 176f, 22f, 574f, 42f, 0.50f, 1f, 8f);
            SetTopRectRuntime(content.Find("AvatarLabel"), 18f, 632f, 248f, 34f);
            SetTopRectRuntime(content.Find("AvatarDropdown"), 176f, 622f, 22f, 54f);

            SetTopRectRuntime(content.Find("CameraSection"), 18f, 730f, 22f, 34f);
            SetTopRectRuntime(content.Find("CameraPresetLabel"), 18f, 784f, 248f, 34f);
            SetTopRectRuntime(content.Find("CameraPresetDropdown"), 176f, 774f, 22f, 54f);
            SetTopRectRuntime(content.Find("CameraAdjustButton"), 176f, 834f, 22f, 44f);
            SetTopRectRuntime(content.Find("CameraActionLabel"), 18f, 894f, 248f, 34f);
            SetTopRectColumnRuntime(content.Find("CameraAutoButton"), 176f, 22f, 884f, 40f, 0f, 0.44f, 8f);
            SetTopRectColumnRuntime(content.Find("CameraSaveButton"), 176f, 22f, 884f, 40f, 0.44f, 0.72f, 8f);
            SetTopRectColumnRuntime(content.Find("CameraDeleteButton"), 176f, 22f, 884f, 40f, 0.72f, 1f, 8f);

            SetTopRectRuntime(content.Find("VoiceSection"), 18f, 972f, 22f, 34f);
            SetTopRectRuntime(content.Find("VolumeLabel"), 18f, 1029f, 248f, 34f);
            SetTopRectRuntime(content.Find("VolumeSlider"), 176f, 1034f, 92f, 28f);
            SetTopRightRectRuntime(content.Find("VolumeValue"), 18f, 1028f, 70f, 34f);
            SetTopRectRuntime(content.Find("SpeakerLabel"), 18f, 1104f, 248f, 34f);
            SetTopRectRuntime(content.Find("SpeakerDropdown"), 176f, 1094f, 22f, 54f);
            SetTopRectRuntime(content.Find("TtsModeLabel"), 18f, 1174f, 248f, 34f);
            SetTopRectRuntime(content.Find("TtsModeDropdown"), 176f, 1164f, 22f, 54f);
            SetTopRectRuntime(content.Find("VoicePreviewLabel"), 18f, 1244f, 248f, 34f);
            SetTopRectRuntime(content.Find("VoicePreviewButton"), 176f, 1234f, 22f, 54f);
            SetTopRectRuntime(content.Find("SpeedLabel"), 18f, 1311f, 248f, 34f);
            SetTopRectRuntime(content.Find("SpeedSlider"), 176f, 1316f, 92f, 28f);
            SetTopRightRectRuntime(content.Find("SpeedValue"), 18f, 1310f, 70f, 34f);
            SetTopRectRuntime(content.Find("PitchLabel"), 18f, 1381f, 248f, 34f);
            SetTopRectRuntime(content.Find("PitchSlider"), 176f, 1386f, 92f, 28f);
            SetTopRightRectRuntime(content.Find("PitchValue"), 18f, 1380f, 70f, 34f);
        }

        private static void EnsureOverlayCanvas(GameObject root, int sortingOrder)
        {
            if (root == null)
            {
                return;
            }

            var canvas = root.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = root.AddComponent<Canvas>();
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            if (root.GetComponent<GraphicRaycaster>() == null)
            {
                root.AddComponent<GraphicRaycaster>();
            }
        }

        private static void EnsureOpaqueBacking(Transform panel)
        {
            var backing = panel.Find("OpaqueBacking");
            if (backing == null)
            {
                var backingObject = new GameObject("OpaqueBacking", typeof(RectTransform), typeof(Image));
                backingObject.transform.SetParent(panel, false);
                backing = backingObject.transform;
            }

            SetAnchorsRuntime(backing, Vector2.zero, Vector2.one);
            var image = backing.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.075f, 0.08f, 0.095f, 1f);
                image.raycastTarget = true;
            }

            backing.SetAsFirstSibling();
        }

        private static void ShiftSettingsRowsAfter(Transform content, float topInclusive, float delta)
        {
            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child.name == "ConversationModeLabel" || child.name == "ConversationModeDropdown")
                {
                    continue;
                }

                var rect = child.GetComponent<RectTransform>();
                if (rect == null || rect.anchorMin.y != 1f || rect.anchorMax.y != 1f)
                {
                    continue;
                }

                var top = -rect.offsetMax.y;
                if (top < topInclusive)
                {
                    continue;
                }

                rect.offsetMin = new Vector2(rect.offsetMin.x, rect.offsetMin.y - delta);
                rect.offsetMax = new Vector2(rect.offsetMax.x, rect.offsetMax.y - delta);
            }

            var contentRect = content.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, contentRect.sizeDelta.y + delta);
            }
        }

        private static void EnsureRuntimeSectionLabel(Transform parent, string name, string value, float top)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                SetTopRectRuntime(existing, 18f, top, 22f, 34f);
                return;
            }

            var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            var label = labelObject.GetComponent<Text>();
            label.text = value;
            label.font = BuiltinUiFont();
            label.fontSize = 16;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.7f, 0.9f, 1f, 1f);
            label.alignment = TextAnchor.MiddleLeft;
            SetTopRectRuntime(labelObject.transform, 18f, top, 22f, 34f);
        }

        private static void CreateOrMoveRuntimeLabel(Transform parent, string name, string value, float top)
        {
            var existing = parent.Find(name);
            var labelObject = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            var label = labelObject.GetComponent<Text>();
            label.text = value;
            label.font = BuiltinUiFont();
            label.fontSize = 14;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            SetTopRectRuntime(labelObject.transform, 18f, top, 248f, 34f);
        }

        private static Font BuiltinUiFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Font.CreateDynamicFontFromOSFont("Arial", 14);
        }

        private static void SetTopRectRuntime(Transform target, float left, float top, float right, float height)
        {
            if (target == null)
            {
                return;
            }

            var rect = target.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTopRightRectRuntime(Transform target, float right, float top, float width, float height)
        {
            if (target == null)
            {
                return;
            }

            var rect = target.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetTopRectColumnRuntime(
            Transform target,
            float left,
            float right,
            float top,
            float height,
            float start,
            float end,
            float gap)
        {
            if (target == null)
            {
                return;
            }

            var rect = target.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            start = Mathf.Clamp01(start);
            end = Mathf.Clamp01(end);
            if (end < start)
            {
                var swap = start;
                start = end;
                end = swap;
            }

            var leftGap = start > 0f ? gap * 0.5f : 0f;
            var rightGap = end < 1f ? gap * 0.5f : 0f;
            rect.anchorMin = new Vector2(start, 1f);
            rect.anchorMax = new Vector2(end, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left * (1f - start) - right * start + leftGap, -top - height);
            rect.offsetMax = new Vector2(left * (1f - end) - right * end - rightGap, -top);
        }

        private static void SetAnchorsRuntime(Transform target, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (target == null)
            {
                return;
            }

            var rect = target.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void StartMicrophoneMonitor()
        {
            StopMicrophoneMonitor();
            var device = MicrophoneValue();
            if (string.IsNullOrWhiteSpace(device) || device == "Default")
            {
                var devices = Microphone.devices;
                device = devices != null && devices.Length > 0 ? devices[0] : null;
            }

            if (string.IsNullOrWhiteSpace(device))
            {
                SetMicrophoneTestStatus("Mic Test: no microphone");
                SetMicrophoneTestLevel(0f);
                return;
            }

            Microphone.GetDeviceCaps(device, out var minFrequency, out var maxFrequency);
            microphoneTestFrequency = minFrequency == 0 && maxFrequency == 0
                ? 44100
                : Mathf.Clamp(44100, minFrequency, maxFrequency);
            microphoneTestDevice = device;

            try
            {
                microphoneTestClip = Microphone.Start(microphoneTestDevice, true, 5, microphoneTestFrequency);
                microphoneTestStartedAt = Time.realtimeSinceStartup;
                SetMicrophoneTestStatus($"Mic Test: listening ({microphoneTestDevice})");
                Debug.Log($"Yui mic test monitor: device='{microphoneTestDevice}', frequency={microphoneTestFrequency}");
            }
            catch (System.Exception ex)
            {
                microphoneTestClip = null;
                microphoneTestDevice = null;
                microphoneTestStartedAt = -1f;
                SetMicrophoneTestStatus("Mic Test: failed");
                Debug.LogWarning($"Yui mic test monitor failed: {ex.Message}");
            }
        }

        private void StopMicrophoneMonitor()
        {
            if (!string.IsNullOrEmpty(microphoneTestDevice) && Microphone.IsRecording(microphoneTestDevice))
            {
                Microphone.End(microphoneTestDevice);
            }

            microphoneTestClip = null;
            microphoneTestDevice = null;
            microphoneTestStartedAt = -1f;
            SetMicrophoneTestLevel(0f);
        }

        private void UpdateMicrophoneMonitor()
        {
            if (microphoneTestClip == null || string.IsNullOrEmpty(microphoneTestDevice))
            {
                return;
            }

            if (Time.realtimeSinceStartup - microphoneTestStartedAt > 8f)
            {
                StopMicrophoneMonitor();
                SetMicrophoneTestStatus("Mic Test: complete");
                return;
            }

            var position = Microphone.GetPosition(microphoneTestDevice);
            if (position <= microphoneTestSamples.Length)
            {
                SetMicrophoneTestLevel(0f);
                return;
            }

            microphoneTestClip.GetData(microphoneTestSamples, position - microphoneTestSamples.Length);
            var sum = 0f;
            for (var i = 0; i < microphoneTestSamples.Length; i++)
            {
                sum += microphoneTestSamples[i] * microphoneTestSamples[i];
            }

            var rms = Mathf.Sqrt(sum / microphoneTestSamples.Length);
            var level = rms > 0.005f ? Mathf.Max(0.05f, rms * 32f) : 0f;
            SetMicrophoneTestLevel(Mathf.Clamp01(level));
        }

        private void SetMicrophoneTestLevel(float level)
        {
            if (microphoneTestLevelFill == null)
            {
                return;
            }

            level = Mathf.Clamp01(level);
            var rect = microphoneTestLevelFill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(level, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetMicrophoneTestStatus(string text)
        {
            if (microphoneTestStatusText != null)
            {
                microphoneTestStatusText.text = text;
            }
        }

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
                resolutionPresetOnOpen = windowResolutionController.PresetIndex;
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
                : new[]
                {
                    "Stable",
                    "Realtime Voice (Experimental)",
                    "Realtime VOICEVOX (Experimental)",
                    "Realtime Translate (Experimental)"
                };
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
                return;
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
            if (string.Equals(mode, "realtime_voice", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "voice", System.StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(mode, "realtime_translate", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "translate", System.StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (string.Equals(mode, "realtime_voicevox", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "voice_text", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "voicevox", System.StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return 0;
        }

        private string ConversationModeValue()
        {
            if (conversationModeDropdown == null)
            {
                return "stable";
            }

            switch (conversationModeDropdown.value)
            {
                case 1:
                    return "realtime_voice";
                case 2:
                    return "realtime_voicevox";
                case 3:
                    return "realtime_translate";
                default:
                    return "stable";
            }
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


