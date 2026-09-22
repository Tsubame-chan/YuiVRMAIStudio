using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiHelpOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject helpRoot;
        [SerializeField] private Button helpButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private string backendUrl = "http://127.0.0.1:8000";


        private void Awake()
        {
            YuiUiLocalization.Changed += RefreshUiLanguage;
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
            YuiUiLocalization.Changed -= RefreshUiLanguage;
            StopStatusPolling();
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
            previousSelection = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (helpRoot != null)
            {
                helpRoot.SetActive(true);
                helpRoot.transform.SetAsLastSibling();
            }

            EnsureOverlayCanvas(helpRoot, 5010);
            ApplyResponsiveLayout();
            StartStatusPolling();
            if (helpRoot != null)
            {
                helpRoot.SetActive(true);
                helpRoot.transform.SetAsLastSibling();
            }

            Canvas.ForceUpdateCanvases();
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(connectionsTab?.gameObject);
        }

        public void Hide()
        {
            StopStatusPolling();
            if (helpRoot != null)
            {
                helpRoot.SetActive(false);
                if (previousSelection != null && previousSelection.activeInHierarchy)
                    UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(previousSelection);
            }
        }

        private void ApplyResponsiveLayout() => RenderModernHelp();
        private void RefreshUiLanguage()
        {
            if (helpRoot != null && helpRoot.activeSelf) RenderModernHelp();
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
                directOpenAiConfigured: !string.IsNullOrWhiteSpace(YuiApiKeyStore.Read()));
        }

        private static YuiCapabilitySnapshot CapabilitySnapshotFromHealth(HealthResponse health, bool backendReachable)
        {
            return YuiCapabilityMatrix.FromHealth(
                health,
                backendReachable,
                YuiVoicevoxCoreBridge.IsSupported,
                localChatAvailable: true,
                directOpenAiConfigured: !string.IsNullOrWhiteSpace(YuiApiKeyStore.Read()));
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
            text.font = YuiUiTypography.Regular;
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
