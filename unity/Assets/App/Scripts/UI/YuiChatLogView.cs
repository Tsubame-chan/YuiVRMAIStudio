using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiChatLogView : MonoBehaviour
    {
        private const int MaxMessages = 100;

        private readonly List<MessageEntry> messages = new List<MessageEntry>();
        private readonly StringBuilder legacyLogBuilder = new StringBuilder();

        [SerializeField] private YuiChatMessageBubble messageBubblePrefab;
        [SerializeField] private int maxMessages = MaxMessages;

        private Text logText;
        private ScrollRect scrollRect;
        private RectTransform bubbleContent;
        private MessageEntry pendingEntry;
        private Font chatFont;
        private Sprite bubbleSprite;
        private YuiChatMessageBubble runtimeBubbleTemplate;
        private bool bubbleMode;
        private Vector2 lastViewportSize;

        public bool IsEmpty => messages.Count == 0 && legacyLogBuilder.Length == 0;

        public void Configure(Text targetLogText, ScrollRect targetScrollRect)
        {
            logText = targetLogText;
            scrollRect = targetScrollRect;
            chatFont = YuiChatLogStyle.ResolveFont(logText);
            bubbleSprite = bubbleSprite != null ? bubbleSprite : YuiChatLogStyle.CreateRoundedBubbleSprite();
            EnsureRuntimeBubbleTemplate();
            bubbleMode = TryPrepareBubbleMode();
            lastViewportSize = CurrentViewportSize();
            RenderAll();
        }

        public void AppendLog(string speaker, string text)
        {
            var safeSpeaker = string.IsNullOrWhiteSpace(speaker) ? "System" : speaker.Trim();
            var safeText = text ?? string.Empty;
            legacyLogBuilder.AppendLine($"{safeSpeaker}: {safeText}");

            if (!bubbleMode)
            {
                RenderLegacy(null, null);
                return;
            }

            messages.Add(CreateBubble(safeSpeaker, safeText, pending: false));
            TrimOldMessages();
            RebuildLayoutAndScroll();
        }

        public void Clear()
        {
            legacyLogBuilder.Length = 0;
            foreach (var message in messages)
            {
                DestroyEntry(message);
            }
            messages.Clear();
            DestroyEntry(pendingEntry);
            pendingEntry = null;
            RenderAll();
        }

        public void SetPendingLine(string speaker, string text)
        {
            var safeSpeaker = string.IsNullOrWhiteSpace(speaker) ? "System" : speaker.Trim();
            var safeText = text ?? string.Empty;

            if (!bubbleMode)
            {
                RenderLegacy(safeSpeaker, safeText);
                return;
            }

            if (pendingEntry == null)
            {
                pendingEntry = CreateBubble(safeSpeaker, safeText, pending: true);
            }
            else
            {
                pendingEntry.Speaker = safeSpeaker;
                pendingEntry.Text = safeText;
                ApplyEntryStyle(pendingEntry);
            }

            RebuildLayoutAndScroll();
        }

        public void ClearPendingLine()
        {
            if (!bubbleMode)
            {
                RenderLegacy(null, null);
                return;
            }

            DestroyEntry(pendingEntry);
            pendingEntry = null;
            RebuildLayoutAndScroll();
        }

        private void Update()
        {
            if (!bubbleMode || scrollRect == null)
            {
                return;
            }

            var current = CurrentViewportSize();
            if (Mathf.Abs(current.x - lastViewportSize.x) < 0.5f
                && Mathf.Abs(current.y - lastViewportSize.y) < 0.5f)
            {
                return;
            }

            lastViewportSize = current;
            RebuildLayoutAndScroll();
        }

        private bool TryPrepareBubbleMode()
        {
            if (scrollRect == null || scrollRect.content == null)
            {
                return false;
            }

            bubbleContent = ResolveBubbleContent();
            if (bubbleContent == null)
            {
                return false;
            }

            if (logText != null && logText.transform != bubbleContent)
            {
                logText.gameObject.SetActive(false);
            }

            var layout = bubbleContent.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = bubbleContent.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = bubbleContent.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = bubbleContent.gameObject.AddComponent<ContentSizeFitter>();
            }
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return true;
        }

        private RectTransform ResolveBubbleContent()
        {
            var existingContent = scrollRect.content;
            var logRect = logText != null ? logText.GetComponent<RectTransform>() : null;
            if (logRect == null || logRect != existingContent)
            {
                CleanupDuplicateBubbleContent(existingContent);
                return existingContent;
            }

            var viewport = scrollRect.viewport != null
                ? scrollRect.viewport
                : existingContent.parent as RectTransform;
            if (viewport == null)
            {
                return null;
            }

            var reusableContent = FindChildRect(viewport, "ChatBubbleContent");
            if (reusableContent != null)
            {
                scrollRect.content = reusableContent;
                CleanupDuplicateBubbleContent(reusableContent);
                if (logText != null)
                {
                    logText.gameObject.SetActive(false);
                }
                return reusableContent;
            }

            var bubbleObject = new GameObject("ChatBubbleContent", typeof(RectTransform));
            var nextContent = bubbleObject.GetComponent<RectTransform>();
            nextContent.SetParent(viewport, false);
            nextContent.anchorMin = new Vector2(0f, 1f);
            nextContent.anchorMax = new Vector2(1f, 1f);
            nextContent.pivot = new Vector2(0.5f, 1f);
            nextContent.anchoredPosition = Vector2.zero;
            nextContent.sizeDelta = new Vector2(0f, Mathf.Max(viewport.rect.height, existingContent.rect.height));

            scrollRect.content = nextContent;
            if (logText != null)
            {
                logText.gameObject.SetActive(false);
            }

            return nextContent;
        }

        private void CleanupDuplicateBubbleContent(RectTransform keep)
        {
            if (keep == null)
            {
                return;
            }

            var parent = keep.parent;
            if (parent == null)
            {
                return;
            }

            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                var child = parent.GetChild(index);
                if (child == keep || child.name != "ChatBubbleContent")
                {
                    continue;
                }

                Destroy(child.gameObject);
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

        private void RenderAll()
        {
            if (!bubbleMode)
            {
                RenderLegacy(null, null);
                return;
            }

            RebuildLayoutAndScroll();
        }

        private void RenderLegacy(string pendingSpeaker, string pendingText)
        {
            if (logText == null)
            {
                return;
            }

            if (!logText.gameObject.activeSelf)
            {
                logText.gameObject.SetActive(true);
            }

            var pending = pendingSpeaker == null ? null : $"{pendingSpeaker}: {pendingText}";
            logText.text = pending == null ? legacyLogBuilder.ToString() : legacyLogBuilder + pending;

            Canvas.ForceUpdateCanvases();
            ResizeLegacyContent();
            ScrollToBottom();
        }

        private MessageEntry CreateBubble(string speaker, string text, bool pending)
        {
            var entry = new MessageEntry
            {
                Speaker = speaker,
                Text = text,
            };

            var prefab = messageBubblePrefab != null ? messageBubblePrefab : runtimeBubbleTemplate;
            var bubble = Instantiate(prefab, bubbleContent, false);
            bubble.name = pending ? "PendingMessageBubble" : "MessageBubble";
            bubble.gameObject.SetActive(true);
            entry.Root = bubble.gameObject;
            entry.Bubble = bubble;

            ApplyEntryStyle(entry);
            return entry;
        }

        private void ApplyEntryStyle(MessageEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var isUser = IsUserSpeaker(entry.Speaker);
            var isSystem = IsSystemSpeaker(entry.Speaker);
            var width = ResolveBubbleWidth();
            var backgroundColor = isSystem
                ? YuiChatLogStyle.SystemBackground
                : isUser
                    ? YuiChatLogStyle.UserBackground
                    : YuiChatLogStyle.AssistantBackground;
            var speakerColor = isSystem
                ? YuiChatLogStyle.SystemSpeaker
                : YuiChatLogStyle.Speaker;
            var bodyColor = YuiChatLogStyle.Body;
            entry.Bubble?.Bind(
                entry.Speaker,
                entry.Text,
                width,
                isUser,
                backgroundColor,
                speakerColor,
                bodyColor,
                chatFont,
                bubbleSprite);
        }

        private void TrimOldMessages()
        {
            var limit = Mathf.Max(1, maxMessages);
            while (messages.Count > limit)
            {
                var stale = messages[0];
                messages.RemoveAt(0);
                DestroyEntry(stale);
            }
        }

        private void DestroyEntry(MessageEntry entry)
        {
            if (entry?.Root == null)
            {
                return;
            }

            Destroy(entry.Root);
            entry.Root = null;
        }

        private void RebuildLayoutAndScroll()
        {
            if (!bubbleMode || bubbleContent == null)
            {
                return;
            }

            foreach (var message in messages)
            {
                ApplyEntryStyle(message);
            }
            ApplyEntryStyle(pendingEntry);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleContent);
            Canvas.ForceUpdateCanvases();
            ScrollToBottom();
        }

        private float ResolveBubbleWidth()
        {
            var viewportWidth = scrollRect != null && scrollRect.viewport != null
                ? scrollRect.viewport.rect.width
                : 0f;
            var candidate = viewportWidth > 0f
                ? viewportWidth * YuiChatLogStyle.BubbleMaxWidthRatio
                : YuiChatLogStyle.BubbleFallbackWidth;
            return Mathf.Max(YuiChatLogStyle.BubbleMinWidth, candidate);
        }

        private void EnsureRuntimeBubbleTemplate()
        {
            if (messageBubblePrefab != null || runtimeBubbleTemplate != null)
            {
                return;
            }

            runtimeBubbleTemplate = YuiChatMessageBubble.CreateTemplate(chatFont, bubbleSprite);
            runtimeBubbleTemplate.transform.SetParent(transform, false);
        }

        private Vector2 CurrentViewportSize()
        {
            if (scrollRect == null || scrollRect.viewport == null)
            {
                return Vector2.zero;
            }

            return scrollRect.viewport.rect.size;
        }

        private void ResizeLegacyContent()
        {
            if (scrollRect == null || scrollRect.content == null || logText == null)
            {
                return;
            }

            var viewportHeight = scrollRect.viewport != null ? scrollRect.viewport.rect.height : 0f;
            var targetHeight = Mathf.Max(viewportHeight, logText.preferredHeight + 24f);
            var size = scrollRect.content.sizeDelta;
            size.y = targetHeight;
            scrollRect.content.sizeDelta = size;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        }

        private void ScrollToBottom()
        {
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private static bool IsUserSpeaker(string speaker)
        {
            return string.Equals(speaker, "You", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(speaker, "User", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSystemSpeaker(string speaker)
        {
            return string.Equals(speaker, "System", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(speaker, "Vision", System.StringComparison.OrdinalIgnoreCase);
        }

        private sealed class MessageEntry
        {
            public string Speaker;
            public string Text;
            public GameObject Root;
            public YuiChatMessageBubble Bubble;
        }
    }
}
