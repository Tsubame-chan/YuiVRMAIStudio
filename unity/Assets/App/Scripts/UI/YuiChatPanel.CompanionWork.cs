using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private sealed class WorkTrialConfig
        {
            [JsonProperty("enabled")] public bool Enabled { get; set; }
            [JsonProperty("connection_id")] public string ConnectionId { get; set; }
            [JsonProperty("grant_id")] public string GrantId { get; set; }
            [JsonProperty("backend_url")] public string BackendUrl { get; set; }
            [JsonProperty("local_character_id")] public string LocalCharacterId { get; set; }
            [JsonProperty("shared_character_id")] public string SharedCharacterId { get; set; }
        }

        private sealed class WorkTrialPending
        {
            [JsonProperty("request_id")] public string RequestId { get; set; }
            [JsonProperty("text_hash")] public string TextHash { get; set; }
            [JsonProperty("backend_url")] public string BackendUrl { get; set; }
            [JsonProperty("local_character_id")] public string LocalCharacterId { get; set; }
        }

        private sealed class WorkTrialAnswerPending
        {
            [JsonProperty("op_id")] public string OpId { get; set; }
            [JsonProperty("activity_id")] public string ActivityId { get; set; }
            [JsonProperty("question_op_id")] public string QuestionOpId { get; set; }
            [JsonProperty("text_hash")] public string TextHash { get; set; }
            [JsonProperty("backend_url")] public string BackendUrl { get; set; }
            [JsonProperty("local_character_id")] public string LocalCharacterId { get; set; }
        }

        private bool companionWorkRefreshing;
        private DateTime companionWorkNextFullScanUtc;
        private HashSet<string> companionWorkSeen;
        private readonly Dictionary<string, int> companionWorkRevisions = new Dictionary<string, int>();
        private string WorkTrialConfigPath => Path.Combine(Application.persistentDataPath,
            "Companion", "work-trial.json");
        private string WorkTrialSeenPath => Path.Combine(Application.persistentDataPath,
            "Companion", "work-trial-seen.json");
        private string WorkTrialPendingPath => Path.Combine(Application.persistentDataPath,
            "Companion", "work-trial-pending.json");
        private string WorkTrialAnswerPendingPath => Path.Combine(Application.persistentDataPath,
            "Companion", "work-trial-answer-pending.json");

        private static string WorkTrialTextHash(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace("-", "").ToLowerInvariant();
        }

        private WorkTrialPending ReadWorkTrialPending()
        {
            if (!File.Exists(WorkTrialPendingPath)) return null;
            var pending = JsonConvert.DeserializeObject<WorkTrialPending>(File.ReadAllText(WorkTrialPendingPath));
            if (pending == null || string.IsNullOrWhiteSpace(pending.RequestId) ||
                string.IsNullOrWhiteSpace(pending.TextHash))
                throw new InvalidDataException("未確認のWork依頼記録が壊れています。元ファイルを確認してください。");
            return pending;
        }

        private void SaveWorkTrialPending(WorkTrialPending pending)
        {
            YuiSyncFileTransaction.Write(WorkTrialPendingPath,
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(pending)));
        }

        private WorkTrialAnswerPending ReadWorkTrialAnswerPending()
        {
            if (!File.Exists(WorkTrialAnswerPendingPath)) return null;
            var pending = JsonConvert.DeserializeObject<WorkTrialAnswerPending>(
                File.ReadAllText(WorkTrialAnswerPendingPath));
            if (pending == null || string.IsNullOrWhiteSpace(pending.OpId) ||
                string.IsNullOrWhiteSpace(pending.ActivityId) ||
                string.IsNullOrWhiteSpace(pending.QuestionOpId) ||
                string.IsNullOrWhiteSpace(pending.TextHash))
                throw new InvalidDataException("未確認のWork回答記録が壊れています。元ファイルを確認してください。");
            return pending;
        }

        private void SaveWorkTrialAnswerPending(WorkTrialAnswerPending pending)
        {
            YuiSyncFileTransaction.Write(WorkTrialAnswerPendingPath,
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(pending)));
        }

        private bool HasCompanionWorkTrialConfig()
        {
            if (!File.Exists(WorkTrialConfigPath)) return false;
            try {
                var config = JsonConvert.DeserializeObject<WorkTrialConfig>(File.ReadAllText(WorkTrialConfigPath));
                return config?.Enabled == true &&
                    (string.IsNullOrEmpty(config.BackendUrl) || config.BackendUrl == client.BaseUrl) &&
                    (string.IsNullOrEmpty(config.LocalCharacterId) || config.LocalCharacterId == ChatCharacterId());
            }
            catch (Exception) { return true; } // A broken opt-in must fail visibly, not silently route to Yui.
        }

        private YuiCompanionWorkClient WorkTrialClient(out string localCharacter)
        {
            if (secretMode) throw new InvalidOperationException("Secret Modeでは外部の作業担当へ送信できません。");
            if (pendingVisionImageAttachment.HasImage)
                throw new InvalidOperationException("この試験接続は画像添付に対応していません。画像を外してから依頼してください。");
            var config = JsonConvert.DeserializeObject<WorkTrialConfig>(File.ReadAllText(WorkTrialConfigPath));
            if (config?.Enabled != true || string.IsNullOrWhiteSpace(config.ConnectionId))
                throw new InvalidDataException("Workの接続設定を確認できません。");
            localCharacter = ChatCharacterId();
            if ((!string.IsNullOrEmpty(config.BackendUrl) && config.BackendUrl != client.BaseUrl) ||
                (!string.IsNullOrEmpty(config.LocalCharacterId) && config.LocalCharacterId != localCharacter))
                throw new InvalidOperationException("dot接続を作成したBackendまたはキャラクターに戻してください。");
            var state = ReadSyncState();
            var sharedId = (string)(state[localCharacter] as JObject)?["shared_id"];
            if (string.IsNullOrWhiteSpace(sharedId))
                throw new InvalidOperationException("このキャラクターを先に端末同期で登録してください。");
            if (!string.IsNullOrEmpty(config.SharedCharacterId) && config.SharedCharacterId != sharedId)
                throw new InvalidOperationException("共有キャラクターの対応が変わりました。dot接続を作り直してください。");
            var pairedToken = YuiSyncCredentialStore.Read(client.BaseUrl);
            return new YuiCompanionWorkClient(client, sharedId, config.ConnectionId, pairedToken);
        }

        private void SaveWorkTrialConfig(WorkTrialConfig config)
        {
            YuiSyncFileTransaction.Write(WorkTrialConfigPath,
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(config, Formatting.Indented)));
            companionWorkRevisions.Clear();
            companionWorkNextFullScanUtc = DateTime.MinValue;
            UpdateChatInteractionModeUi();
        }

        private async Task OpenCompanionWorkSetupAsync()
        {
            if (!HasRegisteredDeviceSync())
            {
                var missing = YuiSimpleDialog.Create("dotの作業接続",
                    "先にこのキャラクターと端末をBackendへ登録し、同期を完了してください。");
                missing.AddButton("戻る", () => { missing.Close(); OpenDeviceSync(); });
                return;
            }
            try
            {
                WorkTrialClientForSetup(out var localCharacter,
                    out var sharedCharacter, out var token);
                var capabilities = await client.CompanionCapabilitiesAsync(token, cancellationTokenSource.Token);
                var issuer = (string)capabilities["oauth_issuer"];
                var resource = (string)capabilities["mcp_resource"];
                if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(resource))
                    throw new InvalidOperationException("BackendのOAuth公開入口とMCP接続先が未設定です。");
                WorkTrialConfig config = null;
                if (File.Exists(WorkTrialConfigPath))
                    config = JsonConvert.DeserializeObject<WorkTrialConfig>(File.ReadAllText(WorkTrialConfigPath));
                if (config?.Enabled == true &&
                    (config.BackendUrl != client.BaseUrl || config.LocalCharacterId != localCharacter))
                    throw new InvalidOperationException("別のBackendまたはキャラクターで試験接続が有効です。そちらで解除してから作成してください。");
                if (config?.Enabled == true && config.BackendUrl == client.BaseUrl &&
                    config.LocalCharacterId == localCharacter &&
                    config.SharedCharacterId == sharedCharacter)
                {
                    var dialog = YuiSimpleDialog.Create("dotの作業接続",
                        $"作業担当: dot (個人試験)\n対象: {characterName}\n認可入口: {issuer}\nMCP: {resource}\n\n依頼本文だけを送ります。記憶の閲覧は許可していません。");
                    dialog.AddButton("確認コードを再発行", async () => {
                        dialog.Close(); await ShowWorkApprovalAsync(config.GrantId, token, issuer, resource);
                    });
                    dialog.AddButton("接続を解除", async () => {
                        dialog.Close(); await RevokeCompanionWorkTrialAsync(config, token);
                    });
                    dialog.AddButton("戻る", () => { dialog.Close(); OpenDeviceSync(); });
                    dialog.Compact(640);
                    return;
                }
                var setup = YuiSimpleDialog.Create("dotの作業接続を試す",
                    $"対象: {characterName}\n認可入口: {issuer}\nMCP: {resource}\n\nWorkの依頼本文をdotへ渡し、進捗と結果を受け取ります。記憶の閲覧は許可しません。接続は後で解除できます。");
                setup.AddButton("この範囲で試験接続を作成", async () => {
                    setup.Close();
                    await CreateCompanionWorkTrialAsync(localCharacter, sharedCharacter, token, issuer, resource);
                });
                setup.AddButton("戻る", () => { setup.Close(); OpenDeviceSync(); });
                setup.Compact(640);
            }
            catch (Exception ex)
            {
                var error = YuiSimpleDialog.Create("dot接続を準備できませんでした", ex.Message);
                error.AddButton("戻る", () => { error.Close(); OpenDeviceSync(); });
                Debug.LogWarning("Companion Work setup failed: " + ex.GetType().Name);
            }
        }

        private void WorkTrialClientForSetup(out string localCharacter,
            out string sharedCharacter, out string token)
        {
            localCharacter = ChatCharacterId();
            sharedCharacter = (string)(ReadSyncState()[localCharacter] as JObject)?["shared_id"];
            token = YuiSyncCredentialStore.Read(client.BaseUrl);
            if (secretMode || string.IsNullOrWhiteSpace(sharedCharacter) || string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Secret Modeを終了し、このキャラクターの端末同期を完了してください。");
        }

        private async Task CreateCompanionWorkTrialAsync(string localCharacter,
            string sharedCharacter, string token, string issuer, string resource)
        {
            string grantId = null;
            try
            {
                var connectionId = Guid.NewGuid().ToString("N");
                var grant = await client.CompanionIssueGrantAsync(connectionId, sharedCharacter,
                    token, cancellationTokenSource.Token);
                grantId = (string)grant["grant_id"];
                if (string.IsNullOrWhiteSpace(grantId)) throw new InvalidDataException("Grantの受理を確認できません。");
                var config = new WorkTrialConfig {
                    Enabled = true, ConnectionId = connectionId, GrantId = grantId,
                    BackendUrl = client.BaseUrl, LocalCharacterId = localCharacter,
                    SharedCharacterId = sharedCharacter
                };
                SaveWorkTrialConfig(config);
                await ShowWorkApprovalAsync(grantId, token, issuer, resource);
            }
            catch (Exception ex)
            {
                if (grantId != null && !File.Exists(WorkTrialConfigPath))
                    try { await client.CompanionRevokeGrantAsync(grantId, token); } catch (Exception) { }
                var error = YuiSimpleDialog.Create("dot接続を作成できませんでした", ex.Message);
                error.AddButton("閉じる", error.Close);
            }
        }

        private async Task ShowWorkApprovalAsync(string grantId, string token,
            string issuer, string resource)
        {
            try
            {
                var approval = await client.CompanionIssueOAuthApprovalAsync(grantId,
                    token, cancellationTokenSource.Token);
                var secret = (string)approval["approval_secret"];
                if (string.IsNullOrWhiteSpace(secret)) throw new InvalidDataException("確認コードがありません。");
                var dialog = YuiSimpleDialog.Create("ChatGPT接続の確認コード",
                    $"このコードをChatGPTから開いたYuiの認可画面へ入力してください。約10分で失効します。\n\n{secret}\n\n認可入口: {issuer}\nMCP: {resource}\n\n接続するプラグインと範囲を画面で確認してください。");
                dialog.AddButton("コードをコピー", () => { GUIUtility.systemCopyBuffer = secret; SetStatus("確認コードをコピーしました"); });
                dialog.AddButton("閉じる", dialog.Close);
                dialog.Compact(680);
            }
            catch (Exception ex)
            {
                var error = YuiSimpleDialog.Create("確認コードを発行できませんでした", ex.Message);
                error.AddButton("閉じる", error.Close);
            }
        }

        private async Task RevokeCompanionWorkTrialAsync(WorkTrialConfig config, string token)
        {
            try
            {
                await client.CompanionRevokeGrantAsync(config.GrantId, token, cancellationTokenSource.Token);
                File.Delete(WorkTrialConfigPath);
                if (File.Exists(WorkTrialPendingPath)) File.Delete(WorkTrialPendingPath);
                if (File.Exists(WorkTrialAnswerPendingPath)) File.Delete(WorkTrialAnswerPendingPath);
                companionWorkRevisions.Clear();
                companionWorkNextFullScanUtc = DateTime.MinValue;
                UpdateChatInteractionModeUi();
                SetStatus("dotの作業接続を解除しました。");
            }
            catch (Exception ex)
            {
                var error = YuiSimpleDialog.Create("接続を解除できませんでした", ex.Message);
                error.AddButton("閉じる", error.Close);
            }
        }

        private bool ArchiveCompanionWorkRequest(string localCharacter, string requestId,
            string activityId, string message, bool display)
        {
            var metadata = JsonConvert.SerializeObject(new {
                request_id = requestId, activity_id = activityId,
                character_id = localCharacter, mode = "work", external_work = true
            });
            try
            {
                ConversationArchive(localCharacter).Append(new YuiTextArchive.Entry {
                    Id = YuiCompanionWorkClient.StableId(requestId, "user-entry"),
                    CreatedUtc = DateTime.UtcNow.ToString("o"), Speaker = "You",
                    Text = message, Mode = "work", Metadata = metadata
                });
                historyGeneration++;
                if (display && ChatCharacterId() == localCharacter &&
                    YuiChatRequestModes.IsWork(chatInteractionMode))
                    chatLogView?.AppendLog("You", message, metadata, "work");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Companion Work history save failed: " + ex.GetType().Name);
                SetStatus("依頼は受理されましたが、端末の履歴保存に失敗しました。空き容量を確認してください。");
                return false;
            }
        }

        private async Task ChooseCompanionWorkDestinationAsync(string message)
        {
            try
            {
                var pending = ReadWorkTrialAnswerPending();
                if (ReadWorkTrialPending() != null && pending == null)
                {
                    await SendCompanionWorkTrialAsync(message);
                    return;
                }
                if (pending != null)
                {
                    if (pending.TextHash != WorkTrialTextHash(message) ||
                        pending.BackendUrl != client.BaseUrl || pending.LocalCharacterId != ChatCharacterId())
                        throw new InvalidOperationException("前回の回答の受理を確認できていません。同じ本文で再送してください。");
                    await SendCompanionWorkAnswerAsync(message, pending.ActivityId,
                        pending.QuestionOpId);
                    return;
                }
                // Query the owner view immediately before choosing. A cached status may
                // refer to an already answered or superseded question.
                var worker = WorkTrialClient(out _);
                var list = await worker.ListAsync(cancellationTokenSource.Token, fullScan: true);
                var waiting = list?.Items?.FirstOrDefault(item => item?.State == "waiting_user");
                if (waiting == null)
                {
                    await SendCompanionWorkTrialAsync(message);
                    return;
                }
                var detail = await client.CompanionActivityDetailsAsync(waiting.CharacterId,
                    waiting.ActivityId, YuiSyncCredentialStore.Read(client.BaseUrl),
                    cancellationTokenSource.Token);
                var question = detail?.Reports?.LastOrDefault(report => report != null && report.Applied &&
                    report.Kind == "question" && report.RequestRevision == detail.RequestRevision);
                if (detail?.State != "waiting_user" || question == null)
                {
                    await SendCompanionWorkTrialAsync(message);
                    return;
                }
                var preview = question.Text.Length > 500
                    ? question.Text.Substring(0, 500) + "…" : question.Text;
                var dialog = YuiSimpleDialog.Create(YuiSimpleDialog.L(
                    "dotから質問があります", "dot has a question"),
                    preview + "\n\n" + YuiSimpleDialog.L(
                        "今の入力を、この質問への回答として送りますか？",
                        "Send your current text as an answer to this question?"));
                dialog.AddButton(YuiSimpleDialog.L("この質問に答える", "Answer this question"), async () => {
                    dialog.Close();
                    await SendCompanionWorkAnswerAsync(message, detail.ActivityId, question.OpId);
                });
                dialog.AddButton(YuiSimpleDialog.L("新しい依頼として送る", "Send as a new task"), async () => {
                    dialog.Close(); await SendCompanionWorkTrialAsync(message);
                });
                dialog.AddButton(YuiSimpleDialog.L("入力へ戻る", "Back to input"), () => {
                    dialog.Close(); RestoreCompanionWorkInput(message);
                });
                dialog.Compact(640);
            }
            catch (Exception ex)
            {
                RestoreCompanionWorkInput(message);
                SetStatus("Workの送信先を確認できませんでした。入力を保持しました。");
                Debug.LogWarning("Companion Work choice failed: " + ex.GetType().Name);
            }
        }

        private void RestoreCompanionWorkInput(string message)
        {
            if (inputField != null && string.IsNullOrEmpty(inputField.text)) inputField.text = message;
        }

        private async Task SendCompanionWorkAnswerAsync(string message,
            string activityId, string questionOpId)
        {
            if (isSending || deviceSyncBusy) { RestoreCompanionWorkInput(message); return; }
            isSending = true;
            SetInteractable(false);
            try
            {
                WorkTrialClient(out var localCharacter);
                var server = client.BaseUrl;
                var pending = ReadWorkTrialAnswerPending();
                if (pending != null && (pending.ActivityId != activityId ||
                    pending.QuestionOpId != questionOpId || pending.TextHash != WorkTrialTextHash(message) ||
                    pending.BackendUrl != server || pending.LocalCharacterId != localCharacter))
                    throw new InvalidOperationException("前回の回答の受理を確認できていません。同じ本文で再送してください。");
                var detail = await client.CompanionActivityDetailsAsync(
                    WorkTrialSharedCharacterId(localCharacter), activityId,
                    YuiSyncCredentialStore.Read(server), cancellationTokenSource.Token);
                if (detail == null) throw new InvalidDataException("元のWork依頼が見つかりません。");
                var opId = pending?.OpId ?? Guid.NewGuid().ToString("N");
                var accepted = detail.Answers?.Any(answer => answer.OpId == opId &&
                    answer.QuestionOpId == questionOpId && WorkTrialTextHash(answer.Text) == WorkTrialTextHash(message)) == true;
                if (!accepted)
                {
                    if (detail.State != "waiting_user" ||
                        detail.Reports?.LastOrDefault(report => report != null && report.Applied && report.Kind == "question" &&
                            report.RequestRevision == detail.RequestRevision)?.OpId != questionOpId)
                        throw new InvalidOperationException("質問が更新されました。Workの履歴を確認してください。");
                    if (pending == null)
                        SaveWorkTrialAnswerPending(new WorkTrialAnswerPending {
                            OpId = opId, ActivityId = activityId, QuestionOpId = questionOpId,
                            TextHash = WorkTrialTextHash(message), BackendUrl = server,
                            LocalCharacterId = localCharacter
                        });
                    var receipt = await client.CompanionActivityCommandAsync(detail.CharacterId,
                        activityId, new YuiCompanionActivityCommand {
                            OpId = opId, ExpectedRevision = detail.Revision,
                            RequestRevision = detail.RequestRevision, Command = "answer_question",
                            QuestionOpId = questionOpId, Text = message
                        }, YuiSyncCredentialStore.Read(server), cancellationTokenSource.Token);
                    if (receipt?.ActivityId != activityId)
                        throw new InvalidDataException("回答の受理確認が一致しません。");
                }
                if (client.BaseUrl != server || ChatCharacterId() != localCharacter)
                    throw new InvalidOperationException("回答は保存されました。元のキャラクターでWork履歴を確認してください。");
                var metadata = JsonConvert.SerializeObject(new {
                    activity_id = activityId, question_op_id = questionOpId,
                    answer_op_id = opId, external_work = true
                });
                ConversationArchive(localCharacter).Append(new YuiTextArchive.Entry {
                    Id = opId, CreatedUtc = DateTime.UtcNow.ToString("o"),
                    Speaker = "You", Text = message, Mode = "work", Metadata = metadata
                });
                historyGeneration++;
                if (YuiChatRequestModes.IsWork(chatInteractionMode))
                    chatLogView?.AppendLog("You", message, metadata, "work");
                File.Delete(WorkTrialAnswerPendingPath);
                SetStatus("dotへ回答しました。作業の続きを待っています。");
                _ = RefreshCompanionWorkTrialAsync(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                RestoreCompanionWorkInput(message);
                SetStatus("dotへ回答できませんでした。入力を保持しました。");
                Debug.LogWarning("Companion Work answer failed: " + ex.GetType().Name);
            }
            finally { isSending = false; SetInteractable(true); }
        }

        private string WorkTrialSharedCharacterId(string localCharacter)
            => (string)(ReadSyncState()[localCharacter] as JObject)?["shared_id"];

        private async Task SendCompanionWorkTrialAsync(string message)
        {
            if (isSending || deviceSyncBusy) return;
            isSending = true;
            SetInteractable(false);
            try
            {
                var worker = WorkTrialClient(out var localCharacter);
                var server = client.BaseUrl;
                var pending = ReadWorkTrialPending();
                if (pending != null &&
                    (pending.TextHash != WorkTrialTextHash(message) || pending.BackendUrl != server ||
                     pending.LocalCharacterId != localCharacter))
                    throw new InvalidOperationException("前回の依頼の受理を確認できていません。同じ本文で再送するか、Workの履歴を確認してください。");
                var requestId = pending?.RequestId ?? Guid.NewGuid().ToString("N");
                SetStatus("dotへの依頼を保存中...");
                var capabilities = await client.CompanionCapabilitiesAsync(
                    YuiSyncCredentialStore.Read(server), cancellationTokenSource.Token);
                var features = capabilities["enabled_features"] as JArray;
                if ((int?)capabilities["protocol"] != 2 || features == null ||
                    !features.Values<string>().Contains("activity_state_testing"))
                    throw new InvalidOperationException("接続先BackendではCompanion Work試験が有効ではありません。");
                if (pending == null)
                    SaveWorkTrialPending(new WorkTrialPending {
                        RequestId = requestId, TextHash = WorkTrialTextHash(message),
                        BackendUrl = server, LocalCharacterId = localCharacter
                    });
                var receipt = await worker.DispatchAsync(requestId, message, cancellationTokenSource.Token);
                if (receipt == null || receipt.ActivityId != YuiCompanionWorkClient.StableId(requestId, "activity"))
                    throw new InvalidDataException("依頼の受理確認が一致しません。");
                if (client.BaseUrl != server || ChatCharacterId() != localCharacter)
                    throw new InvalidOperationException("依頼は保存されましたが、接続先かキャラクターが変わりました。Work履歴で確認してください。");
                if (ArchiveCompanionWorkRequest(localCharacter, requestId,
                    receipt.ActivityId, message, true))
                {
                    File.Delete(WorkTrialPendingPath);
                    SetStatus("dotへ依頼しました。作業中もトークを使えます。");
                }
                _ = RefreshCompanionWorkTrialAsync(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                if (inputField != null && string.IsNullOrEmpty(inputField.text)) inputField.text = message;
                SetStatus("dotへ依頼できませんでした。入力を保持しました。");
                Debug.LogWarning("Companion Work dispatch failed: " + ex.GetType().Name);
            }
            finally
            {
                isSending = false;
                SetInteractable(true);
            }
        }

        private HashSet<string> WorkTrialSeen()
        {
            if (companionWorkSeen != null) return companionWorkSeen;
            try
            {
                companionWorkSeen = File.Exists(WorkTrialSeenPath)
                    ? new HashSet<string>(JsonConvert.DeserializeObject<List<string>>(
                        File.ReadAllText(WorkTrialSeenPath)) ?? new List<string>(), StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
            }
            catch (Exception)
            {
                // Do not re-import old reports when receipt state is damaged.
                throw new InvalidDataException("Work受信記録を読み込めません。元ファイルを確認してください。");
            }
            return companionWorkSeen;
        }

        private void MarkWorkTrialSeen(string reportOpId)
        {
            var seen = WorkTrialSeen();
            var next = new HashSet<string>(seen, StringComparer.Ordinal) { reportOpId };
            YuiSyncFileTransaction.Write(WorkTrialSeenPath,
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(next.OrderBy(x => x, StringComparer.Ordinal))));
            companionWorkSeen = next;
        }

        private async Task RefreshCompanionWorkTrialAsync(CancellationToken cancellation)
        {
            if (companionWorkRefreshing || !HasCompanionWorkTrialConfig() || secretMode) return;
            companionWorkRefreshing = true;
            try
            {
                var worker = WorkTrialClient(out var localCharacter);
                var server = client.BaseUrl;
                var seen = WorkTrialSeen();
                var fullScan = DateTime.UtcNow >= companionWorkNextFullScanUtc;
                var list = await worker.ListAsync(cancellation, fullScan);
                if (fullScan) companionWorkNextFullScanUtc = DateTime.UtcNow.AddMinutes(5);
                var newest = list?.Items?.FirstOrDefault();
                if (!isSending && ChatCharacterId() == localCharacter &&
                    YuiChatRequestModes.IsWork(chatInteractionMode) && newest != null)
                {
                    var stateLabel = newest.State == "completed" ? "結果の報告あり" :
                        newest.State == "waiting_user" ? "質問あり" :
                        newest.State == "cancel_requested" ? "停止確認待ち" :
                        newest.State == "cancelled" ? "停止済み" : "作業中";
                    SetStatus("dot · " + stateLabel);
                }
                var pending = ReadWorkTrialPending();
                var recovered = pending != null && list?.Items != null
                    ? list.Items.FirstOrDefault(x => x?.ActivityId ==
                        YuiCompanionWorkClient.StableId(pending.RequestId, "activity")) : null;
                if (!isSending && pending != null && pending.BackendUrl == server &&
                    pending.LocalCharacterId == localCharacter && recovered != null &&
                    !string.IsNullOrWhiteSpace(recovered.Request) &&
                    WorkTrialTextHash(recovered.Request) == pending.TextHash &&
                    ArchiveCompanionWorkRequest(localCharacter, pending.RequestId,
                        recovered.ActivityId, recovered.Request, true))
                {
                    File.Delete(WorkTrialPendingPath);
                    SetStatus("前回のWork依頼の受理を履歴から確認しました。");
                }
                YuiCompanionActivityDetails speechActivity = null;
                YuiCompanionActivityReport speechReport = null;
                foreach (var item in (list?.Items ?? new List<YuiCompanionActivityDetails>()).AsEnumerable().Reverse())
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (item?.ActivityId == null) continue;
                    if (companionWorkRevisions.TryGetValue(item.ActivityId, out var knownRevision) &&
                        knownRevision == item.Revision) continue;
                    var detail = await client.CompanionActivityDetailsAsync(item.CharacterId,
                        item.ActivityId, YuiSyncCredentialStore.Read(server), cancellation);
                    if (detail?.Reports == null) continue;
                    foreach (var report in detail.Reports)
                    {
                        if (report == null || !report.Applied ||
                            report.RequestRevision != detail.RequestRevision ||
                            string.IsNullOrWhiteSpace(report.OpId) || string.IsNullOrWhiteSpace(report.Text) ||
                            detail.State == "cancel_requested" || detail.State == "cancelled") continue;
                        var newlySeen = !seen.Contains(report.OpId);
                        if (newlySeen)
                        {
                            if (client.BaseUrl != server) return;
                            var metadata = JsonConvert.SerializeObject(new {
                                activity_id = detail.ActivityId, report_op_id = report.OpId,
                                kind = report.Kind, external_report = true,
                                completion_basis = detail.CompletionBasis
                            });
                            ConversationArchive(localCharacter).Append(new YuiTextArchive.Entry {
                                Id = report.OpId, CreatedUtc = DateTime.UtcNow.ToString("o"),
                                Speaker = HistoryCharacterName(localCharacter), Text = report.Text,
                                Mode = "work", Metadata = metadata
                            });
                            historyGeneration++;
                            MarkWorkTrialSeen(report.OpId);
                            seen = WorkTrialSeen();
                            if (ChatCharacterId() == localCharacter && YuiChatRequestModes.IsWork(chatInteractionMode))
                                chatLogView?.AppendLog(CharacterName, report.Text, metadata, "work");
                        }
                        if (newlySeen && (report.Kind == "result" || report.Kind == "question"))
                        {
                            speechActivity = detail;
                            speechReport = report;
                        }
                    }
                    companionWorkRevisions[item.ActivityId] = detail.Revision;
                }
                if (speechReport != null)
                    await SpeakCompanionWorkReportAsync(localCharacter, server, speechActivity,
                        speechReport, cancellation);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogWarning("Companion Work refresh failed: " + ex.GetType().Name);
            }
            finally { companionWorkRefreshing = false; }
        }

        private async Task SpeakCompanionWorkReportAsync(string localCharacter, string server,
            YuiCompanionActivityDetails detail, YuiCompanionActivityReport report,
            CancellationToken cancellation)
        {
            if (ChatCharacterId() != localCharacter || client.BaseUrl != server ||
                !YuiChatRequestModes.IsWork(chatInteractionMode) || isSending ||
                audioSource == null || IsTtsMode("silent")) return;
            var pairedToken = YuiSyncCredentialStore.Read(server);
            var claim = await client.CompanionClaimSpeechAsync(detail.CharacterId,
                detail.ActivityId, report.OpId, pairedToken, cancellation);
            if (claim?.Claimed != true) return;
            await client.CompanionSpeechReceiptAsync(detail.CharacterId, detail.ActivityId,
                report.OpId, "started", claim.ClaimToken, pairedToken, cancellation);
            // The full report remains on screen. Only this short field reaches TTS.
            await SpeakResponseAsync(new ChatResponse {
                Text = report.Text, SpokenText = string.IsNullOrWhiteSpace(report.SpokenText)
                    ? "作業結果を画面にまとめたよ。" : report.SpokenText,
                ShouldTts = true, VoiceStyle = "normal"
            }, report.OpId, cancellation);
            await client.CompanionSpeechReceiptAsync(detail.CharacterId, detail.ActivityId,
                report.OpId, "completed", claim.ClaimToken, pairedToken, cancellation);
        }
    }
}
