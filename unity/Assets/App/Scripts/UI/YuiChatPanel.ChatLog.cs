using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private void AppendLog(string speaker, string text, string resultMetadata = null)
        {
            var displayText = speaker == "Yui" ? YuiSpeechTextUtility.CleanDisplayText(text) : text;
            Debug.Log($"{speaker}: {displayText}");
            chatLogView?.AppendLog(speaker, displayText, resultMetadata);
        }

        private void SetPendingLine(string speaker, string text)
        {
            chatLogView?.SetPendingLine(speaker, text);
        }

        private void ClearPendingLine()
        {
            chatLogView?.ClearPendingLine();
        }
    }
}
