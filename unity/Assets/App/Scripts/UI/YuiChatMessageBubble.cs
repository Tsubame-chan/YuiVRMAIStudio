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
            Sprite bubbleSprite)
        {
            EnsureStructure(font, bubbleSprite);

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
            bubbleLayout.padding = new RectOffset(14, 14, 10, 10);
            bubbleLayout.spacing = 4f;
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
                speakerText = CreateText("Speaker", bubbleRect, 11, FontStyle.Bold, font);
            }

            bodyText = bodyText != null ? bodyText : FindChildText(bubbleRect, "Body");
            if (bodyText == null)
            {
                bodyText = CreateText("Body", bubbleRect, 13, FontStyle.Normal, font);
            }

            ApplyFont(speakerText, font);
            ApplyFont(bodyText, font);
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

        private static void ApplyFont(Text text, Font font)
        {
            if (text != null && font != null)
            {
                text.font = font;
            }
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
    }
}
