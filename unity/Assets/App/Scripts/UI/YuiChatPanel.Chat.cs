using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.EventSystems;
using UnityEngine;
using YuiPhysicalAI.Audio;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private CancellationTokenSource activeChatCancellation;
        private string retryChatMessage;
        private string ChatCharacterId()
        {
            if (avatarSlot == YuiAvatarSlots.CustomVrm1 && runtimeVrmImporter != null)
            {
                var path = runtimeVrmImporter.LastCustomVrmPath;
                foreach (var entry in YuiAvatarLibrary.Read())
                    if (string.Equals(YuiAvatarLibrary.Resolve(entry), path, StringComparison.Ordinal)) return entry.id;
            }
            return "builtin:" + avatarSlot;
        }
        private string ChatSessionId(string characterId)
        {
            var key = "Yui.ChatSession." + characterId + "." + chatInteractionMode;
            var id = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(id)) { id = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString(key, id); PlayerPrefs.Save(); }
            return id;
        }

        private async System.Threading.Tasks.Task SendMessageAsync(string message)
        {
            if (isSending) return;
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
            activeChatCancellation = operation;
            retryChatMessage = message;
            var responseReceived = false;
            var characterId = ChatCharacterId();
            var sessionId = ChatSessionId(characterId);
            var taskId = chatInteractionMode == "work" ? Guid.NewGuid().ToString("N") : null;
            Debug.Log(IsLocalAiConversationMode()
                ? $"Sending message to Yui local AI: {message}"
                : IsDirectOpenAiConversationMode()
                    ? $"Sending message to Yui API mode: {message}"
                    : $"Sending message to Yui backend: {message}");
            var totalTimer = System.Diagnostics.Stopwatch.StartNew();
            isSending = true;
            SetInteractable(false);
            AppendLog("You", message);
            SetStatus("Thinking...");
            SetPendingLine(CharacterName, "考え中...");
            YuiMemoryDiagnostics.LogSnapshot("chat_before_request", $"user_chars={message?.Length ?? 0}");

            try
            {
                SetStatus("Generating...");
                SetPendingLine(CharacterName, "返答生成中...");
                if (pendingVisionImageAttachment.HasImage && !ShouldAttachImageForApiChat())
                {
                    var dataUrl = pendingVisionImageAttachment.ImageDataUrl;
                    var comma = dataUrl.IndexOf(',');
                    var mime = dataUrl.Substring(5, dataUrl.IndexOf(';') - 5);
                    latestVision = await AnalyzeImageViaRuntimeAsync(
                        Convert.FromBase64String(dataUrl.Substring(comma + 1)),
                        "attachment", "general", mime, operation.Token);
                    operation.Token.ThrowIfCancellationRequested();
                }
                var chatRequestId = Guid.NewGuid().ToString("N");
                var chatTimer = System.Diagnostics.Stopwatch.StartNew();
                var chat = await SendChatViaRuntimeAsync(
                    new ChatRequest
                    {
                        RequestId = chatRequestId,
                        CharacterId = characterId,
                        SessionId = sessionId,
                        TaskId = taskId,
                        UserId = userId,
                        Message = message,
                        Context = CreateChatContext(),
                        Mode = chatInteractionMode,
                        Secret = secretMode,
                        CustomInstruction = customInstruction,
                        CharacterName = characterName
                    },
                    operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                responseReceived = true;
                retryChatMessage = null;
                pendingVisionImageAttachment.MarkConsumedAfterSuccessfulChat();
                Debug.Log($"Yui chat latency: {chatTimer.ElapsedMilliseconds} ms");
                YuiMemoryDiagnostics.LogSnapshot(
                    "chat_after_response",
                    $"user_chars={message?.Length ?? 0},reply_chars={chat?.Text?.Length ?? 0},chat_ms={chatTimer.ElapsedMilliseconds}");

                ClearPendingLine();
                AppendLog(CharacterName, chat.Text, JsonConvert.SerializeObject(new {
                    schema_version = 1, character_id = characterId, session_id = sessionId,
                    task_id = taskId, request_id = chatRequestId, mode = chatInteractionMode,
                    created_at = DateTime.UtcNow.ToString("o")
                }));
                Debug.Log($"Yui motion: face={chat.Face}, anim={chat.Animation}");
                if (avatarController != null)
                {
                    avatarController.ApplyResponse(chat);
                }
                if (chatdollKitController != null)
                {
                    chatdollKitController.ApplyResponse(chat);
                }

                await SpeakResponseAsync(chat, chatRequestId, operation.Token);
                YuiMemoryDiagnostics.LogSnapshot(
                    "chat_after_tts",
                    $"user_chars={message?.Length ?? 0},reply_chars={chat?.Text?.Length ?? 0},total_ms={totalTimer.ElapsedMilliseconds}");
                Debug.Log($"Yui total response latency: {totalTimer.ElapsedMilliseconds} ms");
                SetStatus("Ready");
            }
            catch (OperationCanceledException)
            {
                ClearPendingLine();
                SetStatus("停止しました");
            }
            catch (YuiBackendException ex) when (ex.StatusCode == 0)
            {
                ClearPendingLine();
                SetStatus("Backend offline");
                AppendLog(
                    "System",
                    $"Backendに接続できません。scripts/run_backend.ps1 を起動してください。url={ex.Url}");
                Debug.LogError(ex);
            }
            catch (Exception ex)
            {
                ClearPendingLine();
                SetStatus(IsLocalAiConversationMode() ? "Local AI unavailable" : "Error");
                var errorMessage = ex is YuiBackendException backendException
                    ? backendException.UserMessage
                    : ex.Message;
                AppendLog("System", errorMessage);
                Debug.LogError(ex);
            }
            finally
            {
                activeChatCancellation = null;
                if (!responseReceived && inputField != null && string.IsNullOrEmpty(inputField.text))
                    inputField.text = message;
                isSending = false;
                SetInteractable(true);
                if (inputField != null)
                {
                    inputField.DeactivateInputField();
                    if (EventSystem.current != null
                        && EventSystem.current.currentSelectedGameObject == inputField.gameObject)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                    }
                }
            }
        }

    }
}
