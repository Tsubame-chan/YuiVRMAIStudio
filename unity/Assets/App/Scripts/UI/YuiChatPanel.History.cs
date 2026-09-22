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

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
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
            var root = CreateSavedDataPanel("History");
            var character = historyCharacter ?? ChatCharacterId();
            var isSaved = savedHistory; var isBackend = backendHistory; var pageNumber = historyPage;
            var archive = ConversationArchive(character); var store = ResultStore;
            ComposerButton(root,"Conversations","Conversations",()=> { savedHistory=backendHistory=false;historyPage=0;historySnapshot=-1;ShowHistory(); },.03f,.77f,.49f,.85f);
            ComposerButton(root,"SavedAnswers","Saved answers",()=> { savedHistory=true;backendHistory=false;historyPage=0;ShowHistory(); },.51f,.77f,.97f,.85f);
            root.Find(isSaved ? "SavedAnswers" : "Conversations").GetComponent<Image>().color=YuiUiTheme.Selected;
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
                    var response=await client.GetRecentConversationsAsync(userId,6,cancellationTokenSource.Token,character,historySession,pageNumber*6);
                    page=new YuiTextArchive.Page { HasOlder=response.Items.Count==6 };
                    foreach(var item in response.Items.AsEnumerable().Reverse())
                        page.Items.Add(new YuiTextArchive.Entry { Speaker=item.Role=="assistant"?CharacterName:"You",Text=item.Message,CreatedUtc=item.CreatedAt,Mode=historyMode });
                }
                else
                {
                    if (!isSaved && historySnapshot < 0) historySnapshot=archive.Length;
                    var snapshot=historySnapshot;
                    page=await Task.Run(()=>isSaved ? store.Page(pageNumber*6,6) : archive.ReadPage(pageNumber*6,6,null,snapshot));
                }
                if(root==null || savedDataPanel!=root.gameObject) return;
                for(var i=0;i<page.Items.Count;i++)
                {
                    var item=page.Items[i]; var preview=(item.Text??"").Replace('\n',' ').Replace('\r',' ');
                    if(preview.Length>70) preview=preview.Substring(0,70)+"…";
                    ComposerButton(root,"Entry"+i,HistoryCaption(item)+"\n"+preview,
                        ()=>ShowHistoryEntry(item,isSaved,isBackend,archive),.03f,.65f-i*.085f,.97f,.73f-i*.085f,false);
                }
                if(page.Items.Count==0) ComposerButton(root,"Empty","Nothing saved yet",()=>{},.04f,.4f,.96f,.65f).interactable=false;
                ComposerButton(root,"Newer","Newer",()=>{historyPage--;ShowHistory();},.03f,.12f,.27f,.20f).interactable=pageNumber>0;
                ComposerButton(root,"Page",(pageNumber+1).ToString(),()=>{},.29f,.12f,.45f,.20f,false).interactable=false;
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
                var character = historyCharacter;
                remove=ComposerButton(root,"Delete","Delete",async ()=>
                {
                    if(busy) return;
                    if(!confirmed) { confirmed=true;YuiUiLocalization.Set(remove.GetComponentInChildren<Text>(),"Delete this entry");return; }
                    if(!saved && HasStoppableComposerOperation) { SetStatus("返答を停止してから消去してください。");return; }
                    busy=true;
                    try
                    {
                        await Task.Run(()=> { if(saved) store.Remove(item.Id);else archive.Remove(item.Id); });
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
        public void OpenBackendMemories() { _=ShowMemoriesAsync(); }
    }
}
