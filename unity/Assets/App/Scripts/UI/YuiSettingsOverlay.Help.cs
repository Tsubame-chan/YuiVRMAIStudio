using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiSettingsOverlay
    {
        private GameObject settingsHelpLayer;

        private void SettingsHelpRow(Transform content, string labelName, string label, string inputName, string helpName, ref float row)
        {
            var top = row;
            Row(content, labelName, label, inputName, ref row);
            var labelTransform = content.Find(labelName);
            if (labelTransform != null) SetTopRectRuntime(labelTransform, 24, top, 96, 52);
            var help = ModernButton(content, helpName, "?", () => ShowSettingsHelp(content.Find(helpName), helpName));
            SetTopRightRectRuntime(help.transform, 24, top, 64, 56);
        }

        private void BackendHelpHeading(Transform content, ref float row)
        {
            var top = row;
            var title = ModernText(content, "HeadingBackend", "Backend & experiments", YuiUiTypography.Heading);
            title.gameObject.SetActive(true);
            SetTopRectRuntime(title.transform, 24, top, 104, 66);
            var help = ModernButton(content, "BackendHelp", "?", () => ShowSettingsHelp(content.Find("BackendHelp"), "BackendHelp"));
            SetTopRightRectRuntime(help.transform, 24, top + 5, 64, 56);
            row += 82;
        }

        private void CloseSettingsHelp()
        {
            if (settingsHelpLayer == null) return;
            settingsHelpLayer.SetActive(false);
            Destroy(settingsHelpLayer);
            settingsHelpLayer = null;
        }

        private void ShowBackendAcquisitionHelp(bool address)
        {
            var dialog=YuiSimpleDialog.Create(YuiSimpleDialog.L("Backendの入手と接続", "Get and connect Backend"),
                YuiSimpleDialog.L("Backendは、ご自身のMacやWindows PCで動かす追加機能です。\n\n1. 下の配布ページからPC版Yuiを入手し、展開・起動して必要データを準備します。\n2. 導入手順に沿ってPCでBackendを起動します。\n3. スマートフォンから使う場合は両方を同じTailscaleに接続し、この設定画面にPCの接続先を入力します。\n\n端末内AI・端末内音声・OpenAIへの直接接続だけなら不要です。",
                "Backend is an optional service running on your own Mac or Windows PC.\n\n1. Get the desktop Yui app below, extract and launch it, then prepare its data.\n2. Follow the setup guide to start Backend on the PC.\n3. To connect from your phone, join both devices to the same Tailscale account and enter the PC address in Settings.\n\nOn-device AI, local voices and direct OpenAI access do not require Backend."));
            dialog.AddButton(YuiSimpleDialog.L("PC版を入手（Mac / Windows） ↗", "Get desktop app (Mac / Windows) ↗"),()=>Application.OpenURL("https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.4-beta.2"));
            dialog.AddButton(YuiSimpleDialog.L("導入・接続手順を開く ↗", "Open setup and connection guide ↗"),()=>Application.OpenURL("https://github.com/Tsubame-chan/YuiVRMAIStudio#readme"));
            dialog.AddButton(YuiSimpleDialog.L("閉じる", "Close"),dialog.Close);
            if(address)dialog.Body.text+=YuiSimpleDialog.L("\n\n接続先の例：http://100.64.0.9:8000。同じPCならhttp://127.0.0.1:8000。末尾に/healthや/admin/は付けません。", "\n\nExample: http://100.64.0.9:8000. On the same PC use http://127.0.0.1:8000. Do not append /health or /admin/.");
            dialog.Compact(640);
        }

        private void ShowSettingsHelp(Transform source, string topic)
        {
            var apiKey = topic == "ApiKeyHelp";
            var address = topic == "BackendAddressHelp";
            var backend = topic == "BackendHelp" || address;
            CloseSettingsHelp();
            if (backend) { ShowBackendAcquisitionHelp(address); return; }
            var panel = settingsRoot.transform.Find("Panel") as RectTransform;
            if (panel == null || source == null) return;
            // A separate, bounded layer leaves unsaved input untouched and closes on an outside tap.
            settingsHelpLayer = new GameObject("SettingsFieldHelp", typeof(RectTransform), typeof(Image), typeof(Button));
            settingsHelpLayer.transform.SetParent(panel, false);
            SetAnchorsRuntime(settingsHelpLayer.transform, Vector2.zero, Vector2.one);
            settingsHelpLayer.GetComponent<Image>().color = new Color(0, 0, 0, .24f);
            settingsHelpLayer.GetComponent<Button>().onClick.AddListener(CloseSettingsHelp);
            var card = new GameObject("HelpCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(settingsHelpLayer.transform, false);
            var surface = card.GetComponent<Image>();
            surface.color = SettingsField;
            RoundSurface(surface);
            var rect = (RectTransform)card.transform;
            var width = Mathf.Min(640, panel.rect.width - 48);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(width, 500);

            var title = ModernText(rect, "Title", address ? YuiSimpleDialog.L("Backendへの接続方法", "Connect to Backend") : backend ? "What is Backend?" : apiKey ? "What is an API key?" : "How to enter a model", YuiUiTypography.Heading);
            SetTopRectRuntime(title.transform, 24, 20, 84, 62);
            var close = ModernButton(rect, "CloseHelp", "×", CloseSettingsHelp);
            SetTopRightRectRuntime(close.transform, 16, 16, 56, 56);
            var row = 94f;
            if (address)
            {
                Note(rect, Application.isMobilePlatform
                    ? YuiSimpleDialog.L("PCでBackendを起動し、PCとこの端末を同じTailscaleアカウントに接続します。", "Start Backend on your PC and connect both devices to the same Tailscale account.")
                    : YuiSimpleDialog.L("このPCでBackendを起動する場合は http://127.0.0.1:8000 を使います。別のPCなら、両方をTailscaleに接続します。", "For Backend on this PC, use http://127.0.0.1:8000. For another PC, connect both PCs to Tailscale."), ref row);
                Note(rect, Application.isMobilePlatform
                    ? YuiSimpleDialog.L("Tailscaleの端末一覧でPCのIPを確認し、http://IP:8000 の形で入力します。例：http://100.64.0.9:8000", "Find your PC’s IP in the Tailscale device list. Enter http://IP:8000, for example http://100.64.0.9:8000")
                    : YuiSimpleDialog.L("別のPCのBackendに接続する場合は、そのPCのTailscale IPを入力します。例：http://100.64.0.9:8000", "For Backend on another PC, enter that PC's Tailscale IP. Example: http://100.64.0.9:8000"), ref row);
                Note(rect, YuiSimpleDialog.L("/healthは付けません。「Backendで会話」を選び、「設定を保存」を押します。", "Do not append /health. Choose Backend conversation, then Save settings."), ref row);
                Note(rect, YuiSimpleDialog.L("端末内AI・VOICEVOX・OpenAIへの直接接続にはBackendは不要です。", "On-device AI, VOICEVOX and direct OpenAI access do not need Backend."), ref row);
            }
            else if (backend)
            {
                Note(rect, "Backend is an optional service running on a PC or server. It lets you use more natural-sounding speech and AI services beyond those included on your device.", ref row);
                Note(rect, "It can use API keys set up on that PC or server, separately from the app's key. Available features depend on the connected service.", ref row);
                Note(rect, YuiSimpleDialog.L("モバイル版・デスクトップ版ともに任意です。端末内AI・VOICEVOX・OpenAIへの直接接続には不要です。Backendを使う場合だけ、接続先を設定してください。", "Backend is optional on both mobile and desktop. On-device AI, VOICEVOX and direct OpenAI access work without it. Configure a connection only if you use Backend."), ref row);
            }
            else if (apiKey)
                Note(rect, "Use a key to access OpenAI's more capable AI models. Create one and set up billing on OpenAI Platform, then paste it here. You pay OpenAI for usage, separately from a ChatGPT subscription.", ref row);
            else
            {
                Note(rect, string.Format("Example to enter:\n{0}\nCopy this whole string, including every hyphen. Do not include quotes, model:, or a URL.", Api.YuiDirectOpenAiClient.DefaultModel), ref row);
                Note(rect, "To change models, copy the complete Model ID from the official list, replace this field, then Save at the top. Include any date suffix. Choose Responses API + Structured outputs support; check image input and Web search if needed.", ref row);
            }
            if (!backend)
            {
                var caption = apiKey ? "Create a key on OpenAI ↗" : "OpenAI official model list ↗";
                ModernButton(rect, "OfficialHelpLink", caption, () => Application.OpenURL(apiKey
                    ? "https://platform.openai.com/api-keys" : "https://developers.openai.com/api/docs/models"));
                Full(rect, "OfficialHelpLink", caption, ref row);
            }
            if (!backend && !apiKey)
            {
                ModernButton(rect, "RestoreDefault", "Restore default model", () => {
                    if (openAiModelInput != null) openAiModelInput.text = Api.YuiDirectOpenAiClient.DefaultModel;
                    CloseSettingsHelp();
                });
                Full(rect, "RestoreDefault", "Restore default model", ref row);
            }
            rect.sizeDelta = new Vector2(width, row + 16);
            var localSource = panel.InverseTransformPoint(source.position);
            var top = Mathf.Min(localSource.y - 36, panel.rect.yMax - 24);
            top = Mathf.Max(top, panel.rect.yMin + rect.rect.height + 24);
            rect.anchoredPosition = new Vector2(0, top);
            foreach (var button in rect.GetComponentsInChildren<Button>())
            {
                if (button.targetGraphic is Image image) { image.color = SettingsSurface; RoundSurface(image); }
            }
        }
    }
}
