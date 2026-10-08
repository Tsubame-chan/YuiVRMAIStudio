using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiSettingsOverlay
    {
        public void RefreshVoicePackUi()
        {
            if(settingsRoot == null || !settingsRoot.activeSelf)return;
            RefreshTtsModeOptions(TtsModeValue());
            ApplyResponsiveOverlayLayout();
        }
        private static readonly Color SettingsSurface = new Color32(28, 27, 32, 255);
        private static readonly Color SettingsField = new Color32(43, 41, 49, 255);
        private static readonly Color SettingsAccent = new Color32(208, 188, 255, 255);
        private static readonly Color SettingsText = new Color32(235, 230, 240, 255);
        private static readonly Color SettingsMuted = new Color32(185, 180, 194, 255);
        private int settingsPage;
        private readonly List<string> visibleTtsModes = new List<string>();
        private Dropdown advancedModeDropdown;
        private Dropdown languageDropdown;
        private bool renderingSettings;
        private static Sprite roundedSettingsSprite;

        private void SelectSettingsPage(int page)
        {
            settingsPage = page;
            StopMicrophoneMonitor();
            ApplyResponsiveOverlayLayout();
            var scroll = settingsRoot.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null) scroll.verticalNormalizedPosition = 1;
        }

        private void RenderSettingsPage(Transform content)
        {
            if (renderingSettings) return;
            CloseSettingsHelp();
            renderingSettings = true;
            var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            try
            {
                var panel = settingsRoot.transform.Find("Panel");
                if (panel == null) return;
                // Reuse the existing controls and their handlers. Only their layout changes.
                foreach (var name in new[] { "BackendLabel", "BackendInput", "OpenAiApiKeyLabel", "OpenAiApiKeyInput",
                    "OpenAiModelLabel", "OpenAiModelInput", "AutoAiFallbackLabel", "AutoAiFallbackToggle" })
                {
                    var control = UiTreeUtility.FindDeepChild(settingsRoot.transform, name);
                    if (control != null) control.SetParent(content, false);
                }
                if (advancedRoot != null) advancedRoot.SetActive(false);
                if (advancedButton != null) advancedButton.gameObject.SetActive(false);
                foreach (Transform child in content) child.gameObject.SetActive(false);
                SetLabelTextRuntime(panel.Find("Title"), "Settings");
                SetAnchorsRuntime(panel.Find("Title"), new Vector2(.05f,.925f), new Vector2(.48f,.98f));
                SetAnchorsRuntime(applyButton.transform, new Vector2(.68f,.92f), new Vector2(.86f,.98f));
                SetButtonCaption(applyButton, YuiSimpleDialog.L("設定を保存", "Save settings"));
                SetAnchorsRuntime(closeButton.transform, new Vector2(.88f,.92f), new Vector2(.96f,.98f));
                SetButtonCaption(closeButton, "×");
                var tabs = new[] { "AI", "Voice", "Character", "Display", "Advanced" };
                for (var i = 0; i < tabs.Length; i++)
                {
                    var page = i;
                    var button = ModernButton(panel, "SettingsTab" + i, tabs[i], () => SelectSettingsPage(page));
                    SetAnchorsRuntime(button.transform, new Vector2(.04f + i * .184f,.842f), new Vector2(.216f + i * .184f,.9f));
                    button.GetComponent<Image>().color = i == settingsPage ? new Color32(70,59,88,255) : SettingsSurface;
                }
                var scroll = panel.Find("SettingsScroll");
                SetAnchorsRuntime(scroll, new Vector2(.035f,.035f), new Vector2(.965f,.81f));
                if (scroll != null && scroll.TryGetComponent<Image>(out var scrollImage)) scrollImage.color = SettingsSurface;
                var row = 22f;
                if (settingsPage == 0)
                {
                    Heading(content, "Choose how Yui thinks", ref row);
                    var modes = new[] { YuiConversationModes.Stable, YuiConversationModes.LocalAi, YuiConversationModes.DirectOpenAi };
                    var titles = new[] { "Automatic", "On this device", "OpenAI API" };
                    var details = new[] { "Configured backend → OpenAI API → on-device AI", "Use downloaded models on this device", "Voice, text and images — no PC required" };
                    for (var i = 0; i < modes.Length; i++)
                    {
                        var mode = modes[i];
                        var button = ModernButton(content, "AiChoice" + i, titles[i] + "\n" + details[i], () =>
                        { SelectConversationMode(mode); RenderSettingsPage(content); });
                        var text = button.GetComponentInChildren<Text>();
                        text.alignment = TextAnchor.MiddleLeft;
                        text.fontSize = YuiUiTypography.Body;
                        var rect = text.rectTransform; rect.offsetMin = new Vector2(24,6); rect.offsetMax = new Vector2(-76,-6);
                        Place(button.transform, row, 96); row += 110;
                        button.GetComponent<Image>().color = ConversationModeValue() == mode ? new Color32(70,59,88,255) : SettingsField;
                        YuiControlAffordance.SelectionMark(button,ConversationModeValue() == mode);
                    }
                    var snapshot = chatPanel != null ? chatPanel.CurrentCapabilitySnapshot() : null;
                    Note(content, snapshot?.Conversation(ConversationModeValue()).Detail ?? "Choose a connection to get started.", ref row);
                    var modelLabel = YuiSimpleDialog.L("端末内AI · 2B / 4Bを選ぶ", "On-device AI · Choose 2B / 4B");
                    ModernButton(content, "LocalModelMenu", modelLabel, () => YuiLocalModelMenu.Show(chatPanel));
                    Full(content, "LocalModelMenu", modelLabel, ref row);
                    var policyLabel=YuiSimpleDialog.L("回答の方針 · API/端末内AI共通", "Response style · API and on-device AI");
                    ModernButton(content,"ResponsePolicy",policyLabel,()=> {
                        var draft=PlayerPrefs.GetString(YuiPrefsKeys.ResponseInstruction,"");
                        var policy=YuiSimpleDialog.Create(policyLabel,YuiSimpleDialog.L("例: 結論を先に、必要な説明は箇条書きで。\nキャラクターの性格・口調とは別に保存し、APIと端末内AIの両方へ伝えます。", "Example: Start with the conclusion, then use bullets for detail. Saved separately from character personality and used by both API and on-device AI."));
                        var input=policy.AddInput(YuiSimpleDialog.L("回答の方針（1200文字まで）", "Response instructions (up to 1200 characters)"),draft,v=>draft=v);
                        input.characterLimit=1200;
                        policy.AddButton(YuiSimpleDialog.L("保存", "Save"),()=>{PlayerPrefs.SetString(YuiPrefsKeys.ResponseInstruction,draft);PlayerPrefs.Save();policy.Close();});
                        policy.AddButton(YuiSimpleDialog.L("変更せず閉じる", "Close without saving"),()=>policy.Close());
                        policy.Compact(600);
                    });
                    Full(content,"ResponsePolicy",policyLabel,ref row);
                    SettingsHelpRow(content, "OpenAiApiKeyLabel", "OpenAI API key", "OpenAiApiKeyInput", "ApiKeyHelp", ref row);
                    Note(content, "Used by this app only. Backend credentials stay separate.", ref row);
                    if (!string.IsNullOrEmpty(YuiApiKeyStore.LastError) || YuiApiKeyStore.IsRecovering)
                    {
                        Note(content, "The saved API key is locked. Unlock it to use OpenAI. Other features remain available.", ref row);
                        var recover = ModernButton(content, "RecoverApiKey", "Unlock saved API key", async () => {
                            if (chatPanel != null && await chatPanel.RecoverSavedApiKeyAsync() && openAiApiKeyInput != null)
                                openAiApiKeyInput.text = chatPanel.OpenAiApiKey;
                            if (this != null && content != null) RenderSettingsPage(content);
                        });
                        recover.interactable = !YuiApiKeyStore.IsRecovering;
                        Place(recover.transform, row, 58); row += 72;
                    }
                    SettingsHelpRow(content, "OpenAiModelLabel", "API model ID", "OpenAiModelInput", "ApiModelHelp", ref row);
                    Note(content, string.Format("Input example: {0} — include the entire ID and its hyphens. You can keep the default.", Api.YuiDirectOpenAiClient.DefaultModel), ref row);
                    Row(content, "MicrophoneLabel", "Microphone", "MicrophoneDropdown", ref row);
                    Full(content, "MicrophoneTestButton", "Test microphone", ref row);
                    ShowAt(content, "MicrophoneTestMeter", row, 18); row += 24;
                    ShowAt(content, "MicrophoneTestStatus", row, 36); row += 48;
                }
                else if (settingsPage == 1)
                {
                    Heading(content, "A voice for your character", ref row);
                    var mode = ConversationModeValue();
                    if (!YuiConversationModes.IsRealtime(mode))
                    {
                        var label=YuiSimpleDialog.L("Backendの保存した声を選ぶ", "Choose a saved Backend voice");
                        ModernButton(content,"BackendSavedVoice",label,()=>chatPanel?.ChooseBackendVoiceProfile());
                        Full(content,"BackendSavedVoice",label,ref row);
                        if(TtsModeValue()=="backend-profile")Note(content,"Voice parameters are managed in Backend Console. App volume still applies.",ref row);
                    }
                    if (YuiConversationModes.IsRealtime(mode))
                        Note(content, "This Realtime mode uses its own voice engine. Change the mode in Advanced.", ref row);
                    else Row(content, "TtsModeLabel", "Voice engine", "TtsModeDropdown", ref row);
                    if (Application.isMobilePlatform && TtsModeValue() == "aivis-native")
                        Note(content, "Experimental on-device voice. Speech can take several seconds to start. VOICEVOX is recommended for faster conversation.", ref row);
                    if (YuiConversationModes.IsRealtime(mode) || TtsModeValue() != "silent") SliderRow(content, "Volume", "Volume", ref row);
                    if ((!YuiConversationModes.IsRealtime(mode) || YuiConversationModes.IsRealtimeTextTts(mode)) && TtsModeValue() != "silent" && TtsModeValue() != "backend-profile")
                    {
                        if (YuiPhysicalAI.LocalAI.YuiSpeechLanguage.UsesKokoro(YuiUiLocalization.Language, TtsModeValue()))
                        {
                            Note(content, "Kokoro follows Language and works offline.", ref row);
                            var englishVoiceLabel = "English voice: " + (chatPanel?.EnglishVoiceName ?? "Bella") + " · Switch";
                            if (chatPanel != null && YuiPhysicalAI.LocalAI.YuiKokoroSpeech.InstalledVoices(Application.persistentDataPath).Length > 0)
                            {
                            ModernButton(content, "EnglishVoiceChoice", englishVoiceLabel, () => { chatPanel?.ToggleEnglishVoice(); ApplyResponsiveOverlayLayout(); });
                            Full(content, "EnglishVoiceChoice", englishVoiceLabel, ref row);
                            }
                            var downloadLabel = chatPanel != null && chatPanel.EnglishVoiceInstalled ? "English voice data · Ready / Repair" : "Download English voice pack";
                            ModernButton(content, "EnglishVoiceDownload", downloadLabel, () => chatPanel?.DownloadEnglishVoice());
                            Full(content, "EnglishVoiceDownload", downloadLabel, ref row);
                            EnglishVoiceSliders(content, ref row);
                            Full(content, "VoicePreviewButton", "Preview voice", ref row);
                        }
                        else
                        {
                        if (TtsModeValue() == "irodori-native")
                        {
                            Row(content, "SpeakerLabel", "Voice", "SpeakerDropdown", ref row);
                            Full(content, "VoicePreviewButton", "Preview voice", ref row);
                        }
                        else {
                        if (YuiTtsRuntimeRouting.IsVoicevoxIntent(TtsModeValue()) || IsAivisTtsSelected())
                            Row(content, "SpeakerLabel", "Voice", "SpeakerDropdown", ref row);
                        Full(content, "VoicePreviewButton", "Preview voice", ref row);
                        SliderRow(content, "Speed", "Speed", ref row);
                        SliderRow(content, "Pitch", "Pitch", ref row);
                        if (YuiTtsRuntimeRouting.IsVoicevoxIntent(TtsModeValue()) || IsAivisTtsSelected()) SliderRow(content, "Intonation", "Expression", ref row);
                        Row(content, "VoicePresetLabel", "Saved voice", "VoicePresetDropdown", ref row);
                        Row(content, "VoicePresetNameLabel", "Preset name", "VoicePresetNameInput", ref row);
                        Full(content, "VoicePresetSaveButton", "Save voice preset", ref row);
                        Full(content, "VoicePresetDeleteButton", "Delete voice preset", ref row);
                        }
                        }
                    }
                    if (chatPanel != null && chatPanel.VoicePackBusy)
                    {
                        ModernButton(content, "VoicePackProgress", YuiSimpleDialog.L("音声ダウンロードの進捗", "Voice download progress"), () => chatPanel.ShowVoicePackDownload(false));
                        Full(content, "VoicePackProgress", YuiSimpleDialog.L("音声ダウンロードの進捗", "Voice download progress"), ref row);
                    }
                    if (!YuiConversationModes.IsRealtime(mode) && TtsModeValue() == "server-http")
                    {
                        Row(content, "IrodoriVoiceGenderLabel", "Voice base", "IrodoriVoiceGenderDropdown", ref row);
                        Row(content, "IrodoriVoiceInstructLabel", "Voice direction", "IrodoriVoiceInstructInput", ref row, 80);
                    }
                }
                else if (settingsPage == 2)
                {
                    Heading(content, "Your character", ref row);
                    ModernButton(content, "CharacterLibraryButton", "My characters", () => { ApplyFieldsToRuntime(false); chatPanel?.OpenCharacterLibrary(); });
                    Full(content, "CharacterLibraryButton", "My characters", ref row);
                    Note(content, YuiSimpleDialog.L("キャラクターを選ぶと、その場で切り替わります。読み込み中は表示が出ます。", "Selecting a character switches it immediately. A loading indicator appears while it loads."), ref row);
                    Full(content, "CustomVrmImportButton", "Import avatar", ref row);
                    ModernButton(content, "AvatarGuideButton", "Avatar guide", () => { ApplyFieldsToRuntime(false); chatPanel?.OpenAvatarGuide(); });
                    Full(content, "AvatarGuideButton", "Avatar guide", ref row);
                    if (YuiAvatarSlots.IsCustomVrm(chatPanel?.AvatarSlot))
                        Row(content, "CustomVrmNameLabel", "Appearance name", "CustomVrmNameInput", ref row);
                    Row(content, "CharacterNameLabel", "Character name", "CharacterNameInput", ref row);
                    Row(content, "CustomInstructionLabel", YuiSimpleDialog.L("キャラクターの性格・口調", "Character personality / Tone"), "CustomInstructionInput", ref row, 180);
                    var personalityField=content.Find("CustomInstructionInput")?.GetComponent<InputField>();
                    if(personalityField?.placeholder is Text personalityHint)
                        YuiUiLocalization.Set(personalityHint,YuiSimpleDialog.L("話し方・役柄・性格を自由に指定", "Describe tone, role and personality"));
                    Note(content,YuiSimpleDialog.L("口調・役柄・性格を指定できます。APIと端末内AIの両方に伝えます。回答形式はAIタブの「回答の方針」で設定できます。", "Describe personality, tone and role. Used by both API and on-device AI. Set response format under Response style in the AI tab."),ref row);
                    var viewer = FindObjectOfType<YuiConsoleVisibilityController>();
                    var viewerLabel = "鑑賞操作: " + (viewer != null && viewer.ViewerRotatesAvatar ? "本体回転" : "カメラ周回");
                    ModernButton(content,"ViewerModeSettings",viewerLabel,()=> {
                        if(viewer!=null)viewer.SetViewerRotatesAvatar(!viewer.ViewerRotatesAvatar);
                        SetButtonCaption(content.Find("ViewerModeSettings")?.GetComponent<Button>(), "鑑賞操作: " + (viewer!=null && viewer.ViewerRotatesAvatar ? "本体回転" : "カメラ周回"));
                    });
                    Full(content,"ViewerModeSettings",viewerLabel,ref row);
                    Row(content, "CameraPresetLabel", "Camera view", "CameraPresetDropdown", ref row);
                    Full(content, "CameraAdjustButton", "Adjust view", ref row);
                    Full(content, "CameraAutoButton", "Auto frame", ref row);
                    Full(content, "CameraSaveButton", "Save view", ref row);
                    Full(content, "CameraDeleteButton", "Delete saved view", ref row);
                }
                else if (settingsPage == 3)
                {
                    Heading(content, "Your space", ref row);
                    EnsureLanguageControl(content);
                    Row(content, "UiLanguageLabel", "Language", "UiLanguageDropdown", ref row);
                    Row(content, "BackgroundLabel", "Background", "BackgroundDropdown", ref row);
                    if (!Application.isMobilePlatform)
                        Row(content, "ResolutionLabel", "Window size", "ResolutionDropdown", ref row);
                    if (!Application.isMobilePlatform)
                        Row(content, "LookCameraLabel", "Image camera", "LookCameraDropdown", ref row);
                }
                else
                {
                    BackendHelpHeading(content, ref row);
                    Note(content, "Optional PC/server connection. No setup is needed for on-device AI, VOICEVOX or direct OpenAI access.", ref row);
                    if (advancedModeDropdown == null)
                    {
                        advancedModeDropdown = Instantiate(conversationModeDropdown, content);
                        advancedModeDropdown.name = "AdvancedConversationChoice";
                        advancedModeDropdown.onValueChanged = new Dropdown.DropdownEvent();
                        advancedModeDropdown.ClearOptions();
                        advancedModeDropdown.AddOptions(new List<string> { "Keep current AI mode", "Backend conversation", "Realtime · OpenAI voice", "Realtime · VOICEVOX", "Realtime · AivisSpeech", "Realtime · Translation" });
                        advancedModeDropdown.onValueChanged.AddListener(index =>
                        { if (index > 0) { SelectConversationMode(YuiConversationModes.FromDropdownIndex(index + 1)); RenderSettingsPage(content); } });
                    }
                    var modeIndex = YuiConversationModes.DropdownIndex(ConversationModeValue());
                    advancedModeDropdown.SetValueWithoutNotify(modeIndex >= 2 && modeIndex <= 6 ? modeIndex - 1 : 0);
                    Place(advancedModeDropdown.transform, row, 88); row += 104;
                    Note(content, chatPanel?.CurrentCapabilitySnapshot().Conversation(ConversationModeValue()).Detail ?? "", ref row);
                    SettingsHelpRow(content, "BackendLabel", "Backend URL", "BackendInput", "BackendAddressHelp", ref row);
                    Note(content, Application.isMobilePlatform
                        ? YuiSimpleDialog.L("接続するPCのアドレスを入力します。Backendを使わない場合は空欄で構いません。", "Enter the address of the PC to connect to. Leave blank if you do not use Backend.")
                        : YuiSimpleDialog.L("このPCのBackendには既定のアドレスで接続できます。別のPCへの接続方法は「？」へ。", "The default address connects to Backend on this PC. See ? to connect to another PC."), ref row);
                    Note(content, "AI and voice services for this connection are configured on the PC or server. This app's OpenAI API key is not shared with it.", ref row);
                    Row(content, "AutoAiFallbackLabel", "If a request fails", "AutoAiFallbackToggle", ref row, 116);
                    if (Application.isMobilePlatform)
                    {
                        Heading(content, "On-device data", ref row);
                        Note(content, YuiPhysicalAI.LocalAI.YuiAppleHostedAssets.Enabled
                            ? "Conversation data is delivered by Apple during setup. Once ready, you can chat offline. Standard voice data is included in the app."
                            : YuiSimpleDialog.L("標準のAI・音声データはアプリに含まれています。追加のAIはAIタブ、追加の音声は音声タブから、容量を確認してダウンロードできます。", "Standard AI and voice data are included. Download optional AI in the AI tab and optional voices in the Voice tab after reviewing their size."), ref row);
                    }
                    else
                    {
                        Heading(content, "Downloads", ref row);
                        ShowAt(content, "LocalAiAssetStatusText", row, 68); row += 80;
                        Full(content, "LocalAiAssetRepairButton", "Manage on-device models", ref row);
                        Full(content, "OptionalTtsDownloadButton", "Download additional voices", ref row);
                    }
                    Heading(content, "Conversation data", ref row);
                    ModernButton(content, "DeviceSyncButton", "Sync character & conversations", () => chatPanel?.OpenDeviceSync());
                    Full(content, "DeviceSyncButton", "Sync character & conversations", ref row);
                    ModernButton(content, "BackendMemoryButton", "Manage character memories", () => chatPanel?.OpenCharacterMemories());
                    Full(content, "BackendMemoryButton", "Manage character memories", ref row);
                    Full(content, "ClearHistoryButton", "Clear backend conversations & memories", ref row);
                    Note(content, "On-device history is managed from History in the console.", ref row);
                }
                content.GetComponent<RectTransform>().sizeDelta = new Vector2(0, row + 24);
                LocalizeSettingsOptions();
                YuiUiLocalization.BindKnownLabels(panel);
                StyleSettingsControls(panel);
                YuiToolbarIconUtility.ApplyCloseIcon(closeButton);
                YuiControlAffordance.Scrollbar(scroll != null ? scroll.GetComponent<ScrollRect>() : null);
                ConfigureSettingsNavigation(panel, content);
            }
            finally
            {
                renderingSettings = false;
                if (selected != null && selected.activeInHierarchy && UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(selected);
            }
        }

        private void SelectConversationMode(string mode)
        {
            conversationModeDropdown.SetValueWithoutNotify(YuiConversationModes.DropdownIndex(mode));
            if (!YuiConversationModes.IsRealtimeTextTts(mode)) return;
            var voice = TtsModeForConversationMode(mode, TtsModeValue());
            if (!visibleTtsModes.Contains(voice))
            {
                visibleTtsModes.Add(voice);
                ttsModeDropdown.options.Add(new Dropdown.OptionData("AivisSpeech · Setup required"));
            }
            ttsModeDropdown.value = visibleTtsModes.IndexOf(voice);
            ttsModeDropdown.RefreshShownValue();
        }

        private void ConfigureSettingsNavigation(Transform panel, Transform content)
        {
            // Unity's automatic navigation can select the conversation controls
            // behind this modal. Keep keyboard/controller focus inside Settings.
            var fields = new List<Selectable>(content.GetComponentsInChildren<Selectable>(false));
            fields.RemoveAll(field => !field.IsInteractable() || field is Scrollbar);
            fields.Sort((left, right) => right.transform.position.y.CompareTo(left.transform.position.y));
            var tab = panel.Find("SettingsTab" + settingsPage)?.GetComponent<Button>();
            for (var i = 0; i < fields.Count; i++)
            {
                if(fields[i].GetComponent<YuiScrollIntoView>()==null) fields[i].gameObject.AddComponent<YuiScrollIntoView>();
                fields[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = i == 0 ? tab : fields[i - 1],
                    selectOnDown = i + 1 < fields.Count ? fields[i + 1] : tab };
            }
            for (var i = 0; i < 5; i++)
            {
                var button = panel.Find("SettingsTab" + i)?.GetComponent<Button>();
                if (button == null) continue;
                button.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = panel.Find("SettingsTab" + ((i + 4) % 5))?.GetComponent<Button>(),
                    selectOnRight = panel.Find("SettingsTab" + ((i + 1) % 5))?.GetComponent<Button>(),
                    selectOnUp = applyButton, selectOnDown = fields.Count > 0 ? fields[0] : applyButton };
            }
            applyButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnRight = closeButton, selectOnDown = tab, selectOnUp = tab };
            closeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = applyButton, selectOnDown = tab, selectOnUp = tab };
        }

        private static void RoundSurface(Image image)
        {
            if (image == null) return;
            if (roundedSettingsSprite == null)
            {
                // Editor built-in UI sprites are not available in every Player.
                const int size = 64; const float radius = 14f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "Settings rounded surface"; texture.hideFlags = HideFlags.HideAndDontSave;
                texture.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[size * size];
                for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius), 0);
                    var dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius), 0);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy)));
                }
                texture.SetPixels(pixels); texture.Apply(false, true);
                roundedSettingsSprite = Sprite.Create(texture, new Rect(0,0,size,size), new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(radius,radius,radius,radius));
                roundedSettingsSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            image.sprite = roundedSettingsSprite;
            image.type = Image.Type.Sliced;
        }

        private static void Place(Transform target, float top, float height)
        { if (target == null) return; target.gameObject.SetActive(true); SetTopRectRuntime(target, 24, top, 24, height); }
        private static void ShowAt(Transform root, string name, float top, float height) => Place(root.Find(name), top, height);
        private static void SetButtonCaption(Button button, string caption)
        { if (button != null && button.GetComponentInChildren<Text>(true) is Text label) YuiUiLocalization.Set(label, caption); }
        private static void Full(Transform root, string name, string caption, ref float row)
        { var control = root.Find(name); Place(control, row, 88); if (control != null) SetButtonCaption(control.GetComponent<Button>(), caption); row += 104; }
        private static void Row(Transform root, string labelName, string label, string control, ref float row, float height = 88)
        {
            var name = root.Find(labelName); if (name != null) { name.gameObject.SetActive(true); SetLabelTextRuntime(name, label); SetTopRectRuntime(name,24,row,24,52); }
            row += 58; ShowAt(root, control, row, height); row += height + 24;
        }
        private static void SliderRow(Transform root, string prefix, string label, ref float row)
        {
            var top = row;
            Row(root, prefix + "Label", label, prefix + "Slider", ref row, 44);
            var value = root.Find(prefix + "Value"); if (value != null) { value.gameObject.SetActive(true); SetTopRightRectRuntime(value,24,top,120,52); }
        }
        private static void Heading(Transform root, string text, ref float row)
        { var label = ModernText(root, "Heading" + row, text, YuiUiTypography.Heading); Place(label.transform,row,66); row += 82; }
        private static void Note(Transform root, string text, ref float row)
        {
            var label = ModernText(root, "Note" + row, text, YuiUiTypography.Note);
            var width = Mathf.Max(400, ((RectTransform)root).rect.width - 48);
            var height = Mathf.Max(76, label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text,
                label.GetGenerationSettings(new Vector2(width, 0))) / label.pixelsPerUnit + 18);
            Place(label.transform,row,height); label.color = SettingsMuted; row += height + 14;
        }
        private static Text ModernText(Transform root, string name, string text, int size)
        {
            var t = root.Find(name)?.GetComponent<Text>();
            if (t == null) { var go = new GameObject(name,typeof(RectTransform),typeof(Text)); go.transform.SetParent(root,false); t=go.GetComponent<Text>(); t.font=BuiltinUiFont(); t.raycastTarget=false; }
            YuiUiLocalization.Set(t,text,name.StartsWith("Note",StringComparison.Ordinal)); t.fontSize=size; t.alignment=TextAnchor.MiddleLeft; t.horizontalOverflow=HorizontalWrapMode.Wrap; t.verticalOverflow=VerticalWrapMode.Truncate; return t;
        }
        private static Button ModernButton(Transform root, string name, string caption, UnityEngine.Events.UnityAction action)
        {
            var button=root.Find(name)?.GetComponent<Button>();
            if (button==null)
            {
                var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(root,false);
                button=go.GetComponent<Button>(); button.targetGraphic=go.GetComponent<Image>(); button.onClick.AddListener(action);
                var label=ModernText(go.transform,"Label",caption,YuiUiTypography.Button); SetAnchorsRuntime(label.transform,Vector2.zero,Vector2.one); label.alignment=TextAnchor.MiddleCenter;
            }
            SetButtonCaption(button,caption); button.gameObject.SetActive(true); return button;
        }
        private static void StyleSliderTrack(RectTransform track, Color color)
        {
            if (track == null) return;
            track.anchorMin = new Vector2(track.anchorMin.x,.5f);
            track.anchorMax = new Vector2(track.anchorMax.x,.5f);
            track.sizeDelta = new Vector2(track.sizeDelta.x,10);
            track.anchoredPosition = new Vector2(track.anchoredPosition.x,0);
            if (track.TryGetComponent<Image>(out var image)) { image.color=color; RoundSurface(image); }
        }

        private static void StyleSettingsControls(Transform panel)
        {
            var background=panel.GetComponent<Image>(); if(background!=null) background.color=SettingsSurface;
            var backing=panel.Find("OpaqueBacking")?.GetComponent<Image>(); if(backing!=null) backing.color=SettingsSurface;
            foreach(var label in panel.GetComponentsInChildren<Text>(true))
            {
                label.font = YuiUiTypography.Regular;
                label.resizeTextForBestFit = false;
                label.color=label.name.StartsWith("Note",StringComparison.Ordinal) ? SettingsMuted : SettingsText;
                if (label.GetComponentInParent<Button>()?.name.StartsWith("SettingsTab",StringComparison.Ordinal) == true) label.fontSize=YuiUiTypography.Tab;
                else if(!label.name.StartsWith("Heading",StringComparison.Ordinal) && !label.name.StartsWith("Note",StringComparison.Ordinal)) label.fontSize=label.name == "Title" ? YuiUiTypography.Title : YuiUiTypography.Label;
            }
            foreach(var control in panel.GetComponentsInChildren<Selectable>(true))
            {
                if (control.targetGraphic is Image surface && !(control is Slider)) RoundSurface(surface);
                var colors=control.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(.88f,.83f,1); colors.selectedColor=new Color(.82f,.74f,1); colors.pressedColor=new Color(.74f,.66f,.9f); control.colors=colors;
                if(control is InputField input)
                {
                    YuiControlAffordance.Input(input);
                    if(input.textComponent!=null) { input.textComponent.color=SettingsText; input.textComponent.fontSize=YuiUiTypography.Body; }
                    if(input.placeholder is Text placeholder) placeholder.color=SettingsMuted;
                }
                else if(control is Dropdown dropdown)
                {
                    if(dropdown.targetGraphic is Image image) image.color=SettingsField;
                    PrepareDropdownTemplateRuntime(dropdown,432,72);
                    YuiControlAffordance.DropdownArrow(dropdown);
                    if(dropdown.template!=null && dropdown.template.TryGetComponent<Image>(out var popup)) YuiUiTheme.SurfaceOn(popup,SettingsField);
                }
                else if(control is Toggle toggle && toggle.name == "AutoAiFallbackToggle")
                {
                    var view=toggle.GetComponent<YuiSettingsSwitch>() ?? toggle.gameObject.AddComponent<YuiSettingsSwitch>();
                    view.Configure();
                }
                else if(control is Toggle item && item.GetComponentInParent<Dropdown>(true)!=null)
                {
                    if(item.targetGraphic is Image itemBackground) itemBackground.color=Color.white;
                    var itemColors=item.colors;itemColors.normalColor=SettingsField;itemColors.highlightedColor=YuiUiTheme.Selected;
                    itemColors.selectedColor=YuiUiTheme.Selected;itemColors.pressedColor=new Color32(86,74,104,255);item.colors=itemColors;
                    if(item.graphic is Image check) {check.sprite=YuiToolbarIconUtility.LoadSymbol("check");check.color=SettingsAccent;check.preserveAspect=true;}
                }
                else if(control is Slider slider)
                {
                    var track = slider.transform.Find("Background") as RectTransform;
                    StyleSliderTrack(track, SettingsField);
                    StyleSliderTrack(slider.fillRect, SettingsAccent);
                    if (slider.handleRect != null)
                    {
                        var handle = slider.handleRect;
                        handle.anchorMin = new Vector2(handle.anchorMin.x, .5f);
                        handle.anchorMax = new Vector2(handle.anchorMax.x, .5f);
                        handle.sizeDelta = new Vector2(30,30);
                        handle.anchoredPosition = new Vector2(handle.anchoredPosition.x,0);
                        if (handle.TryGetComponent<Image>(out var knob)) { knob.color = SettingsAccent; RoundSurface(knob); }
                    }
                }
                else if(control is Button button && !button.name.StartsWith("AiChoice") && !button.name.StartsWith("SettingsTab"))
                    {
                    var primary=button.name=="ApplyButton" || button.name=="VoicePresetSaveButton";
                    if(button.targetGraphic is Image image) image.color=primary ? SettingsAccent : new Color32(62,54,76,255);
                    var text=button.GetComponentInChildren<Text>(true);
                    if(text!=null) text.color=primary ? new Color32(45,32,65,255) : SettingsText;
                    if(button.name.Contains("Delete") || button.name=="ClearHistoryButton" || button.name=="CustomVrmClearButton")
                    { if(button.targetGraphic is Image deleteImage) deleteImage.color=SettingsSurface; if(text!=null) text.color=new Color32(255,180,171,255); }
                }
            }
        }
    }
}
