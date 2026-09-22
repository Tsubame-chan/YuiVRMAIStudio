using UnityEngine;
using UnityEngine.UI;
namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiSettingsOverlay
    {
        private void EnsureLanguageControl(Transform content)
        {
            if (languageDropdown == null)
            {
                // Clone presentation, replace the event so no AI/display side effects survive.
                languageDropdown = Instantiate(backgroundDropdown != null ? backgroundDropdown : conversationModeDropdown, content);
                languageDropdown.name = "UiLanguageDropdown";
                languageDropdown.onValueChanged = new Dropdown.DropdownEvent();
                languageDropdown.ClearOptions();
                languageDropdown.AddOptions(new System.Collections.Generic.List<string> { "日本語", "English" });
                languageDropdown.onValueChanged.AddListener(index => YuiUiLocalization.SetLanguage(index == 0 ? "ja" : "en"));
                ModernText(content, "UiLanguageLabel", "Language", YuiUiTypography.Label).fontStyle = FontStyle.Bold;
            }
            languageDropdown.SetValueWithoutNotify(YuiUiLocalization.Language == "ja" ? 0 : 1);
        }
        private void RefreshUiLanguage()
        {
            // Do not RefreshFields here: it would discard unsaved AI/key/personality edits.
            if (settingsRoot != null) ApplyResponsiveOverlayLayout();
        }
        private void LocalizeSettingsOptions()
        {
            // These dropdowns persist stable indexes/IDs. User/device/preset names stay verbatim.
            foreach (var dropdown in new[] { conversationModeDropdown, ttsModeDropdown, irodoriVoiceGenderDropdown,
                backgroundDropdown, advancedModeDropdown, cameraPresetDropdown })
            {
                if (dropdown == null) continue;
                foreach (var option in dropdown.options) option.text = YuiUiLocalization.Text(option.text);
                dropdown.RefreshShownValue();
            }
            if (voicePresetDropdown != null && voicePresetDropdown.options.Count > 0)
            { voicePresetDropdown.options[0].text=YuiUiLocalization.Text("Manual");voicePresetDropdown.RefreshShownValue(); }
            if (microphoneDropdown != null)
            {
                for (var i = 0; i < microphoneOptionValues.Length && i < microphoneDropdown.options.Count; i++)
                    microphoneDropdown.options[i].text = microphoneOptionValues[i] == "Default" ? YuiUiLocalization.Text("Default microphone") : microphoneOptionValues[i];
                microphoneDropdown.RefreshShownValue();
            }
            if (lookCameraDropdown != null)
            {
                for (var i = 0; i < cameraOptionValues.Length && i < lookCameraDropdown.options.Count; i++)
                    lookCameraDropdown.options[i].text = cameraOptionValues[i] == "Disabled" ? YuiUiLocalization.Text("Camera off") : cameraOptionValues[i];
                lookCameraDropdown.RefreshShownValue();
            }
            if (avatarDropdown != null && chatPanel != null)
            {
                var labels = chatPanel.GetAvatarSlotOptions();
                for (var i = 0; i < labels.Length && i < avatarDropdown.options.Count; i++)
                {
                    // Only generated default/empty labels; never translate a user's appearance name.
                    var slot = chatPanel.GetAvatarSlotValueForOptionIndex(i);
                    avatarDropdown.options[i].text = !YuiPhysicalAI.Core.YuiAvatarSlots.IsCustomVrm(slot)
                        || labels[i].EndsWith(" · Empty", System.StringComparison.Ordinal) || labels[i].EndsWith(" · File missing", System.StringComparison.Ordinal)
                        ? YuiUiLocalization.Text(labels[i]) : labels[i];
                }
                avatarDropdown.RefreshShownValue();
            }
        }
        private void StyleCameraAdjustmentHud()
        {
            if (cameraAdjustRoot == null) return;
            var root = cameraAdjustRoot.transform;
            YuiUiTypography.Apply(root);
            YuiUiTheme.SurfaceOn(cameraAdjustRoot.GetComponent<Image>(),YuiUiTheme.Surface);
            var title=root.Find("Title")?.GetComponent<Text>();
            if(title!=null) { YuiUiLocalization.Set(title,"Adjust view");title.fontSize=YuiUiTypography.Heading;title.resizeTextForBestFit=false;SetTopRectRuntime(title.transform,24,16,24,58); }
            var body=root.Find("Body")?.GetComponent<Text>();
            var height=180f;
            if(body!=null)
            {
                YuiUiLocalization.Set(body,"Drag to rotate. Right-drag or use two fingers to move. Scroll or pinch to zoom. Done saves this view.",true);
                body.fontSize=YuiUiTypography.Note;body.resizeTextForBestFit=false;body.color=YuiUiTheme.Muted;
                height=Mathf.Max(150,body.cachedTextGeneratorForLayout.GetPreferredHeight(body.text,body.GetGenerationSettings(new Vector2(392,0)))/body.pixelsPerUnit+12);
                SetTopRectRuntime(body.transform,24,84,24,height);
            }
            ((RectTransform)root).sizeDelta=new Vector2(440,height+184);
            if(cameraAdjustDoneButton!=null) { YuiUiTheme.ButtonStyle(cameraAdjustDoneButton,true);SetButtonCaption(cameraAdjustDoneButton,"Done");SetTopRectRuntime(cameraAdjustDoneButton.transform,24,height+102,24,64); }
        }
    }
}
