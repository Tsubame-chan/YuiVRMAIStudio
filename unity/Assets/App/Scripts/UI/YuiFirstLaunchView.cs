using System;
using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    // Native uGUI implementation of docs/design/first_launch_20260924.
    public sealed class YuiFirstLaunchView : MonoBehaviour
    {
        private RectTransform safe;
        private Text heading, detail, percent, footnote;
        private Image fill;
        private GameObject track;
        private Button action;
        private Action actionHandler;
        private GameObject consent;
        private Rect lastSafe;
        private int lastWidth, lastHeight;
        private bool Japanese => YuiUiLocalization.Language == "ja";
        public static YuiFirstLaunchView Create()
        {
            var root = new GameObject("YuiFirstLaunch", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844); scaler.matchWidthOrHeight = 0;
            var view = root.AddComponent<YuiFirstLaunchView>(); view.Build(); return view;
        }
        private void Build()
        {
            var background = Panel("Background", transform, new Color32(20, 20, 27, 255)); Stretch(background.rectTransform);
            safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>(); safe.SetParent(transform, false);
            ApplySafeArea();
            Label("Brand", "Yui VRM AI Studio", 17, .94f, 30, YuiUiTheme.Muted);
            var iconFrame = Panel("IconFrame", safe, Color.white); YuiUiTheme.Round(iconFrame);
            Place(iconFrame.rectTransform, .76f, 112, 112);
            iconFrame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var icon = new GameObject("Cards", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            icon.transform.SetParent(iconFrame.transform, false); icon.texture = Resources.Load<Texture2D>("YuiBrand/Cards"); icon.raycastTarget = false;
            Stretch(icon.rectTransform);
            var card = Panel("PreparationCard", safe, new Color32(34, 33, 43, 255)); YuiUiTheme.Round(card); Place(card.rectTransform, .32f, -1, 170);
            heading = Label("Status", "", 20, .36f, 54, YuiUiTheme.Text);
            detail = Label("Detail", "", 14, .29f, 48, YuiUiTheme.Muted);
            var rail = Panel("ProgressTrack", safe, new Color32(68, 62, 81, 255)); YuiUiTheme.Round(rail); Place(rail.rectTransform, .24f, -1, 6, 44);
            track = rail.gameObject;
            fill = Panel("Progress", rail.transform, YuiUiTheme.Accent); YuiUiTheme.Round(fill); Stretch(fill.rectTransform);
            percent = Label("Percent", "", 14, .18f, 28, YuiUiTheme.Muted);
            var buttonImage = Panel("Continue", safe, YuiUiTheme.Accent); YuiUiTheme.Round(buttonImage); Place(buttonImage.rectTransform, .135f, -1, 54);
            action = buttonImage.gameObject.AddComponent<Button>(); action.targetGraphic = buttonImage;
            var buttonLabel = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); buttonLabel.transform.SetParent(action.transform, false);
            Stretch(buttonLabel.rectTransform); Configure(buttonLabel, "", 18, new Color32(41, 30, 60, 255));
            action.onClick.AddListener(() => actionHandler?.Invoke());
            footnote = Label("Footnote", "", 12, .045f, 44, YuiUiTheme.Muted);
            ShowDownloading(-1);
        }
        public void ShowSplash()
        {
            foreach (Transform child in safe)
                child.gameObject.SetActive(child.name == "Brand" || child.name == "IconFrame");
            Place((RectTransform)safe.Find("IconFrame"), .5f, 144, 144);
        }
        public void ShowDownloading(float value, bool paused = false)
        {
            HideConsent();
            heading.text = Japanese ? (paused ? "接続を待っています" : "会話データを準備しています") : (paused ? "Waiting for a connection" : "Preparing your conversations");
            detail.text = Japanese ? (paused ? "通信が戻ると、取得を再開します。" : "Appleから必要なデータを取得しています。") : (paused ? "The download will resume when connected." : "Downloading the data from Apple.");
            track.SetActive(true); percent.gameObject.SetActive(true); action.gameObject.SetActive(false);
            var fraction = Mathf.Clamp01(value);
            fill.rectTransform.anchorMax = new Vector2(fraction, 1);
            percent.text = value < 0 ? (Japanese ? "準備中" : "Preparing…") : $"{Mathf.FloorToInt(fraction * 100)}% / 100%";
            footnote.text = Japanese ? "初回のみ通信が必要です。Wi-Fi推奨。\n準備が終われば、オフラインで話せます。" : "Internet is needed for setup. Wi-Fi is recommended.\nOnce ready, you can chat offline.";
        }
        public void ShowReady(Action onContinue)
        {
            HideConsent();
            heading.text = Japanese ? "会話の準備ができました" : "Ready to talk";
            detail.text = Japanese ? "あなたのキャラクターで、\n話しはじめましょう。" : "Start a conversation\nwith your character.";
            track.SetActive(false); percent.gameObject.SetActive(false);
            footnote.text = Japanese ? "この端末のAIで、\nオフラインでも会話できます。" : "Chat with the AI on this device,\neven when you’re offline.";
            SetAction(Japanese ? "はじめる" : "Get started", onContinue);
        }
        public void ShowFailure(Action onRetry)
        {
            HideConsent();
            heading.text = Japanese ? "もう一度、準備しましょう" : "Let’s try again";
            detail.text = Japanese ? "通信と空き容量を確認して、\nもう一度お試しください。" : "Check your connection and free storage,\nthen try again.";
            track.SetActive(false); percent.gameObject.SetActive(false);
            SetAction(Japanese ? "もう一度試す" : "Try again", onRetry);
        }
        public void ShowDeferred(Action onPrepare)
        {
            HideConsent();
            heading.text = Japanese ? "会話データの準備" : "Set up your conversations";
            detail.text = Japanese ? "Wi-Fiにつないでから、\n準備をはじめられます。" : "You can start setup\nonce you’re connected to Wi-Fi.";
            track.SetActive(false); percent.gameObject.SetActive(false);
            footnote.text = Japanese ? "今はダウンロードしません。\nアプリを閉じて、あとで準備できます。" : "No download has started.\nYou can close the app and set up later.";
            SetAction(Japanese ? "準備をはじめる" : "Start setup", onPrepare);
        }
        public void ShowDownloadConsent(Action onStart, Action onLater)
        {
            ShowDeferred(() => ShowDownloadConsent(onStart, onLater));
            var shade = Panel("DownloadConsent", safe, new Color(0, 0, 0, .72f)); Stretch(shade.rectTransform);
            consent = shade.gameObject;
            var card = Panel("Dialog", shade.transform, new Color32(34, 33, 43, 255));
            YuiUiTheme.Round(card); Place(card.rectTransform, .45f, -1, 360, 20);
            var title = DialogLabel(card.transform, "Title", Japanese ? "初回ダウンロードの確認" : "Before your first download", 20);
            Place(title.rectTransform, .86f, -1, 60, 18);
            var body = DialogLabel(card.transform, "Message", Japanese
                ? "約3.2 GBの会話データを取得します。\nWi-Fiでの通信を推奨します。\n準備が終われば、オフラインで話せます。"
                : "Download about 3.2 GB of conversation data.\nWi-Fi is recommended.\nOnce ready, you can chat offline.", 16);
            Place(body.rectTransform, .56f, -1, 130, 20);
            DialogButton(card.transform, "StartDownload", Japanese ? "ダウンロードを開始" : "Start download", .25f, YuiUiTheme.Accent, new Color32(41,30,60,255), () => { HideConsent(); onStart?.Invoke(); });
            DialogButton(card.transform, "Later", Japanese ? "あとで" : "Later", .09f, new Color32(51,47,62,255), YuiUiTheme.Text, () => { HideConsent(); onLater?.Invoke(); });
        }
        private void HideConsent()
        {
            if (consent == null) return;
            consent.SetActive(false);
            if (Application.isPlaying) Destroy(consent); else DestroyImmediate(consent);
            consent = null;
        }
        private static Text DialogLabel(Transform parent, string name, string value, int size)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false); Configure(label, value, size, YuiUiTheme.Text); return label;
        }
        private static void DialogButton(Transform parent, string name, string value, float y, Color background, Color foreground, Action callback)
        {
            var panel = Panel(name, parent, background); YuiUiTheme.Round(panel); Place(panel.rectTransform, y, -1, 46, 20);
            var button = panel.gameObject.AddComponent<Button>(); button.targetGraphic = panel;
            var label = DialogLabel(panel.transform, "Label", value, 17); Stretch(label.rectTransform); label.color = foreground;
            button.onClick.AddListener(() => callback());
        }
        private void SetAction(string label, Action handler)
        { actionHandler = handler; action.GetComponentInChildren<Text>().text = label; action.gameObject.SetActive(true); }
        private void Update()
        { if (Screen.safeArea != lastSafe || Screen.width != lastWidth || Screen.height != lastHeight) ApplySafeArea(); }
        private void ApplySafeArea()
        {
            lastSafe = Screen.safeArea; lastWidth = Screen.width; lastHeight = Screen.height;
            safe.anchorMin = new Vector2(lastSafe.xMin / Mathf.Max(1,lastWidth), lastSafe.yMin / Mathf.Max(1,lastHeight));
            safe.anchorMax = new Vector2(lastSafe.xMax / Mathf.Max(1,lastWidth), lastSafe.yMax / Mathf.Max(1,lastHeight));
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }
        private Text Label(string name, string value, int size, float y, float height, Color color)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>(); text.transform.SetParent(safe, false);
            Place(text.rectTransform, y, -1, height); Configure(text, value, size, color); return text;
        }
        private static void Configure(Text text, string value, int size, Color color)
        { text.text = value; text.font = YuiUiTypography.Regular; text.fontSize = size; text.color = color; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; text.supportRichText = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; }
        private static Image Panel(string name, Transform parent, Color color)
        { var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>(); image.transform.SetParent(parent, false); image.color = color; return image; }
        private static void Place(RectTransform rect, float y, float width, float height, float margin = 28)
        {
            rect.anchorMin = new Vector2(width < 0 ? 0 : .5f, y); rect.anchorMax = new Vector2(width < 0 ? 1 : .5f, y);
            rect.pivot = new Vector2(.5f,.5f); rect.sizeDelta = new Vector2(width < 0 ? -margin * 2 : width, height); rect.anchoredPosition = Vector2.zero;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
