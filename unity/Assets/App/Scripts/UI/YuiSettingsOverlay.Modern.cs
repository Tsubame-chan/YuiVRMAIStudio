using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiSettingsOverlay
    {
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
                SetButtonCaption(applyButton, "Save");
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
                    Row(content, "OpenAiApiKeyLabel", "OpenAI API key", "OpenAiApiKeyInput", ref row);
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
                    Row(content, "OpenAiModelLabel", "Model", "OpenAiModelInput", ref row);
                    Row(content, "MicrophoneLabel", "Microphone", "MicrophoneDropdown", ref row);
                    Full(content, "MicrophoneTestButton", "Test microphone", ref row);
                    ShowAt(content, "MicrophoneTestMeter", row, 18); row += 24;
                    ShowAt(content, "MicrophoneTestStatus", row, 36); row += 48;
                }
                else if (settingsPage == 1)
                {
                    Heading(content, "A voice for your character", ref row);
                    var mode = ConversationModeValue();
                    if (YuiConversationModes.IsRealtime(mode))
                        Note(content, "This Realtime mode uses its own voice engine. Change the mode in Advanced.", ref row);
                    else Row(content, "TtsModeLabel", "Voice engine", "TtsModeDropdown", ref row);
                    if (YuiConversationModes.IsRealtime(mode) || TtsModeValue() != "silent") SliderRow(content, "Volume", "Volume", ref row);
                    if ((!YuiConversationModes.IsRealtime(mode) || YuiConversationModes.IsRealtimeTextTts(mode)) && TtsModeValue() != "silent")
                    {
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
                    Full(content, "CustomVrmImportButton", "Import avatar", ref row);
                    ModernButton(content, "AvatarGuideButton", "Avatar guide", () => { ApplyFieldsToRuntime(false); chatPanel?.OpenAvatarGuide(); });
                    Full(content, "AvatarGuideButton", "Avatar guide", ref row);
                    if (YuiAvatarSlots.IsCustomVrm(chatPanel?.AvatarSlot))
                        Row(content, "CustomVrmNameLabel", "Appearance name", "CustomVrmNameInput", ref row);
                    Row(content, "CharacterNameLabel", "Character name", "CharacterNameInput", ref row);
                    Row(content, "CustomInstructionLabel", "Personality", "CustomInstructionInput", ref row, 180);
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
                    Row(content, "ResolutionLabel", "Window size", "ResolutionDropdown", ref row);
                    Row(content, "LookCameraLabel", "Image camera", "LookCameraDropdown", ref row);
                }
                else
                {
                    Heading(content, "Backend & experiments", ref row);
                    Note(content, "These modes require a configured backend. They are optional for phone-only use.", ref row);
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
                    Row(content, "BackendLabel", "Backend URL", "BackendInput", ref row);
                    Note(content, "Backend modes read the backend's own .env. The app API key is never sent to it.", ref row);
                    Row(content, "AutoAiFallbackLabel", "If a request fails", "AutoAiFallbackToggle", ref row, 116);
                    Heading(content, "Downloads", ref row);
                    ShowAt(content, "LocalAiAssetStatusText", row, 68); row += 80;
                    Full(content, "LocalAiAssetRepairButton", "Manage on-device models", ref row);
                    Full(content, "OptionalTtsDownloadButton", "Download additional voices", ref row);
                    Heading(content, "Conversation data", ref row);
                    ModernButton(content, "BackendMemoryButton", "Manage backend memories", () => chatPanel?.OpenBackendMemories());
                    Full(content, "BackendMemoryButton", "Manage backend memories", ref row);
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
