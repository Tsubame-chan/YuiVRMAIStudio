using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiHelpOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject helpRoot;
        [SerializeField] private Button helpButton;
        [SerializeField] private Button closeButton;

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
            SetText(panel.Find("Subtitle"), "会話、音声、画像、カメラ、VRM、表示、記憶をまとめて扱うローカルAIアバターです。Sは履歴を残さない会話モードです。", 13, FontStyle.Normal);
            SetAnchors(panel.Find("Subtitle"), new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.90f));
            ReflowCard(panel, "TalkCard", new Vector2(0.06f, 0.66f), new Vector2(0.94f, 0.80f),
                "話す", "Messageに入力してGo。Recは音声入力です。",
                "マイクはSettings > Micで選択し、Mic Testで入力レベルを確認できます。");
            ReflowCard(panel, "VisionCard", new Vector2(0.06f, 0.50f), new Vector2(0.94f, 0.64f),
                "見せる", "Imgは画像ファイル、Lookは選択中のカメラ画像を送ります。",
                "Look用カメラはSettings > Camera > Deviceで選びます。");
            ReflowCard(panel, "AvatarCard", new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.48f),
                "VRM", "AvatarでUnityChanまたはCustom VRMを選びます。",
                "Load VRMはVRM 1.0/0.xの.vrmファイル向けです。VRChat SDKのprefabやunitypackageは直接読み込めません。");
            ReflowCard(panel, "ViewerCard", new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.32f),
                "眺める", "Xで会話パネルを隠すと、ドラッグ/ホイールで表示調整できます。",
                "Settings > ViewでAuto、Adjust View、Cam 1-4の保存/削除ができます。");
            ReflowCard(panel, "SettingsCard", new Vector2(0.06f, 0.045f), new Vector2(0.94f, 0.16f),
                "設定", "Voice、Window、Character、Custom Note、Clear Historyを下へスクロールして設定します。",
                "Applyで反映。Windowは小/中/大/FHDの縦長プリセットです。");
            var oldFooter = panel.Find("Footer");
            if (oldFooter != null)
            {
                oldFooter.gameObject.SetActive(false);
            }
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
            SetAnchors(card.Find("Title"), new Vector2(0.04f, 0.58f), new Vector2(0.26f, 0.94f));
            SetAnchors(card.Find("Body"), new Vector2(0.29f, 0.52f), new Vector2(0.96f, 0.94f));
            SetAnchors(card.Find("Example"), new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.46f));
            SetText(card.Find("Title"), title, 15, FontStyle.Bold);
            SetText(card.Find("Body"), body, 12, FontStyle.Normal);
            SetText(card.Find("Example"), example, 11, FontStyle.Normal);
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
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
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
