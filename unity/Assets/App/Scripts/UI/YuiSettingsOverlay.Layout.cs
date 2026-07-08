using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiSettingsOverlay
    {
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
                contentRect.sizeDelta = new Vector2(0f, Mathf.Max(contentRect.sizeDelta.y, 2940f));
            }

            if (content != null)
            {
                ReflowSettingsRowsRuntime(content);
            }
        }

        private void ReflowSettingsRowsRuntime(Transform content)
        {
            SetTopRectRuntime(content.Find("ExperimentalSection"), 18f, 18f, 22f, 34f);
            SetLabelTextRuntime(content.Find("ExperimentalSection"), "AI");
            SetTopRectRuntime(content.Find("ConversationModeLabel"), 18f, 72f, 248f, 34f);
            SetLabelTextRuntime(content.Find("ConversationModeLabel"), "AI Mode");
            SetTopRectRuntime(content.Find("ConversationModeDropdown"), 176f, 62f, 22f, 54f);
            PrepareDropdownTemplateRuntime(content.Find("ConversationModeDropdown"), 286f, 34f);

            var ttsModeValue = TtsModeValue();
            var irodori = string.Equals(ttsModeValue, "server-http", System.StringComparison.OrdinalIgnoreCase);
            var voicevox = string.Equals(ttsModeValue, "server", System.StringComparison.OrdinalIgnoreCase);
            var aivis = string.Equals(ttsModeValue, "aivis", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(ttsModeValue, "aivis-native", System.StringComparison.OrdinalIgnoreCase);
            var voiceCompatible = voicevox || aivis;
            SetVoiceControlVisible(content, "SpeakerLabel", voiceCompatible);
            SetVoiceControlVisible(content, "SpeakerDropdown", voiceCompatible);
            SetVoiceControlVisible(content, "IrodoriVoiceGenderLabel", irodori);
            SetVoiceControlVisible(content, "IrodoriVoiceGenderDropdown", irodori);
            SetVoiceControlVisible(content, "IrodoriVoiceInstructLabel", irodori);
            SetVoiceControlVisible(content, "IrodoriVoiceInstructInput", irodori);
            SetVoiceControlVisible(content, "IntonationLabel", voiceCompatible);
            SetVoiceControlVisible(content, "IntonationSlider", voiceCompatible);
            SetVoiceControlVisible(content, "IntonationValue", voiceCompatible);
            SetVoiceControlVisible(content, "SynthesisVolumeLabel", false);
            SetVoiceControlVisible(content, "SynthesisVolumeSlider", false);
            SetVoiceControlVisible(content, "SynthesisVolumeValue", false);
            SetVoiceControlVisible(content, "PrePauseLabel", false);
            SetVoiceControlVisible(content, "PrePauseSlider", false);
            SetVoiceControlVisible(content, "PrePauseValue", false);
            SetVoiceControlVisible(content, "PostPauseLabel", false);
            SetVoiceControlVisible(content, "PostPauseSlider", false);
            SetVoiceControlVisible(content, "PostPauseValue", false);

            SetTopRectRuntime(content.Find("VoiceSection"), 18f, 150f, 22f, 34f);
            SetLabelTextRuntime(content.Find("VoiceSection"), "Voice");
            var row = 204f;
            SetTopRectRuntime(content.Find("TtsModeLabel"), 18f, row, 248f, 34f);
            SetLabelTextRuntime(content.Find("TtsModeLabel"), "Voice Engine");
            SetTopRectRuntime(content.Find("TtsModeDropdown"), 176f, row - 10f, 22f, 54f);
            row += 70f;

            SetTopRectRuntime(content.Find("VolumeLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("VolumeSlider"), 176f, row + 5f, 92f, 28f);
            SetTopRightRectRuntime(content.Find("VolumeValue"), 18f, row - 1f, 70f, 34f);
            row += 70f;

            SetTopRectRuntime(content.Find("VoicePreviewLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("VoicePreviewButton"), 176f, row - 10f, 22f, 54f);
            row += 67f;

            SetTopRectRuntime(content.Find("SpeedLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("SpeedSlider"), 176f, row + 5f, 92f, 28f);
            SetTopRightRectRuntime(content.Find("SpeedValue"), 18f, row - 1f, 70f, 34f);
            row += 70f;
            SetTopRectRuntime(content.Find("PitchLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("PitchSlider"), 176f, row + 5f, 92f, 28f);
            SetTopRightRectRuntime(content.Find("PitchValue"), 18f, row - 1f, 70f, 34f);
            row += 80f;

            SetTopRectRuntime(content.Find("VoicePresetLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("VoicePresetDropdown"), 176f, row - 10f, 22f, 54f);
            row += 70f;
            SetTopRectRuntime(content.Find("VoicePresetNameLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("VoicePresetNameInput"), 176f, row - 10f, 22f, 42f);
            row += 60f;
            SetTopRectRuntime(content.Find("VoicePresetActionLabel"), 18f, row, 248f, 34f);
            SetTopRectColumnRuntime(content.Find("VoicePresetSaveButton"), 176f, 22f, row - 10f, 42f, 0f, 0.50f, 8f);
            SetTopRectColumnRuntime(content.Find("VoicePresetDeleteButton"), 176f, 22f, row - 10f, 42f, 0.50f, 1f, 8f);
            row += 70f;

            SetTopRectRuntime(content.Find("SpeakerLabel"), 18f, row, 248f, 34f);
            SetLabelTextRuntime(content.Find("SpeakerLabel"), aivis ? "Aivis Voice" : "VOICEVOX Voice");
            SetTopRectRuntime(content.Find("SpeakerDropdown"), 176f, row - 10f, 22f, 54f);
            if (voiceCompatible)
            {
                row += 70f;
                SetTopRectRuntime(content.Find("IntonationLabel"), 18f, row, 248f, 34f);
                SetTopRectRuntime(content.Find("IntonationSlider"), 176f, row + 5f, 92f, 28f);
                SetTopRightRectRuntime(content.Find("IntonationValue"), 18f, row - 1f, 70f, 34f);
                row += 110f;
            }

            SetTopRectRuntime(content.Find("IrodoriVoiceGenderLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("IrodoriVoiceGenderDropdown"), 176f, row - 10f, 22f, 54f);
            if (irodori)
            {
                row += 70f;
                SetTopRectRuntime(content.Find("IrodoriVoiceInstructLabel"), 18f, row, 248f, 34f);
                SetTopRectRuntime(content.Find("IrodoriVoiceInstructInput"), 176f, row - 10f, 22f, 72f);
                row += 110f;
            }

            SetTopRectRuntime(content.Find("LocalAiAssetSectionLabel"), 18f, row, 22f, 34f);
            SetLabelTextRuntime(content.Find("LocalAiAssetSectionLabel"), "Downloads");
            row += 48f;
            SetTopRectRuntime(content.Find("LocalAiAssetStatusText"), 18f, row, 22f, 42f);
            row += 54f;
            SetTopRectColumnRuntime(content.Find("LocalAiAssetRepairButton"), 176f, 22f, row - 10f, 42f, 0f, 0.50f, 8f);
            SetTopRectColumnRuntime(content.Find("OptionalTtsDownloadButton"), 176f, 22f, row - 10f, 42f, 0.50f, 1f, 8f);
            row += 70f;

            SetTopRectRuntime(content.Find("MicSection"), 18f, row, 22f, 34f);
            SetLabelTextRuntime(content.Find("MicSection"), "Input");
            row += 54f;
            SetTopRectRuntime(content.Find("MicrophoneLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("MicrophoneDropdown"), 176f, row - 10f, 22f, 54f);
            row += 60f;
            SetTopRectRuntime(content.Find("MicrophoneTestButton"), 176f, row, 22f, 44f);
            row += 50f;
            SetTopRectRuntime(content.Find("MicrophoneTestMeter"), 176f, row, 22f, 18f);
            SetTopRectRuntime(content.Find("MicrophoneTestStatus"), 176f, row + 20f, 22f, 24f);
            row += 70f;

            SetTopRectRuntime(content.Find("InputCameraSection"), 18f, row, 22f, 34f);
            row += 54f;
            SetTopRectRuntime(content.Find("LookCameraLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("LookCameraDropdown"), 176f, row - 10f, 22f, 54f);
            row += 90f;

            SetTopRectRuntime(content.Find("AvatarSection"), 18f, row, 22f, 34f);
            row += 54f;
            SetTopRectRuntime(content.Find("AvatarLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("AvatarDropdown"), 176f, row - 10f, 22f, 54f);
            row += 70f;
            SetTopRectRuntime(content.Find("CustomVrmNameLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("CustomVrmNameInput"), 176f, row - 10f, 22f, 42f);
            row += 60f;
            SetTopRectColumnRuntime(content.Find("CustomVrmImportButton"), 176f, 22f, row - 10f, 42f, 0f, 0.50f, 8f);
            SetTopRectColumnRuntime(content.Find("CustomVrmClearButton"), 176f, 22f, row - 10f, 42f, 0.50f, 1f, 8f);
            row += 80f;

            SetTopRectRuntime(content.Find("CameraSection"), 18f, row, 22f, 34f);
            row += 54f;
            SetTopRectRuntime(content.Find("CameraPresetLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("CameraPresetDropdown"), 176f, row - 10f, 22f, 54f);
            row += 60f;
            SetTopRectRuntime(content.Find("CameraAdjustButton"), 176f, row - 10f, 22f, 44f);
            row += 60f;
            SetTopRectRuntime(content.Find("CameraActionLabel"), 18f, row, 248f, 34f);
            SetTopRectColumnRuntime(content.Find("CameraAutoButton"), 176f, 22f, row - 10f, 40f, 0f, 0.44f, 8f);
            SetTopRectColumnRuntime(content.Find("CameraSaveButton"), 176f, 22f, row - 10f, 40f, 0.44f, 0.72f, 8f);
            SetTopRectColumnRuntime(content.Find("CameraDeleteButton"), 176f, 22f, row - 10f, 40f, 0.72f, 1f, 8f);
            row += 80f;

            SetTopRectRuntime(content.Find("WindowSection"), 18f, row, 22f, 34f);
            row += 54f;
            SetTopRectRuntime(content.Find("ResolutionLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("ResolutionDropdown"), 176f, row - 10f, 22f, 54f);
            row += 70f;
            SetTopRectRuntime(content.Find("BackgroundLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("BackgroundDropdown"), 176f, row - 10f, 22f, 54f);
            row += 100f;

            SetTopRectRuntime(content.Find("CharacterSection"), 18f, row, 22f, 34f);
            row += 54f;
            SetTopRectRuntime(content.Find("CharacterNameLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("CharacterNameInput"), 176f, row - 10f, 22f, 54f);
            row += 70f;
            SetTopRectRuntime(content.Find("CustomInstructionLabel"), 18f, row, 248f, 34f);
            SetTopRectRuntime(content.Find("CustomInstructionInput"), 176f, row - 10f, 22f, 132f);
            row += 150f;

            var contentRect = content.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, Mathf.Max(row, 1200f));
            }
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

        private static void PrepareDropdownTemplateRuntime(Transform dropdownTransform, float templateHeight, float itemHeight)
        {
            if (dropdownTransform == null)
            {
                return;
            }

            PrepareDropdownTemplateRuntime(dropdownTransform.GetComponent<Dropdown>(), templateHeight, itemHeight);
        }

        private static void PrepareDropdownTemplateRuntime(Dropdown dropdown, float templateHeight, float itemHeight)
        {
            if (dropdown == null)
            {
                return;
            }

            var template = dropdown.template != null
                ? dropdown.template
                : dropdown.transform.Find("Template")?.GetComponent<RectTransform>();
            if (template == null)
            {
                return;
            }

            dropdown.template = template;
            template.sizeDelta = new Vector2(template.sizeDelta.x, Mathf.Max(120f, templateHeight));

            var scrollRect = template.GetComponent<ScrollRect>();
            var viewport = template.Find("Viewport") as RectTransform;
            if (viewport != null)
            {
                SetAnchorsRuntime(viewport, Vector2.zero, Vector2.one);
            }

            var content = template.Find("Viewport/Content") as RectTransform;
            if (content != null)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                var optionCount = Mathf.Max(1, dropdown.options != null ? dropdown.options.Count : 1);
                content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(templateHeight, optionCount * itemHeight));
                if (scrollRect != null)
                {
                    scrollRect.content = content;
                }
            }

            var item = template.Find("Viewport/Content/Item") as RectTransform;
            if (item != null)
            {
                var contentHeight = Mathf.Max(templateHeight, content != null ? content.sizeDelta.y : templateHeight);
                item.anchorMin = new Vector2(0f, Mathf.Clamp01(1f - itemHeight / contentHeight));
                item.anchorMax = Vector2.one;
                item.offsetMin = Vector2.zero;
                item.offsetMax = Vector2.zero;
            }

            if (scrollRect != null)
            {
                scrollRect.viewport = viewport;
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.scrollSensitivity = 24f;
            }

            dropdown.itemText = template.Find("Viewport/Content/Item/Item Label")?.GetComponent<Text>() ?? dropdown.itemText;
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

        private static void SetLabelTextRuntime(Transform labelTransform, string value)
        {
            var label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (label != null)
            {
                label.text = value;
            }
        }

        private static void SetVoiceControlVisible(Transform content, string name, bool visible)
        {
            var target = content.Find(name);
            if (target != null)
            {
                target.gameObject.SetActive(visible);
            }
        }

        private void RefreshTtsSpecificVoiceControls()
        {
            if (settingsRoot == null)
            {
                return;
            }

            var content = UiTreeUtility.FindDeepChild(settingsRoot.transform, "Content");
            if (content != null)
            {
                ReflowSettingsRowsRuntime(content);
            }
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
    }
}
