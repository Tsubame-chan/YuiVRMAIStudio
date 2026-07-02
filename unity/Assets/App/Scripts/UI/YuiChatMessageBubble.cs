using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiChatMessageBubble : MonoBehaviour
    {
        [SerializeField] private RectTransform bubbleRect;
        [SerializeField] private LayoutElement bubbleElement;
        [SerializeField] private Image background;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text bodyText;
        [SerializeField] private RectTransform actionsRect;
        [SerializeField] private Button copyButton;
        [SerializeField] private Text copyButtonText;
        [SerializeField] private Button linksButton;
        [SerializeField] private Text linksButtonText;
        [SerializeField] private RectTransform linksRect;

        private readonly List<GameObject> linkRows = new List<GameObject>();
        private static Sprite pillSprite;
        private bool linksExpanded;
        private string copyText = string.Empty;
        private IReadOnlyList<YuiChatLink> currentLinks = System.Array.Empty<YuiChatLink>();

        public RectTransform RectTransform => transform as RectTransform;

        public static YuiChatMessageBubble CreateTemplate(Font font, Sprite bubbleSprite)
        {
            var root = new GameObject("YuiChatMessageBubbleTemplate", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var view = root.AddComponent<YuiChatMessageBubble>();
            view.EnsureStructure(font, bubbleSprite);
            root.SetActive(false);
            return view;
        }

        public void Bind(
            string speaker,
            string text,
            float width,
            bool alignRight,
            Color backgroundColor,
            Color speakerColor,
            Color bodyColor,
            Font font,
            Sprite bubbleSprite,
            string originalText = null,
            IReadOnlyList<YuiChatLink> links = null)
        {
            EnsureStructure(font, bubbleSprite);
            copyText = originalText ?? text ?? string.Empty;
            currentLinks = links ?? System.Array.Empty<YuiChatLink>();
            linksExpanded = false;

            if (speakerText != null)
            {
                speakerText.text = speaker;
                speakerText.color = speakerColor;
            }

            if (bodyText != null)
            {
                bodyText.text = text;
                bodyText.color = bodyColor;
            }

            if (background != null)
            {
                background.color = backgroundColor;
            }

            var rowLayout = GetComponent<HorizontalLayoutGroup>();
            if (rowLayout != null)
            {
                rowLayout.childAlignment = alignRight ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            }

            if (bubbleRect != null)
            {
                bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            }

            if (bubbleElement != null)
            {
                bubbleElement.preferredWidth = width;
                bubbleElement.flexibleWidth = 0f;
            }

            BindActionButtons(font);
        }

        private void EnsureStructure(Font font, Sprite bubbleSprite)
        {
            EnsureRootLayout();

            if (bubbleRect == null)
            {
                bubbleRect = FindChildRect(transform, "Bubble");
            }

            if (bubbleRect == null)
            {
                var bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
                bubble.transform.SetParent(transform, false);
                bubbleRect = bubble.GetComponent<RectTransform>();
            }

            background = background != null ? background : bubbleRect.GetComponent<Image>();
            if (background == null)
            {
                background = bubbleRect.gameObject.AddComponent<Image>();
            }
            if (bubbleSprite != null)
            {
                background.sprite = bubbleSprite;
                background.type = Image.Type.Sliced;
            }

            var bubbleLayout = bubbleRect.GetComponent<VerticalLayoutGroup>();
            if (bubbleLayout == null)
            {
                bubbleLayout = bubbleRect.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            bubbleLayout.padding = new RectOffset(16, 16, 12, 12);
            bubbleLayout.spacing = 7f;
            bubbleLayout.childAlignment = TextAnchor.UpperLeft;
            bubbleLayout.childControlWidth = true;
            bubbleLayout.childControlHeight = true;
            bubbleLayout.childForceExpandWidth = true;
            bubbleLayout.childForceExpandHeight = false;

            var fitter = bubbleRect.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = bubbleRect.gameObject.AddComponent<ContentSizeFitter>();
            }
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            bubbleElement = bubbleElement != null ? bubbleElement : bubbleRect.GetComponent<LayoutElement>();
            if (bubbleElement == null)
            {
                bubbleElement = bubbleRect.gameObject.AddComponent<LayoutElement>();
            }

            speakerText = speakerText != null ? speakerText : FindChildText(bubbleRect, "Speaker");
            if (speakerText == null)
            {
                speakerText = CreateText("Speaker", bubbleRect, YuiChatLogStyle.SpeakerFontSize, FontStyle.Bold, font);
            }

            bodyText = bodyText != null ? bodyText : FindChildText(bubbleRect, "Body");
            if (bodyText == null)
            {
                bodyText = CreateText("Body", bubbleRect, YuiChatLogStyle.BodyFontSize, FontStyle.Normal, font);
            }

            speakerText.fontSize = YuiChatLogStyle.SpeakerFontSize;
            bodyText.fontSize = YuiChatLogStyle.BodyFontSize;
            bodyText.lineSpacing = 1.08f;
            ApplyFont(speakerText, font);
            ApplyFont(bodyText, font);
            EnsureActions(font);
        }

        private void EnsureRootLayout()
        {
            var rowLayout = GetComponent<HorizontalLayoutGroup>();
            if (rowLayout == null)
            {
                rowLayout = gameObject.AddComponent<HorizontalLayoutGroup>();
            }
            rowLayout.padding = new RectOffset(0, 0, 0, 0);
            rowLayout.spacing = 0f;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var rowElement = GetComponent<LayoutElement>();
            if (rowElement == null)
            {
                rowElement = gameObject.AddComponent<LayoutElement>();
            }
            rowElement.minHeight = 56f;
            rowElement.flexibleWidth = 1f;
        }

        private static Text CreateText(string name, Transform parent, int size, FontStyle style, Font font)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = name == "Body" ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
            text.supportRichText = false;
            ApplyFont(text, font);
            return text;
        }

        private void EnsureActions(Font font)
        {
            if (actionsRect == null)
            {
                actionsRect = FindChildRect(bubbleRect, "Actions");
            }

            if (actionsRect == null)
            {
                var actions = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                actions.transform.SetParent(bubbleRect, false);
                actionsRect = actions.GetComponent<RectTransform>();
            }

            var actionsLayout = actionsRect.GetComponent<HorizontalLayoutGroup>();
            if (actionsLayout == null)
            {
                actionsLayout = actionsRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            }
            actionsLayout.spacing = 8f;
            actionsLayout.childAlignment = TextAnchor.MiddleLeft;
            actionsLayout.childControlWidth = false;
            actionsLayout.childControlHeight = true;
            actionsLayout.childForceExpandWidth = false;
            actionsLayout.childForceExpandHeight = false;

            var actionsElement = actionsRect.GetComponent<LayoutElement>();
            if (actionsElement == null)
            {
                actionsElement = actionsRect.gameObject.AddComponent<LayoutElement>();
            }
            actionsElement.minHeight = 26f;
            actionsElement.preferredHeight = 26f;

            copyButton = copyButton != null ? copyButton : FindChildButton(actionsRect, "CopyButton");
            if (copyButton == null)
            {
                copyButton = CreateActionButton(
                    "CopyButton",
                    actionsRect,
                    "Copy",
                    font,
                    56f,
                    new Color(1f, 1f, 1f, 0.08f),
                    new Color(0.86f, 0.9f, 1f, 0.92f));
            }
            copyButtonText = copyButtonText != null ? copyButtonText : copyButton.GetComponentInChildren<Text>(true);

            linksButton = linksButton != null ? linksButton : FindChildButton(actionsRect, "LinksButton");
            if (linksButton == null)
            {
                linksButton = CreateActionButton(
                    "LinksButton",
                    actionsRect,
                    "Sources",
                    font,
                    92f,
                    new Color(0.38f, 0.58f, 0.95f, 0.22f),
                    new Color(0.9f, 0.95f, 1f, 0.96f));
            }
            linksButtonText = linksButtonText != null ? linksButtonText : linksButton.GetComponentInChildren<Text>(true);

            if (linksRect == null)
            {
                linksRect = FindChildRect(bubbleRect, "Links");
            }

            if (linksRect == null)
            {
                var linksObject = new GameObject("Links", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
                linksObject.transform.SetParent(bubbleRect, false);
                linksRect = linksObject.GetComponent<RectTransform>();
            }

            var linksLayout = linksRect.GetComponent<VerticalLayoutGroup>();
            if (linksLayout == null)
            {
                linksLayout = linksRect.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            linksLayout.spacing = 6f;
            linksLayout.childAlignment = TextAnchor.UpperLeft;
            linksLayout.childControlWidth = true;
            linksLayout.childControlHeight = true;
            linksLayout.childForceExpandWidth = true;
            linksLayout.childForceExpandHeight = false;
        }

        private void BindActionButtons(Font font)
        {
            var hasText = !string.IsNullOrWhiteSpace(copyText);
            var hasLinks = currentLinks != null && currentLinks.Count > 0;

            if (actionsRect != null)
            {
                actionsRect.gameObject.SetActive(hasText || hasLinks);
            }

            if (copyButton != null)
            {
                copyButton.gameObject.SetActive(hasText);
                copyButton.onClick.RemoveAllListeners();
                copyButton.onClick.AddListener(CopyCurrentText);
            }

            if (copyButtonText != null)
            {
                copyButtonText.text = "Copy";
                ApplyFont(copyButtonText, font);
            }

            if (linksButton != null)
            {
                linksButton.gameObject.SetActive(hasLinks);
                linksButton.onClick.RemoveAllListeners();
                linksButton.onClick.AddListener(ToggleLinks);
            }

            if (linksButtonText != null)
            {
                linksButtonText.text = hasLinks ? $"Sources {currentLinks.Count}" : "Sources";
                ApplyFont(linksButtonText, font);
            }

            RebuildLinkRows(font);
        }

        private void CopyCurrentText()
        {
            GUIUtility.systemCopyBuffer = copyText ?? string.Empty;
            if (copyButtonText != null)
            {
                copyButtonText.text = "Done";
            }
        }

        private void ToggleLinks()
        {
            linksExpanded = !linksExpanded;
            if (linksRect != null)
            {
                linksRect.gameObject.SetActive(linksExpanded);
            }

            Canvas.ForceUpdateCanvases();
            if (bubbleRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRect);
            }
        }

        private void RebuildLinkRows(Font font)
        {
            foreach (var row in linkRows)
            {
                if (row != null)
                {
                    Destroy(row);
                }
            }
            linkRows.Clear();

            var hasLinks = currentLinks != null && currentLinks.Count > 0;
            if (linksRect != null)
            {
                linksRect.gameObject.SetActive(hasLinks && linksExpanded);
            }

            if (!hasLinks || linksRect == null)
            {
                return;
            }

            foreach (var link in currentLinks)
            {
                var row = CreateLinkRow(linksRect, link, font);
                linkRows.Add(row);
            }
        }

        private static Button CreateActionButton(
            string name,
            Transform parent,
            string label,
            Font font,
            float preferredWidth,
            Color backgroundColor,
            Color textColor)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            root.transform.SetParent(parent, false);

            var image = root.GetComponent<Image>();
            image.color = backgroundColor;
            image.sprite = ResolvePillSprite();
            image.type = Image.Type.Sliced;

            var element = root.GetComponent<LayoutElement>();
            element.minWidth = preferredWidth;
            element.preferredWidth = preferredWidth;
            element.minHeight = 26f;
            element.preferredHeight = 26f;

            var button = root.GetComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText("Label", root.transform, YuiChatLogStyle.ActionFontSize, FontStyle.Bold, font);
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = textColor;
            var textRect = text.transform as RectTransform;
            if (textRect != null)
            {
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
            }

            return button;
        }

        private static GameObject CreateLinkRow(Transform parent, YuiChatLink link, Font font)
        {
            var row = new GameObject("LinkRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 5, 5);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var element = row.GetComponent<LayoutElement>();
            element.minHeight = 34f;
            element.preferredHeight = 34f;

            var rowImage = row.AddComponent<Image>();
            rowImage.color = new Color(1f, 1f, 1f, 0.075f);
            rowImage.sprite = ResolvePillSprite();
            rowImage.type = Image.Type.Sliced;

            var openButton = CreateActionButton(
                "OpenLinkButton",
                row.transform,
                link.CompactLabel,
                font,
                190f,
                new Color(0f, 0f, 0f, 0f),
                new Color(0.9f, 0.95f, 1f, 0.98f));
            var openElement = openButton.GetComponent<LayoutElement>();
            openElement.preferredWidth = 210f;
            openElement.flexibleWidth = 1f;
            var openLabel = openButton.GetComponentInChildren<Text>(true);
            if (openLabel != null)
            {
                openLabel.text = link.CompactLabel;
                openLabel.alignment = TextAnchor.MiddleLeft;
            }
            openButton.onClick.AddListener(() => Application.OpenURL(link.Url));

            var copyButton = CreateActionButton(
                "CopyLinkButton",
                row.transform,
                "Copy",
                font,
                54f,
                new Color(1f, 1f, 1f, 0.08f),
                new Color(0.86f, 0.9f, 1f, 0.92f));
            var copyElement = copyButton.GetComponent<LayoutElement>();
            copyElement.preferredWidth = 54f;
            copyButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = link.Url);

            return row;
        }

        private static void ApplyFont(Text text, Font font)
        {
            if (text != null && font != null)
            {
                text.font = font;
            }
        }

        private static Sprite ResolvePillSprite()
        {
            if (pillSprite == null)
            {
                pillSprite = YuiChatLogStyle.CreateRoundedBubbleSprite();
            }

            return pillSprite;
        }

        private static RectTransform FindChildRect(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == childName)
                {
                    return child as RectTransform;
                }
            }

            return null;
        }

        private static Text FindChildText(Transform parent, string childName)
        {
            var child = FindChildRect(parent, childName);
            return child != null ? child.GetComponent<Text>() : null;
        }

        private static Button FindChildButton(Transform parent, string childName)
        {
            var child = FindChildRect(parent, childName);
            return child != null ? child.GetComponent<Button>() : null;
        }
    }
}
