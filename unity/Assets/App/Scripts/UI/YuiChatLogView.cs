using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiChatLogView : MonoBehaviour
    {
        private readonly StringBuilder logBuilder = new StringBuilder();
        private Text logText;
        private ScrollRect scrollRect;
        private string pendingLine;

        public bool IsEmpty => logBuilder.Length == 0;

        public void Configure(Text targetLogText, ScrollRect targetScrollRect)
        {
            logText = targetLogText;
            scrollRect = targetScrollRect;
            Render();
        }

        public void AppendLog(string speaker, string text)
        {
            logBuilder.AppendLine($"{speaker}: {text}");
            Render();
        }

        public void Clear()
        {
            logBuilder.Length = 0;
            pendingLine = null;
            Render();
        }

        public void SetPendingLine(string speaker, string text)
        {
            pendingLine = $"{speaker}: {text}";
            Render();
        }

        public void ClearPendingLine()
        {
            if (pendingLine == null)
            {
                return;
            }

            pendingLine = null;
            Render();
        }

        private void Render()
        {
            if (logText == null)
            {
                return;
            }

            logText.text = pendingLine == null
                ? logBuilder.ToString()
                : logBuilder.ToString() + pendingLine;

            Canvas.ForceUpdateCanvases();
            ResizeLogContent();
            ScrollToBottom();
        }

        private void ResizeLogContent()
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
    }
}
