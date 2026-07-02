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
        private async System.Threading.Tasks.Task SendMessageAsync(string message)
        {
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
                var chatRequestId = Guid.NewGuid().ToString("N");
                var chatTimer = System.Diagnostics.Stopwatch.StartNew();
                var chat = await SendChatViaRuntimeAsync(
                    new ChatRequest
                    {
                        RequestId = chatRequestId,
                        UserId = userId,
                        Message = message,
                        Context = CreateChatContext(),
                        Secret = secretMode,
                        CustomInstruction = customInstruction,
                        CharacterName = characterName
                    },
                    cancellationTokenSource.Token);
                pendingVisionImageAttachment.MarkConsumedAfterSuccessfulChat();
                Debug.Log($"Yui chat latency: {chatTimer.ElapsedMilliseconds} ms");
                YuiMemoryDiagnostics.LogSnapshot(
                    "chat_after_response",
                    $"user_chars={message?.Length ?? 0},reply_chars={chat?.Text?.Length ?? 0},chat_ms={chatTimer.ElapsedMilliseconds}");

                ClearPendingLine();
                AppendLog(CharacterName, chat.Text);
                Debug.Log($"Yui motion: face={chat.Face}, anim={chat.Animation}");
                if (avatarController != null)
                {
                    avatarController.ApplyResponse(chat);
                }
                if (chatdollKitController != null)
                {
                    chatdollKitController.ApplyResponse(chat);
                }

                await SpeakResponseAsync(chat, chatRequestId, cancellationTokenSource.Token);
                YuiMemoryDiagnostics.LogSnapshot(
                    "chat_after_tts",
                    $"user_chars={message?.Length ?? 0},reply_chars={chat?.Text?.Length ?? 0},total_ms={totalTimer.ElapsedMilliseconds}");
                Debug.Log($"Yui total response latency: {totalTimer.ElapsedMilliseconds} ms");
                SetStatus("Ready");
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
