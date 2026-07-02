using NUnit.Framework;
using System.IO;
using UnityEngine;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiPlatformGuardTests
    {
        [Test]
        public void FilePickerCapabilities_CoverDesktopAndMobileTargets()
        {
            foreach (var platform in new[]
                     {
                         YuiPlatformFamily.Windows,
                         YuiPlatformFamily.MacOS,
                         YuiPlatformFamily.IOS,
                         YuiPlatformFamily.Android
                     })
            {
                var capabilities = YuiPlatformFilePickerCapabilities.For(platform);

                Assert.IsTrue(capabilities.SupportsImage, $"{platform} must support image picking.");
                Assert.IsTrue(capabilities.SupportsVrm, $"{platform} must support VRM picking.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(capabilities.Implementation));
            }
        }

        [Test]
        public void WindowsForegroundAppMonitor_IsDisabledInMacEditor()
        {
#if UNITY_EDITOR_OSX
            var gameObject = new GameObject("foreground-monitor-test");
            try
            {
                var monitor = gameObject.AddComponent<YuiWindowsForegroundAppMonitor>();

                Assert.IsFalse(monitor.IsSupported);
                Assert.IsFalse(monitor.GetForegroundApp().IsAvailable);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
#else
            Assert.Pass("This guard is only meaningful in the macOS Unity Editor build.");
#endif
        }

        [Test]
        public void IOSImagePicker_UsesPhotoPickerInsteadOfDocumentPicker()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var pluginPath = Path.Combine(projectRoot, "Assets", "Plugins", "iOS", "iOSDocumentPickerPlugin.mm");

            var source = File.ReadAllText(pluginPath);

            StringAssert.Contains("PHPickerViewController", source);
            StringAssert.Contains("YuiIOSPhotoPickerDelegate", source);
            StringAssert.Contains("YuiDocumentPicker_OpenDocument", source);
        }

        [Test]
        public void IOSLocalVision_UsesOnDeviceVisionClassificationAndTextRecognition()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var pluginPath = Path.Combine(projectRoot, "Assets", "Plugins", "iOS", "YuiPlatformVisionBridge.swift");

            var source = File.ReadAllText(pluginPath);

            StringAssert.Contains("VNClassifyImageRequest", source);
            StringAssert.Contains("VNRecognizeTextRequest", source);
            StringAssert.Contains("YuiPlatformVisionBridge_Analyze", source);
            StringAssert.Contains("\"wine\": \"ワイン\"", source);
            StringAssert.Contains("\"tableware\": \"食器\"", source);
            StringAssert.Contains("\"adult cat\": \"成猫\"", source);
            StringAssert.Contains("主な候補", source);
        }

        [Test]
        public void IOSPlatformSpeech_RequestsPunctuationWhenAvailable()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var pluginPath = Path.Combine(projectRoot, "Assets", "Plugins", "iOS", "YuiPlatformSpeechBridge.swift");

            var source = File.ReadAllText(pluginPath);

            StringAssert.Contains("addsPunctuation", source);
        }

        [Test]
        public void IOSGoogleAiEdgePrompt_AllowsContextualLocalAnswers()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var pluginPath = Path.Combine(projectRoot, "Assets", "Plugins", "iOS", "YuiGoogleAiEdgeBridge.swift");

            var source = File.ReadAllText(pluginPath);

            StringAssert.Contains("通常は短く", source);
            StringAssert.Contains("40〜80字", source);
            StringAssert.Contains("100字前後", source);
            StringAssert.Contains("2〜4文", source);
            StringAssert.DoesNotContain("1〜2文で音声", source);
        }

        [Test]
        public void IOSGoogleAiEdgeSampler_UsesWarmerChatButKeepsVisionConservative()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var pluginPath = Path.Combine(projectRoot, "Assets", "Plugins", "iOS", "YuiGoogleAiEdgeBridge.swift");

            var source = File.ReadAllText(pluginPath);

            StringAssert.Contains("yuiSamplerTemperature", source);
            StringAssert.Contains("capability == \"Chat\" ? 0.70 : 0.45", source);
            StringAssert.Contains("temperature: yuiSamplerTemperature(for: capability)", source);
        }

        [Test]
        public void DirectOpenAiClient_UsesResponsesApiWithStructuredChatOutput()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var clientPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "Api", "YuiDirectOpenAiClient.cs");

            Assert.IsTrue(File.Exists(clientPath), "Direct OpenAI chat must be available without the backend.");
            var source = File.ReadAllText(clientPath);

            StringAssert.Contains("https://api.openai.com/v1/responses", source);
            StringAssert.Contains("Authorization", source);
            StringAssert.Contains("Bearer ", source);
            StringAssert.Contains("json_schema", source);
            StringAssert.Contains("additionalProperties", source);
            StringAssert.Contains("ExtractOutputText", source);
            StringAssert.DoesNotContain("Debug.Log(openAiApiKey", source);
        }

        [Test]
        public void SettingsOverlay_ExposesDirectOpenAiKeyAndModelInAdvancedSettings()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var settingsPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiSettingsOverlay.cs");
            var runtimeUiPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiSettingsOverlay.RuntimeUi.cs");
            var actionsPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiSettingsOverlay.Actions.cs");
            var chatPanelPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiChatPanel.Settings.cs");

            var settings = File.ReadAllText(settingsPath);
            var runtimeUi = File.ReadAllText(runtimeUiPath);
            var actions = File.ReadAllText(actionsPath);
            var chatPanel = File.ReadAllText(chatPanelPath);

            StringAssert.Contains("openAiApiKeyInput", settings);
            StringAssert.Contains("openAiModelInput", settings);
            StringAssert.Contains("OpenAI API Key", runtimeUi);
            StringAssert.Contains("OpenAI Model", runtimeUi);
            StringAssert.Contains("SetDirectOpenAiSettings", actions);
            StringAssert.Contains("SetDirectOpenAiSettings", chatPanel);
        }

        [Test]
        public void SettingsOverlay_KeepsAdvancedApiBlockOpaque()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var runtimeUiPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiSettingsOverlay.RuntimeUi.cs");

            var runtimeUi = File.ReadAllText(runtimeUiPath);

            StringAssert.Contains("EnsureAdvancedPanelBacking", runtimeUi);
            StringAssert.Contains("AdvancedApiBacking", runtimeUi);
            StringAssert.Contains("0.98f", runtimeUi);
            StringAssert.Contains("SetAsFirstSibling", runtimeUi);
        }

        [Test]
        public void SettingsOverlay_MakesOpaqueAdvancedInputsReadable()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var runtimeUiPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiSettingsOverlay.RuntimeUi.cs");

            var runtimeUi = File.ReadAllText(runtimeUiPath);

            StringAssert.Contains("MakeRuntimeInputReadable(backendUrlInput", runtimeUi);
            StringAssert.Contains("MakeRuntimeInputReadable(openAiApiKeyInput", runtimeUi);
            StringAssert.Contains("MakeRuntimeInputReadable(openAiModelInput", runtimeUi);
            StringAssert.Contains("input.textComponent.color", runtimeUi);
            StringAssert.Contains("placeholderText.color", runtimeUi);
            StringAssert.Contains("input.caretColor", runtimeUi);
            StringAssert.Contains("input.selectionColor", runtimeUi);
        }

        [Test]
        public void SettingsOverlay_AdvancedApiInputsReserveThreeRowsAndClipLongSecrets()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var runtimeUiPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiSettingsOverlay.RuntimeUi.cs");
            var setupPath = Path.Combine(projectRoot, "Assets", "App", "Editor", "YuiAvatarSceneSetup.cs");

            var runtimeUi = File.ReadAllText(runtimeUiPath);
            var setup = File.ReadAllText(setupPath);

            StringAssert.Contains("EnsureAdvancedApiPanelFrame", runtimeUi);
            StringAssert.Contains("CreateOrMoveRuntimeLabel(parent, \"BackendLabel\", \"Backend URL\", 20f)", runtimeUi);
            StringAssert.Contains("SetTopRectRuntime(backendUrlInput.transform, 176f, 10f, 22f, 42f)", runtimeUi);
            StringAssert.Contains("CreateOrMoveRuntimeLabel(parent, \"OpenAiApiKeyLabel\", \"OpenAI API Key\", 78f)", runtimeUi);
            StringAssert.Contains("SetTopRectRuntime(openAiApiKeyInput.transform, 176f, 68f, 22f, 42f)", runtimeUi);
            StringAssert.Contains("CreateOrMoveRuntimeLabel(parent, \"OpenAiModelLabel\", \"OpenAI Model\", 136f)", runtimeUi);
            StringAssert.Contains("SetTopRectRuntime(openAiModelInput.transform, 176f, 126f, 22f, 42f)", runtimeUi);
            StringAssert.Contains("input.gameObject.AddComponent<RectMask2D>()", runtimeUi);
            StringAssert.Contains("input.textComponent.horizontalOverflow = HorizontalWrapMode.Overflow", runtimeUi);
            StringAssert.Contains("input.textComponent.verticalOverflow = VerticalWrapMode.Truncate", runtimeUi);
            StringAssert.Contains("input.textComponent.resizeTextForBestFit = false", runtimeUi);
            StringAssert.Contains("SetAnchors(root, new Vector2(0.07f, 0.68f), new Vector2(0.93f, 0.86f))", setup);
            StringAssert.Contains("EnsureHelpText(root, \"OpenAiApiKeyLabel\", \"OpenAI API Key\"", setup);
            StringAssert.Contains("EnsureSettingsInput(root, \"OpenAiApiKeyInput\"", setup);
            StringAssert.Contains("EnsureHelpText(root, \"OpenAiModelLabel\", \"OpenAI Model\"", setup);
            StringAssert.Contains("EnsureSettingsInput(root, \"OpenAiModelInput\"", setup);
        }

        [Test]
        public void SettingsOverlay_RefreshesCapabilitiesBeforeDecoratingModeLabels()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var settingsPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiSettingsOverlay.cs");
            var capabilityPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiChatPanel.Capabilities.cs");

            var settings = File.ReadAllText(settingsPath);
            var capabilities = File.ReadAllText(capabilityPath);

            StringAssert.Contains("await chatPanel.RefreshCapabilitySnapshotAsync", settings);
            StringAssert.Contains("GetProviderStatusAsync", capabilities);
            StringAssert.Contains("providerStatus != null || IsBackendRecentlyReachable()", capabilities);
            StringAssert.Contains("YuiLocalAiRuntimeFactory.HasOnDeviceEmbeddedPack", capabilities);
        }

        [Test]
        public void LocalAiGuidance_UsesApiNameInsteadOfAdvancedApiNickname()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var modesPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "Core", "YuiConversationModes.cs");
            var promptPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "LocalAI", "Adapters", "YuiLocalAiPromptBuilder.cs");

            var modes = File.ReadAllText(modesPath);
            var prompt = File.ReadAllText(promptPath);

            StringAssert.Contains("APIの方が向いています", modes);
            StringAssert.Contains("APIならより正確", prompt);
            StringAssert.DoesNotContain("Advanced/API", modes);
            StringAssert.DoesNotContain("Advanced/API", prompt);
        }

        [Test]
        public void StableApiImageUpload_AttachesImageForNextChatInsteadOfShowingVisionSummary()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var visionPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiChatPanel.Vision.cs");

            var source = File.ReadAllText(visionPath);

            StringAssert.Contains("ShouldAttachImageForApiChat", source);
            StringAssert.Contains("YuiConversationModes.Stable", source);
            StringAssert.Contains("次のメッセージで画像を直接見ながら返答します", source);
        }

        [Test]
        public void TtsDefaults_PreferStableVoicevoxOverAivisForLongApiReplies()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var settingsPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiChatPanel.Settings.cs");

            var source = File.ReadAllText(settingsPath);

            StringAssert.Contains("private const int CurrentVoiceTuningSchemaVersion = 8", File.ReadAllText(Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiChatPanel.cs")));
            StringAssert.Contains("return \"server\";", source);
            StringAssert.DoesNotContain("shouldMoveOldDefaultToAivis", source);
            StringAssert.DoesNotContain("string.Equals(savedTtsMode, \"aivis\"", source);
        }

        [Test]
        public void RealtimeNamedTtsModes_ForceTheirMatchingVoiceEngine()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var actionsPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiSettingsOverlay.Actions.cs");

            var source = File.ReadAllText(actionsPath);

            StringAssert.Contains("TtsModeForConversationMode", source);
            StringAssert.Contains("YuiConversationModes.IsRealtimeVoicevox", source);
            StringAssert.Contains("YuiConversationModes.IsRealtimeAivis", source);
            StringAssert.Contains("return \"aivis\";", source);
        }

        [Test]
        public void HelpOverlay_ExplainsLocalAiAndApiQualityTradeoff()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var helpPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiHelpOverlay.cs");

            var source = File.ReadAllText(helpPath);

            StringAssert.Contains("ローカルAI", source);
            StringAssert.Contains("オフライン", source);
            StringAssert.Contains("API", source);
            StringAssert.Contains("高精度", source);
            StringAssert.Contains("できないこと", source);
            StringAssert.Contains("Realtime", source);
            StringAssert.Contains("メモリDB", source);
        }

        [Test]
        public void HelpOverlay_UsesSecretModeWordingAndReadableTextSizing()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var helpPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiHelpOverlay.cs");

            var source = File.ReadAllText(helpPath);

            StringAssert.Contains("シークレットモード", source);
            StringAssert.DoesNotContain("Sは履歴", source);
            StringAssert.Contains("SetText(panel.Find(\"Subtitle\"),", source);
            StringAssert.Contains("SetText(card.Find(\"Title\"), title, 18", source);
            StringAssert.Contains("SetText(card.Find(\"Body\"), body, 15", source);
            StringAssert.Contains("SetText(card.Find(\"Example\"), example, 14", source);
            StringAssert.Contains("text.resizeTextForBestFit = false", source);
        }

        [Test]
        public void HelpOverlay_FormatsProviderStatusWithLabelsAndSeparators()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var helpPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiHelpOverlay.cs");
            var diagnosticsPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiCapabilityDiagnostics.cs");

            var source = File.ReadAllText(helpPath);
            var diagnostics = File.ReadAllText(diagnosticsPath);

            StringAssert.Contains("YuiCapabilityDiagnostics.FormatBody", source);
            StringAssert.Contains("YuiCapabilityMatrix.FromProviderStatus", source);
            StringAssert.Contains("Backend", diagnostics);
            StringAssert.Contains("DB", diagnostics);
            StringAssert.Contains("OpenAI", diagnostics);
            StringAssert.Contains("Irodori TTS", diagnostics);
            StringAssert.Contains(" | ", diagnostics);
            StringAssert.Contains("StatusBadge", diagnostics);
            StringAssert.Contains("text.supportRichText = true", source);
            StringAssert.DoesNotContain("StatusIcon(status.Backend?.Status) + \" Backend\"", source);
        }

        [Test]
        public void SettingsOverlay_ExpandsAiModeDropdownSoAllModesAreVisible()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var layoutPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiSettingsOverlay.Layout.cs");
            var runtimeUiPath = Path.Combine(projectRoot, "Assets", "App", "Scripts", "UI", "YuiSettingsOverlay.RuntimeUi.cs");

            var layout = File.ReadAllText(layoutPath);
            var runtimeUi = File.ReadAllText(runtimeUiPath);

            StringAssert.Contains("PrepareDropdownTemplateRuntime(content.Find(\"ConversationModeDropdown\")", layout);
            StringAssert.Contains("PrepareDropdownTemplateRuntime(conversationModeDropdown", runtimeUi);
            StringAssert.Contains("286f", layout);
            StringAssert.Contains("286f", runtimeUi);
        }
    }
}
