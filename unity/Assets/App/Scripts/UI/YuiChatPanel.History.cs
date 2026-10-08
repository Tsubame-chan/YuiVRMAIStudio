using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Avatar;
using System.Collections.Generic;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private const int HistoryPageSize = 100;
        private int historyPage, historyGeneration;
        private bool savedHistory, backendHistory;
        private long historySnapshot = -1;
        private string historyCharacter;
        private string historyMode;
        private string historySession;

        private YuiTextArchive ConversationArchive(string character)
        {
            using var hash = SHA256.Create();
            var key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(character))).Replace("-", "");
            return new YuiTextArchive(Path.Combine(Application.persistentDataPath, "ConversationHistory", key + ".jsonl"));
        }

        private void OpenHistory()
        {
            historyCharacter = ChatCharacterId(); historyMode = chatInteractionMode;
            historySession = ChatSessionId(historyCharacter);
            historyPage = 0; savedHistory = backendHistory = false; historySnapshot = -1;
            ShowHistory();
        }

        private async void ShowHistory()
        {
            if (backendHistory && !savedHistory && !secretMode)
            {
                try { await EnsureExternalDataPermissionAsync(backendUrl, false, cancellationTokenSource.Token); }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { SetStatus("History could not be opened. Your data is retained."); Debug.LogWarning(ex.GetType().Name); return; }
            }
            var root = CreateSavedDataPanel("History");
            var character = historyCharacter ?? ChatCharacterId();
            var isSaved = savedHistory; var isBackend = backendHistory; var pageNumber = historyPage;
            var archive = ConversationArchive(character); var store = ResultStore;
            ComposerButton(root,"Conversations","Conversations",()=> { savedHistory=backendHistory=false;historyPage=0;historySnapshot=-1;ShowHistory(); },.03f,.77f,.49f,.85f);
            ComposerButton(root,"SavedAnswers","Saved answers",()=> { savedHistory=true;backendHistory=false;historyPage=0;ShowHistory(); },.51f,.77f,.97f,.85f);
            if(isSaved) ComposerButton(root,"SavedScope","すべての保存済み回答",()=>{},.03f,.68f,.97f,.755f).interactable=false;
            else ComposerButton(root,"HistoryCharacter","会話相手: " + HistoryCharacterName(character) + " ▾",ShowHistoryCharacterSelector,.03f,.68f,.97f,.755f);
            root.Find(isSaved ? "SavedAnswers" : "Conversations").GetComponent<Image>().color=YuiUiTheme.Selected;
            if (!isSaved && !isBackend)
                ComposerButton(root,"DeleteHistory","履歴をまとめて削除",ShowHistoryDeletion,.03f,.205f,.97f,.27f);
            if (secretMode && !isSaved)
            {
                ComposerButton(root,"Private","History is hidden in Secret mode",()=>{},.04f,.36f,.96f,.66f).interactable=false;
                return;
            }
            try
            {
                YuiTextArchive.Page page;
                if (isBackend)
                {
                    var response=await client.GetRecentConversationsAsync(userId,HistoryPageSize,cancellationTokenSource.Token,character,historySession,pageNumber*HistoryPageSize);
                    page=new YuiTextArchive.Page { HasOlder=response.Items.Count==HistoryPageSize };
                    foreach(var item in response.Items.AsEnumerable().Reverse())
                        page.Items.Add(new YuiTextArchive.Entry { Speaker=item.Role=="assistant"?CharacterName:"You",Text=item.Message,CreatedUtc=item.CreatedAt,Mode=historyMode });
                }
                else
                {
                    if (!isSaved && historySnapshot < 0) historySnapshot=archive.Length;
                    var snapshot=historySnapshot;
                    page=await Task.Run(()=>isSaved ? store.Page(pageNumber*HistoryPageSize,HistoryPageSize) : archive.ReadPage(pageNumber*HistoryPageSize,HistoryPageSize,null,snapshot));
                }
                if(root==null || savedDataPanel!=root.gameObject) return;
                RenderHistoryRows(root,page,isSaved,isBackend,archive,character);
                if(page.Items.Count==0) ComposerButton(root,"Empty","Nothing saved yet",()=>{},.04f,.4f,.96f,.65f).interactable=false;
                ComposerButton(root,"Newer","Newer",()=>{historyPage--;ShowHistory();},.03f,.12f,.27f,.20f).interactable=pageNumber>0;
                ComposerButton(root,"Page",$"{(page.Items.Count==0?0:pageNumber*HistoryPageSize+1)}–{pageNumber*HistoryPageSize+page.Items.Count}",()=>{},.29f,.12f,.70f,.20f,false).interactable=false;
                ComposerButton(root,"Older","Older",()=>{historyPage++;ShowHistory();},.73f,.12f,.97f,.20f).interactable=page.HasOlder;
                if(!isSaved && RequiresBackend)
                    ComposerButton(root,"BackendHistory",isBackend?"On-device history":"Earlier backend history",()=>{backendHistory=!backendHistory;historyPage=0;ShowHistory();},.03f,.025f,.97f,.095f);
                else ComposerButton(root,"Retention",page.DamagedLines>0?"Some records could not be read; original data is retained.":"Text history is not deleted automatically",()=>{},.03f,.025f,.97f,.095f).interactable=false;
                FocusModal(root);
            }
            catch(Exception ex)
            {
                if(root!=null) ComposerButton(root,"Error","History could not be opened. Your data is retained.",()=>{},.04f,.3f,.96f,.6f).interactable=false;
                Debug.LogWarning("History read failed: "+ex.Message);
            }
        }

        private void RenderHistoryRows(Transform root,YuiTextArchive.Page page,bool isSaved,bool isBackend,YuiTextArchive archive,string character)
        {
                var list = new GameObject("HistoryList", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
                list.transform.SetParent(root, false); Place(list.GetComponent<RectTransform>(), .03f, .29f, .97f, .66f);
                list.GetComponent<Image>().color = YuiUiTheme.Field;
                list.GetComponent<Mask>().showMaskGraphic = true;
                var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(list.transform, false); content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1);
                const float rowHeight = 176;
                content.sizeDelta = new Vector2(0, page.Items.Count * rowHeight);
                var scroll = list.GetComponent<ScrollRect>(); scroll.viewport = list.GetComponent<RectTransform>(); scroll.content = content; scroll.horizontal = false;
                YuiControlAffordance.Scrollbar(scroll);
                for(var i=0;i<page.Items.Count;i++)
                {
                    var item=page.Items[i]; var preview=(item.Text??"").Replace('\n',' ').Replace('\r',' ');
                    if(preview.Length>100) preview=preview.Substring(0,100)+"…";
                    var entry=ComposerButton(content,"Entry"+i,"",
                        ()=>ShowHistoryEntry(item,isSaved,isBackend,archive),0,0,1,1,false);
                    var r=entry.GetComponent<RectTransform>(); r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);
                    r.anchoredPosition=new Vector2(0,-i*rowHeight);r.sizeDelta=new Vector2(0,rowHeight-12);
                    var oldLabel=entry.GetComponentInChildren<Text>();oldLabel.gameObject.SetActive(false);
                    var own=item.Speaker=="You";
                    entry.GetComponent<Image>().color=own?new Color(.18f,.21f,.25f):new Color(.22f,.19f,.28f);
                    HistoryCell(r,"Speaker",own?"あなた":item.Speaker??(isSaved?"保存済み":HistoryCharacterName(character)),.025f,.08f,.21f,.94f,true);
                    var stamp=DateTime.TryParse(item.CreatedUtc,out var time)?time.ToLocalTime().ToString("MM/dd HH:mm"):"";
                    HistoryCell(r,"Metadata",stamp+(string.IsNullOrWhiteSpace(item.Mode)?"":"  ·  "+(YuiChatRequestModes.IsWork(item.Mode)?"Work":"Talk")),.25f,.73f,.97f,.97f,false,true);
                    HistoryCell(r,"Message",preview,.25f,.06f,.97f,.71f,false);
                }
                scroll.verticalNormalizedPosition=1;
        }

        private string HistoryCharacterName(string id)
        {
            if(id==ChatCharacterId())return CharacterName;
            try { var profile=ProfileStore.Read(id);if(!string.IsNullOrWhiteSpace(profile?.Name))return profile.Name; } catch(Exception) { }
            var entry=YuiAvatarLibrary.Read().Find(e=>e.id==id);
            return entry?.name ?? (id=="builtin:"+YuiAvatarSlots.UnityChanDefault?"Unityちゃん":"Yui");
        }
        private static void HistoryCell(RectTransform parent,string name,string text,float left,float bottom,float right,float top,bool bold,bool muted=false)
        {
            var label=new GameObject(name,typeof(RectTransform),typeof(Text)).GetComponent<Text>();label.transform.SetParent(parent,false);
            Place(label.rectTransform,left,bottom,right,top);label.font=YuiChatLogStyle.ResolveFont(null);
            label.fontSize=YuiUiTypography.Caption;label.color=muted?YuiUiTheme.Muted:YuiUiTheme.Text;
            label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;label.alignment=TextAnchor.UpperLeft;
            label.supportRichText=false;label.raycastTarget=false;label.text=text;
        }
        private void ShowHistoryCharacterSelector()
        {
            var root=CreateSavedDataPanel("履歴の会話相手");
            var ids=new List<string>{ChatCharacterId(),"builtin:"+YuiAvatarSlots.UnityChanDefault};
            ids.Add("builtin:"+YuiBuildProfile.DefaultAvatarSlot);
            ids.AddRange(YuiAvatarLibrary.Read().Select(e=>e.id));ids=ids.Distinct().ToList();
            var viewport=new GameObject("Characters",typeof(RectTransform),typeof(Image),typeof(Mask),typeof(ScrollRect));
            viewport.transform.SetParent(root,false);Place(viewport.GetComponent<RectTransform>(),.03f,.18f,.97f,.82f);
            viewport.GetComponent<Image>().color=YuiUiTheme.Field;viewport.GetComponent<Mask>().showMaskGraphic=true;
            var content=new GameObject("Content",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(viewport.transform,false);
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,ids.Count*120);
            var scroll=viewport.GetComponent<ScrollRect>();scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=content;scroll.horizontal=false;
                YuiControlAffordance.Scrollbar(scroll);
            for(var i=0;i<ids.Count;i++) {
                var id=ids[i];var button=ComposerButton(content,"Character"+i,HistoryCharacterName(id)+(id==ChatCharacterId()?" · 会話中":""),()=>{historyCharacter=id;historyPage=0;historySnapshot=-1;historySession=ChatSessionId(id);ShowHistory();},0,0,1,1,false);
                var rect=button.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-120*i);rect.sizeDelta=new Vector2(0,108);
            }
            ComposerButton(root,"Back","戻る",ShowHistory,.03f,.04f,.97f,.14f);
        }

        private void ShowHistoryDeletion()
        {
            var root=CreateSavedDataPanel("端末内の会話履歴を削除");
            SavedDataText(root,"削除する範囲を選んでください。Talk・Workの履歴とAIに渡す最近の会話を削除します。\n\n保存済み回答、Backend側の履歴・記憶、アバター、設定は残ります。削除は取り消せません。");
            ComposerButton(root,"Character",HistoryCharacterName(historyCharacter??ChatCharacterId())+" の履歴",()=>ConfirmHistoryDeletion(false),.03f,.13f,.97f,.23f);
            ComposerButton(root,"All","全キャラクターの履歴",()=>ConfirmHistoryDeletion(true),.03f,.015f,.97f,.115f);
        }

        private void EditMessageForResend(string message)
        {
            if (HasStoppableComposerOperation || isSending) { SetStatus("返答を停止してから編集してください。"); return; }
            var root=CreateSavedDataPanel("発言を編集して再送");
            var field=SavedDataField(root,message,false);
            root.gameObject.AddComponent<YuiMobileEditorLayout>();
            var note=ComposerButton(root,"Note","元の会話は残ります。現在の会話に新しい発言として送ります。入力欄の下書き・添付は置き換えます。",()=>{},.03f,.15f,.97f,.23f);
            note.interactable=false;note.GetComponentInChildren<Text>().fontSize=YuiUiTypography.Caption;
            ComposerButton(root,"UseEdited","送信",()=> {
                if(inputField==null || string.IsNullOrWhiteSpace(field.text) || isSending || HasStoppableComposerOperation)return;
                inputField.text=field.text;
                pendingVisionImageAttachment.MarkConsumedAfterSuccessfulChat();latestVision=null;UpdateComposerState();
                field.DeactivateInputField();Destroy(root.gameObject);SendCurrentInput();
            },.03f,.03f,.97f,.13f);
        }

        private void ConfirmHistoryDeletion(bool all)
        {
            var character=historyCharacter??ChatCharacterId();
            var root=CreateSavedDataPanel("履歴削除の確認");
            SavedDataText(root,all?"この端末の全キャラクターの会話履歴と、その発言から自動保存されたメモを削除します。保存済み回答とBackend側の記録は残ります。取り消せません。":HistoryCharacterName(character)+" のTalk・Workの会話履歴と、その発言から自動保存されたメモを端末から削除します。保存済み回答とBackend側の記録は残ります。取り消せません。");
            var busy=false;
            ComposerButton(root,"Cancel","キャンセル",ShowHistory,.03f,.06f,.47f,.18f);
            ComposerButton(root,"ConfirmDelete","削除する",async ()=> {
                if(busy)return;
                if(HasStoppableComposerOperation || isSending || deviceSyncBusy) { SetStatus("会話または同期が終わってから消去してください。");return; }
                busy=true;
                try {
                    // No background send can begin between the guard and local deletion.
                    YuiSyncFileTransaction.BeginHistoryForget(Application.persistentDataPath);
                    try {
                    if(all) {
                        CharacterMemoryStore.ForgetAllHistory();
                        var directory=Path.Combine(Application.persistentDataPath,"ConversationHistory");
                        if(Directory.Exists(directory))foreach(var file in Directory.GetFiles(directory,"*.jsonl"))new YuiTextArchive(file).Clear();
                        DialogueStore.ClearAll();
                        PlayerPrefs.SetString("Yui.ChatSession.Generation",Guid.NewGuid().ToString("N"));
                    } else {
                        CharacterMemoryStore.ForgetAllHistory(character);
                        ConversationArchive(character).Clear();
                        foreach(var mode in new[]{"talk","work"}) { DialogueStore.Clear(character,mode);PlayerPrefs.DeleteKey("Yui.ChatSession."+character+"."+mode+PlayerPrefs.GetString("Yui.ChatSession.Generation","")); }
                    }
                    } finally { YuiSyncFileTransaction.CompleteHistoryForget(Application.persistentDataPath); }
                    PlayerPrefs.Save(); retryChatMessage=null; historySnapshot=-1; historyPage=0; historyGeneration++;
                    chatLogView?.Clear(); await RestoreConversationViewAsync();
                    if(root!=null && savedDataPanel==root.gameObject)ShowHistory();
                    SetStatus("端末内の会話履歴を削除しました");
                } catch(Exception ex) { SetStatus("削除できません: "+ex.Message);busy=false; }
            },.53f,.06f,.97f,.18f);
        }

        private static string HistoryCaption(YuiTextArchive.Entry item)
        {
            var date=DateTime.TryParse(item.CreatedUtc,out var time)?time.ToLocalTime().ToString("yyyy/MM/dd HH:mm"):YuiUiLocalization.Text("Earlier record");
            return date+(string.IsNullOrEmpty(item.Speaker)?"":" · "+(item.Speaker=="You"?YuiUiLocalization.Text("You"):item.Speaker))+(string.IsNullOrEmpty(item.Mode)?"":" · "+(YuiChatRequestModes.IsWork(item.Mode)?"Work":"Talk"));
        }

        private void ShowHistoryEntry(YuiTextArchive.Entry item,bool saved,bool backend,YuiTextArchive archive)
        {
            var root=CreateSavedDataPanel(HistoryCaption(item),false);
            SavedDataText(root,item.Text??"");
            if (!string.IsNullOrWhiteSpace(item.Text) && item.Speaker != "You" && item.Speaker != "System")
            {
                var reader=root.Find("Reader")?.GetComponent<RectTransform>();
                if (reader != null) Place(reader,.03f,.32f,.97f,.83f);
                Button replay=null;
                replay=ComposerButton(root,"ReadAloud","Read aloud with current voice",()=>_ = ReadHistoryAloudAsync(item.Text,replay),.03f,.235f,.97f,.305f);
                replay.interactable = !IsTtsMode("silent");
            }
            ComposerButton(root,"Copy","Copy",()=>GUIUtility.systemCopyBuffer=item.Text??"",.03f,.12f,.31f,.21f);
            ComposerButton(root,"Back","Back",ShowHistory,.69f,.12f,.97f,.21f);
            if(!backend)
            {
                var confirmed=false;var busy=false;Button remove=null;
                var store=ResultStore;
                var character = historyCharacter??ChatCharacterId();
                remove=ComposerButton(root,"Delete","Delete",async ()=>
                {
                    if(busy) return;
                    if(!confirmed) { confirmed=true;YuiUiLocalization.Set(remove.GetComponentInChildren<Text>(),"Delete this entry");return; }
                    if(!saved && (HasStoppableComposerOperation || deviceSyncBusy)) { SetStatus("会話または同期が終わってから消去してください。");return; }
                    busy=true;
                    try
                    {
                        var localRoot=Application.persistentDataPath;
                        await Task.Run(()=> { if(saved) store.Remove(item.Id);else {
                            YuiSyncFileTransaction.BeginHistoryForget(localRoot);
                            try { CharacterMemoryStore.ForgetHistory(character,item.Id);archive.Remove(item.Id); }
                            finally { YuiSyncFileTransaction.CompleteHistoryForget(localRoot); }
                        } });
                        if(!saved) { DialogueStore.Clear(character,item.Mode??historyMode);historySnapshot=-1;await RestoreConversationViewAsync(); }
                        if(root!=null && savedDataPanel==root.gameObject) ShowHistory();
                    }
                    catch(Exception ex) { SetStatus("削除できません: "+ex.Message);busy=false; }
                },.34f,.12f,.66f,.21f);
            }
            var scopeLabel=ComposerButton(root,"Scope",backend?"Stored on the backend":saved?"Stored on this device":"Deletes this device’s copy; backend records are managed in Advanced.",()=>{},.03f,.03f,.97f,.095f);
            scopeLabel.interactable=false;
            scopeLabel.GetComponentInChildren<Text>().fontSize=YuiUiTypography.Caption;
        }

        private CancellationTokenSource historyVoiceCancellation;
        private async Task ReadHistoryAloudAsync(string text, Button button)
        {
            if (historyVoiceCancellation != null) { historyVoiceCancellation.Cancel(); return; }
            if (HasStoppableComposerOperation || isSending) { SetStatus("Stop the current response before reading history aloud."); return; }
            using var operation=CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
            historyVoiceCancellation=activeVoiceCancellation=operation;
            isSending=true;SetInteractable(false);
            YuiUiLocalization.Set(button.GetComponentInChildren<Text>(),"Stop");
            try
            {
                // Recreate speech only; never resubmit old text to the LLM or append it to memory/history.
                await SpeakResponseAsync(new ChatResponse { Text=text, SpokenText=text, ShouldTts=true },
                    "history-"+Guid.NewGuid().ToString("N"),operation.Token);
            }
            catch(OperationCanceledException) { SetStatus("停止しました"); }
            catch(Exception ex) { SetStatus("Speech playback failed. Check the selected voice in Settings.");Debug.LogWarning("History speech: "+ex.GetType().Name); }
            finally
            {
                if(activeVoiceCancellation==operation)activeVoiceCancellation=null;
                historyVoiceCancellation=null;isSending=false;SetInteractable(true);
                if(button!=null)YuiUiLocalization.Set(button.GetComponentInChildren<Text>(),"Read aloud with current voice");
            }
        }

        private void MigrateRecentDialogue()
        {
            if (secretMode) return;
            var character = ChatCharacterId();
            var marker = "yui_history_migrated_v1_" + character;
            if (PlayerPrefs.GetInt(marker, 0) == 1) return;
            try
            {
                var archive = ConversationArchive(character);
                foreach (var mode in new[] { YuiChatRequestModes.Talk, YuiChatRequestModes.Work })
                {
                    var turns = DialogueStore.Read(character, mode);
                    for (var i = 0; i < turns.Count; i++)
                    {
                        // Stable IDs make a retried migration idempotent. These legacy
                        // records have no timestamp and may already be shortened.
                        archive.Append(new YuiTextArchive.Entry { Id = "legacy-" + mode + "-" + i + "-u", Speaker = "You", Text = turns[i].User, Mode = mode });
                        archive.Append(new YuiTextArchive.Entry { Id = "legacy-" + mode + "-" + i + "-a", Speaker = CharacterName, Text = turns[i].Assistant, Mode = mode });
                    }
                }
                PlayerPrefs.SetInt(marker, 1); PlayerPrefs.Save();
            }
            catch (Exception ex) { SetStatus("History could not be opened. Your data is retained."); Debug.LogWarning(ex.Message); }
        }

        private async Task RestoreConversationViewAsync()
        {
            var generation=++historyGeneration;
            var character=ChatCharacterId();var mode=chatInteractionMode;
            if(secretMode) { chatLogView?.Clear();return; }
            var archive=ConversationArchive(character);
            try
            {
                var page=await Task.Run(()=>archive.ReadPage(0,100,mode));
                if(this==null || secretMode || generation!=historyGeneration || character!=ChatCharacterId() || mode!=chatInteractionMode) return;
                chatLogView?.Restore(page.Items.AsEnumerable().Reverse());
            }
            catch(Exception ex) { SetStatus("History could not be opened. Your data is retained.");Debug.LogWarning(ex.Message); }
        }

        public void OpenCharacterLibrary() => ShowAvatarLibrary();
        public void OpenAvatarGuide() => ShowAvatarImportGuide();
        public void OpenCharacterMemories() { localMemoryPage=0;_=ShowMemoriesAsync(); }
        public void OpenBackendMemories() { _=ShowBackendMemoriesAsync(); }
    }
}
