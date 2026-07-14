using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiHelpOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject helpRoot;
        [SerializeField] private Button helpButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private string backendUrl = "http://127.0.0.1:8000";

        private string providerStatusBody = "Backendの接続状態を確認中です。";
        private string providerStatusDetail = "少し待ってからもう一度Helpを開いてください。";

        private void Awake()
        {
            if (helpButton != null)
            {
                helpButton.onClick.AddListener(Show);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }

            YuiToolbarIconUtility.ApplyHelpIcon(helpButton);
            Hide();
        }

        private void OnDestroy()
        {
            if (helpButton != null)
            {
                helpButton.onClick.RemoveListener(Show);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }
        }

        public void Configure(GameObject root, Button openButton, Button dismissButton)
        {
            if (helpButton != null)
            {
                helpButton.onClick.RemoveListener(Show);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }

            helpRoot = root;
            helpButton = openButton;
            closeButton = dismissButton;

            if (helpButton != null)
            {
                helpButton.onClick.AddListener(Show);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }

            YuiToolbarIconUtility.ApplyHelpIcon(helpButton);
            Hide();
        }

        public void Show()
        {
            if (helpRoot != null)
            {
                helpRoot.SetActive(true);
                helpRoot.transform.SetAsLastSibling();
            }

            EnsureOverlayCanvas(helpRoot, 5010);
            ApplyResponsiveLayout();
            _ = RefreshProviderStatusAsync();
            if (helpRoot != null)
            {
                helpRoot.SetActive(true);
                helpRoot.transform.SetAsLastSibling();
            }

            Canvas.ForceUpdateCanvases();
        }

        public void Hide()
        {
            if (helpRoot != null)
            {
                helpRoot.SetActive(false);
            }
        }

        private void ApplyResponsiveLayout()
        {
            if (helpRoot == null)
            {
                return;
            }

            var rootRect = helpRoot.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
            }

            var rootImage = helpRoot.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.color = new Color(0.02f, 0.025f, 0.03f, 0.72f);
            }

            var panel = helpRoot.transform.Find("Panel");
            if (panel == null)
            {
                return;
            }

            SetAnchors(panel, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.94f));
            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.075f, 0.08f, 0.095f, 1f);
            }

            EnsureOpaqueBacking(panel);

            SetAnchors(panel.Find("Title"), new Vector2(0.06f, 0.91f), new Vector2(0.72f, 0.985f));
            SetAnchors(closeButton != null ? closeButton.transform : panel.Find("CloseButton"), new Vector2(0.86f, 0.91f), new Vector2(0.96f, 0.985f));
            SetText(panel.Find("Title"), "Yuiでできること", 22, FontStyle.Bold);
            SetText(panel.Find("Subtitle"), "会話、音声、画像、カメラ、VRM、表示、記憶をまとめて扱うAIアバターです。シークレットモードでは履歴を残さず会話できます。", 16, FontStyle.Normal);
            SetAnchors(panel.Find("Subtitle"), new Vector2(0.06f, 0.815f), new Vector2(0.94f, 0.895f));
            ReflowCard(panel, "TalkCard", new Vector2(0.06f, 0.66f), new Vector2(0.94f, 0.80f),
                "接続状態", providerStatusBody, providerStatusDetail);
            ReflowCard(panel, "VisionCard", new Vector2(0.06f, 0.50f), new Vector2(0.94f, 0.64f),
                "AIモード", "ローカルAIはオフライン優先で軽く使えます。API Modeは通信とAPI利用量が発生します。",
                "高精度な画像理解、長い文脈、複雑な推論はAPI向きです。Local AI時はAPIへ自動切替しません。");
            ReflowCard(panel, "AvatarCard", new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.48f),
                "Direct API", "BackendなしでAPIチャットとAPI画像理解を使えます。声はTTS Modeで別に選びます。",
                "できないこと: Realtime会話/翻訳、メモリDB、Web検索、外部ツール、Backend TTSにはBackendが必要です。");
            ReflowCard(panel, "ViewerCard", new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.32f),
                "話す/見せる", "Message or taskに入力してSend。Micは音声入力、Imageは画像、Cameraは選択中のカメラ画像です。",
                "API Modeでは画像をAPI LLMへ直接渡します。Local AIでは端末内の軽量Visionを使います。");
            ReflowCard(panel, "SettingsCard", new Vector2(0.06f, 0.045f), new Vector2(0.94f, 0.16f),
                "VRMと声", "AvatarでUnityChanまたはCustom VRMを選びます。声はTTS ModeでAIモードとは別に選べます。",
                "Load VRMは.vrmファイル向けです。Backend URLはYui backendだけを指定します。");
            var oldFooter = panel.Find("Footer");
            if (oldFooter != null)
            {
                oldFooter.gameObject.SetActive(false);
            }
        }

        private async System.Threading.Tasks.Task RefreshProviderStatusAsync()
        {
            try
            {
                var savedBackendUrl = PlayerPrefs.GetString(YuiPrefsKeys.BackendUrl, backendUrl);
                var client = new YuiBackendClient(savedBackendUrl);
                try
                {
                    var status = await client.GetProviderStatusAsync();
                    var snapshot = CapabilitySnapshotFromProviderStatus(status, backendReachable: true);
                    providerStatusBody = YuiCapabilityDiagnostics.FormatBody(snapshot);
                    providerStatusDetail = YuiCapabilityDiagnostics.FormatDetail(snapshot);
                }
                catch (YuiBackendException ex) when (ex.StatusCode == 404)
                {
                    var health = await client.GetHealthAsync();
                    var snapshot = CapabilitySnapshotFromHealth(health, backendReachable: true);
                    providerStatusBody = YuiCapabilityDiagnostics.FormatBody(snapshot);
                    providerStatusDetail = "Backendは起動していますが、接続診断APIが古い可能性があります。Backendを再起動してください。";
                }
            }
            catch (System.Exception ex)
            {
                var snapshot = CapabilitySnapshotFromProviderStatus(null, backendReachable: false);
                providerStatusBody = YuiCapabilityDiagnostics.FormatBody(snapshot);
                providerStatusDetail = $"Backendに接続できません。ローカル機能で継続できますが、Realtime/Backend TTSにはローカルサービスが必要です: {ShortMessage(ex.Message)}";
            }

            ApplyResponsiveLayout();
            Canvas.ForceUpdateCanvases();
        }

        private static string FormatProviderStatusBody(ProviderStatusResponse status)
        {
            return YuiCapabilityDiagnostics.FormatBody(CapabilitySnapshotFromProviderStatus(status, backendReachable: status != null));
        }

        private static string FormatProviderStatusDetail(ProviderStatusResponse status)
        {
            return YuiCapabilityDiagnostics.FormatDetail(CapabilitySnapshotFromProviderStatus(status, backendReachable: status != null));
        }

        private static string FormatHealthStatusBody(HealthResponse health)
        {
            return YuiCapabilityDiagnostics.FormatBody(CapabilitySnapshotFromHealth(health, backendReachable: health != null));
        }

        private static YuiCapabilitySnapshot CapabilitySnapshotFromProviderStatus(ProviderStatusResponse status, bool backendReachable)
        {
            return YuiCapabilityMatrix.FromProviderStatus(
                status,
                backendReachable,
                YuiVoicevoxCoreBridge.IsSupported,
                localChatAvailable: true,
                directOpenAiConfigured: !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(YuiPrefsKeys.OpenAiApiKey, "")));
        }

        private static YuiCapabilitySnapshot CapabilitySnapshotFromHealth(HealthResponse health, bool backendReachable)
        {
            return YuiCapabilityMatrix.FromHealth(
                health,
                backendReachable,
                YuiVoicevoxCoreBridge.IsSupported,
                localChatAvailable: true,
                directOpenAiConfigured: !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(YuiPrefsKeys.OpenAiApiKey, "")));
        }

        private static string FormatStatusLine(string label, string status)
        {
            return $"{label}: {StatusBadge(status)}";
        }

        private static string ProviderStatus(ProviderStatusResponse status, string key)
        {
            if (status?.Providers == null || !status.Providers.TryGetValue(key, out var item) || item == null)
            {
                return "unknown";
            }

            return item.Status ?? "unknown";
        }

        private static bool HealthBool(HealthResponse health, string key)
        {
            return health?.Providers != null
                && health.Providers.TryGetValue(key, out var value)
                && value is bool configured
                && configured;
        }

        private static bool HealthFeature(HealthResponse health, string key)
        {
            return health?.Features != null
                && health.Features.TryGetValue(key, out var enabled)
                && enabled;
        }

        private static string StatusBadge(string status)
        {
            var label = StatusText(status);
            var color = StatusColor(status);
            return $"<color={color}><b>{label}</b></color>";
        }

        private static string StatusText(string status)
        {
            switch (status)
            {
                case "ok":
                case "configured":
                    return "OK";
                case "missing_key":
                case "offline":
                case "error":
                    return "NG";
                case "not_configured":
                case "disabled":
                    return "--";
                case "degraded":
                    return "WARN";
                default:
                    return "--";
            }
        }

        private static string StatusColor(string status)
        {
            switch (status)
            {
                case "ok":
                case "configured":
                    return "#7FE391";
                case "missing_key":
                case "offline":
                case "error":
                    return "#FF8A80";
                case "degraded":
                    return "#FFD166";
                default:
                    return "#AEB7C4";
            }
        }

        private static string ShortMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "詳細不明";
            }

            return message.Length <= 80 ? message : message.Substring(0, 80) + "...";
        }

        private static void ReflowCard(Transform panel, string name, Vector2 anchorMin, Vector2 anchorMax, string title, string body, string example)
        {
            var card = panel.Find(name);
            if (card == null)
            {
                var cardObject = new GameObject(name, typeof(RectTransform), typeof(Image));
                cardObject.transform.SetParent(panel, false);
                cardObject.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.17f, 0.95f);
                card = cardObject.transform;
                CreateCardText(card, "Title");
                CreateCardText(card, "Body");
                CreateCardText(card, "Example");
            }

            card.gameObject.SetActive(true);
            SetAnchors(card, anchorMin, anchorMax);
            SetAnchors(card.Find("Title"), new Vector2(0.04f, 0.70f), new Vector2(0.96f, 0.94f));
            SetAnchors(card.Find("Body"), new Vector2(0.04f, 0.39f), new Vector2(0.96f, 0.70f));
            SetAnchors(card.Find("Example"), new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.38f));
            SetText(card.Find("Title"), title, 18, FontStyle.Bold);
            SetText(card.Find("Body"), body, 15, FontStyle.Normal);
            SetText(card.Find("Example"), example, 14, FontStyle.Normal);
        }

        private static void CreateCardText(Transform card, string name)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(card, false);
            var text = textObject.GetComponent<Text>();
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Meiryo", "Yu Gothic", "MS Gothic", "Arial" }, 12)
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
        }

        private static void SetAnchors(Transform target, Vector2 anchorMin, Vector2 anchorMax)
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

        private static void SetText(Transform target, string value, int fontSize, FontStyle fontStyle)
        {
            if (target == null)
            {
                return;
            }

            var text = target.GetComponent<Text>();
            if (text == null)
            {
                return;
            }

            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false;
            text.resizeTextMinSize = Mathf.Max(8, fontSize - 4);
            text.resizeTextMaxSize = fontSize;
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

            SetAnchors(backing, Vector2.zero, Vector2.one);
            var image = backing.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.075f, 0.08f, 0.095f, 1f);
                image.raycastTarget = true;
            }

            backing.SetAsFirstSibling();
        }
    }
}
