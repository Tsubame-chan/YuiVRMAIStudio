using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private void AppendLog(string speaker, string text, string resultMetadata = null)
        {
            // Display/copy/save retain the response, including code, indentation and URLs.
            // Speech cleanup belongs only to the TTS path.
            var displayText = text ?? string.Empty;
            historyGeneration++;
            if (!secretMode && speaker != "System")
            {
                try { ConversationArchive(ChatCharacterId()).Append(new YuiTextArchive.Entry {
                    Id=System.Guid.NewGuid().ToString("N"), CreatedUtc=System.DateTime.UtcNow.ToString("o"),
                    Speaker=speaker,Text=displayText,Mode=chatInteractionMode,Metadata=resultMetadata }); }
                catch (System.Exception ex) { SetStatus("History could not be saved. Free some space and try again.");Debug.LogWarning(ex.Message); }
            }
            // ConversationHistory is the user-controlled archive. Player.log
            // must not become a second, separately retained copy of its body.
            chatLogView?.AppendLog(speaker, displayText, resultMetadata, chatInteractionMode);
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
