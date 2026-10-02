using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Avatar;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private bool deviceSyncBusy;
        private string syncConnectionChecked;
        private string SyncStateFile => Path.Combine(Application.persistentDataPath,"DeviceSync",YuiSyncCredentialStore.ServerKey(client.BaseUrl),"state.json");
        private JObject ReadSyncState()
        {
            YuiSyncFileTransaction.Recover(Application.persistentDataPath);
            return File.Exists(SyncStateFile)?JObject.Parse(File.ReadAllText(SyncStateFile)):new JObject();
        }
        public void OpenDeviceSync()
        {
            if(!CanStartDeviceSync())return;
            var dialog=YuiSimpleDialog.Create("キャラクターと会話を同期",$"送り先: {client.BaseUrl}\n\n名前・人格の指示・記憶・会話履歴を、このPCと登録した端末で共有します。初回は共有先のキャラクターを選び、差分を確認してから反映します。声の設定、APIキー、アバターは送りません。");
            dialog.AddButton("登録済みの端末で差分を確認",()=>{dialog.Close();_=StartDeviceSyncAsync(false);});
            dialog.AddButton("この端末をPCに登録",()=>{dialog.Close();OpenSyncPairing();});
            dialog.AddButton("閉じる",dialog.Close);dialog.Compact(620);
        }
        private bool CanStartDeviceSync()
        {
            if(secretMode){SetStatus("Secret Modeを終了してから同期してください。Secret中の会話は送信されません。");return false;}
            if(deviceSyncBusy || !CanChangeCharacter())return false;
            if(!characterProfileWritable){SetStatus("人格データを読み込めません。元ファイルを確認してから同期してください。");return false;}
            return true;
        }
        private void OpenSyncPairing()
        {
            var code="";
            var dialog=YuiSimpleDialog.Create("端末を登録","PCのBackend Console → 接続と設定 → 端末と同期で登録コードを表示してください。コードは10分間・1回有効です。");
            var input=dialog.AddInput("登録コード","",v=>code=v);input.characterLimit=16;input.lineType=UnityEngine.UI.InputField.LineType.SingleLine;
            UnityEngine.UI.Button submit=null;
            submit=dialog.AddButton("登録する",async()=> {
                if(!CanStartDeviceSync())return;
                submit.interactable=false;
                try {
                    deviceSyncBusy=true;
                    var deviceName=SystemInfo.deviceName??"Yui app";
                    var data=await client.SyncAsync("/sync/pair",new JObject{{"name",deviceName.Substring(0,Math.Min(80,deviceName.Length))},{"code",code.Trim()}},null,cancellationTokenSource.Token);
                    YuiSyncCredentialStore.Write(client.BaseUrl,(string)data["token"]);
                    dialog.Close();SetStatus("端末を登録しました。");
                }catch(Exception){dialog.Body.text="登録できませんでした。接続先URLとコードを確認してください。コードが期限切れの場合はPCで再発行してください。";}
                finally{deviceSyncBusy=false;if(submit!=null)submit.interactable=true;}
            });
            dialog.AddButton("戻る",()=>{if(!deviceSyncBusy){dialog.Close();OpenDeviceSync();}});dialog.Compact(620);
        }
        private static Task<bool> SyncConfirm(string title,string message,string accept)
        {
            var completion=new TaskCompletionSource<bool>();
            var dialog=YuiSimpleDialog.Create(title,message);
            dialog.AddButton(accept,()=>{dialog.Close();completion.TrySetResult(true);});
            dialog.AddButton("今回は同期しない",()=>{dialog.Close();completion.TrySetResult(false);});dialog.Compact(620);
            return completion.Task;
        }
        private async Task<string> ChooseSharedCharacter(JArray choices)
        {
            var done=new TaskCompletionSource<string>();
            var dialog=YuiSimpleDialog.Create("共有するキャラクターを選択","同じ名前でも、自動で同一のキャラクターにはしません。既存につなぐ場合は対象を選んでください。別々に保つ場合は新しく追加します。");
            dialog.AddButton("新しい共有キャラクターとして追加",()=>{dialog.Close();done.TrySetResult("");});
            foreach(var item in choices) {
                var id=(string)item["id"];var name=(string)item["name"];
                dialog.AddButton(name+" · "+id.Substring(0,Math.Min(6,id.Length)),()=>{dialog.Close();done.TrySetResult(id);});
            }
            dialog.AddButton("戻る",()=>{dialog.Close();done.TrySetResult(null);});dialog.Compact(620);
            return await done.Task;
        }
        private JObject CaptureSyncData()
        {
            // Refuse to overwrite files damaged outside the running UI.
            ProfileStore.Read(ChatCharacterId());
            var result=new JObject();
            result["profile:name"]=new JObject{{"text",characterName??"Yui"}};
            result["profile:instruction"]=new JObject{{"text",customInstruction??""}};
            foreach(var m in new YuiCharacterMemoryStore(Path.Combine(Application.persistentDataPath,"CharacterMemory")).Read(ChatCharacterId()))result["memory:"+m.Id]=new JObject{{"content",m.Content},{"pinned",m.Pinned},{"recorded_utc",m.CreatedUtc??""}};
            var page=ConversationArchive(ChatCharacterId()).ReadPage(0,18000);
            if(page.HasOlder)throw new InvalidDataException("履歴が同期の上限を超えています。元ファイルは保持しています。");
            if(page.DamagedLines>0)throw new InvalidDataException("履歴に読み込めない行があります。元ファイルは保持しています。");
            foreach(var h in page.Items) {
                JObject metadata=null;try{if(!string.IsNullOrEmpty(h.Metadata))metadata=JObject.Parse(h.Metadata);}catch(JsonException){}
                result["history:"+h.Id]=new JObject{{"text",h.Text??""},{"speaker",h.Speaker??""},{"mode",h.Mode??"talk"},{"recorded_utc",h.CreatedUtc??""},
                    {"conversation_id",(string)metadata?["session_id"]??"legacy:"+h.Id},
                    {"turn_id",(string)metadata?["request_id"]??h.Id}};
            }
            return result;
        }
        private static JArray SyncOperations(JObject current,JArray agreed)
        {
            var baseline=agreed?.ToDictionary(x=>(string)x["kind"]+":"+(string)x["id"],x=>x)??new Dictionary<string,JToken>();
            var keys=current.Properties().Select(x=>x.Name).Union(baseline.Keys).OrderBy(x=>x,StringComparer.Ordinal);
            var items=new JArray();
            foreach(var key in keys) {
                baseline.TryGetValue(key,out var previous);var value=current[key];var deleted=value==null;
                if(deleted && (bool?)previous?["deleted"]==true)continue;
                var separator=key.IndexOf(':');
                items.Add(new JObject{{"kind",key.Substring(0,separator)},{"id",key.Substring(separator+1)},
                    {"base_version",(long?)previous?["version"]??0},{"changed",previous==null || deleted!=(bool)previous["deleted"] || !JToken.DeepEquals(value,previous["value"])},
                    {"deleted",deleted},{"value",deleted?new JObject():value.DeepClone()}});
            }
            return items;
        }
        private bool SyncStillCurrent(string character,string server,JObject before)
            => !secretMode && ChatCharacterId()==character && client.BaseUrl==server && JToken.DeepEquals(CaptureSyncData(),before);
        private async Task StartDeviceSyncAsync(bool onConnection)
        {
            if(!CanStartDeviceSync())return;
            deviceSyncBusy=true;
            try {
                var character=ChatCharacterId();var server=client.BaseUrl;var syncClient=new YuiBackendClient(server);
                var token=YuiSyncCredentialStore.Read(server);
                if(string.IsNullOrEmpty(token)){if(!onConnection)SetStatus("先に「この端末をPCに登録」を選んでください。");return;}
                var state=ReadSyncState();
                // Metadata only: never send persona or history before initial consent.
                var choices=await syncClient.SyncAsync("/sync/characters",null,token,cancellationTokenSource.Token);
                var serverId=(string)choices["server_id"];
                if(string.IsNullOrEmpty(serverId))throw new InvalidDataException("接続先の同期機能を確認できません。Backendを更新してください。");
                if((string)state["server_id"]!=serverId)state=new JObject{{"server_id",serverId}};
                var binding=state[character] as JObject;var newBinding=binding==null;
                var before=CaptureSyncData();
                if(binding==null) {
                    if(onConnection)return;
                    if(!await SyncConfirm("共有するデータを確認",$"送り先: {server}\nキャラクター: {characterName}\n\n人格設定2項目、記憶{before.Properties().Count(x=>x.Name.StartsWith("memory:"))}件、履歴{before.Properties().Count(x=>x.Name.StartsWith("history:"))}件を送信して差分を確認します。本文はこのPCに保存されます。\n\n同期前の退避コピーは端末内のDeviceSync/Backupsに残ります。", "この範囲で差分を確認"))return;
                    if(!SyncStillCurrent(character,server,before))throw new InvalidOperationException("内容が変更されました。もう一度確認してください。");
                    var shared=await ChooseSharedCharacter((JArray)choices["items"]);
                    if(shared==null)return;
                    if(shared.Length==0)shared=(string)(await syncClient.SyncAsync("/sync/characters",new JObject{{"name",characterName}},token,cancellationTokenSource.Token))["id"];
                    binding=new JObject{{"shared_id",shared},{"items",new JArray()}};
                }
                var operations=SyncOperations(before,(JArray)binding["items"]);
                var plan=await syncClient.SyncAsync("/sync/plan",new JObject{{"character_id",binding["shared_id"]},{"items",operations}},token,cancellationTokenSource.Token);
                var conflicts=(JArray)plan["conflicts"];var upload=(JObject)plan["upload"];
                var updates=upload.Properties().Sum(p=>(int)p.Value)+(int)plan["download"]+conflicts.Count;
                if(updates==0&&!newBinding){if(!onConnection)SetStatus("同期する変更はありません。");return;}
                var downloads=plan["download_counts"];
                if(updates>0&&!await SyncConfirm("同期する変更",$"送り先: {server}\n\nこの端末から: 人格{upload["profile"]}項目、記憶{upload["memory"]}件、履歴{upload["history"]}件、削除{upload["deletions"]}件\nPCから: 人格{downloads["profile"]}項目、記憶{downloads["memory"]}件、履歴{downloads["history"]}件、削除{downloads["deletions"]}件\n内容の選択が必要: {conflicts.Count}件\n\n履歴は両方を残します。最近の会話の文脈は一つに連結しません。",conflicts.Count>0?"内容を選んで同期":"両方に反映"))return;
                var decisions=new JObject();
                foreach(var conflict in conflicts) {
                    var completion=new TaskCompletionSource<string>();
                    var key=(string)conflict["key"];
                    var label=key=="profile:name"?"キャラクターの名前":key=="profile:instruction"?"人格の指示":key.StartsWith("memory:")?"記憶":"会話履歴";
                    var dialog=YuiSimpleDialog.Create("変更が重なっています",$"{label}\n\nこの端末:\n{DescribeSyncConflict(conflict["local"])}\n\nPCの共有版:\n{DescribeSyncConflict(conflict["remote"])}");
                    dialog.AddButton("この端末の内容を使う",()=>{dialog.Close();completion.TrySetResult("local");});
                    dialog.AddButton("PCの共有版を使う",()=>{dialog.Close();completion.TrySetResult("remote");});
                    dialog.AddButton("今回は同期しない",()=>{dialog.Close();completion.TrySetResult(null);});dialog.Compact(700);
                    var choice=await completion.Task;if(choice==null)return;decisions[key]=choice;
                }
                if(!SyncStillCurrent(character,server,before))throw new InvalidOperationException("内容が変更されました。もう一度差分を確認してください。");
                var merged=await syncClient.SyncAsync("/sync/commit",new JObject{{"plan_id",plan["plan_id"]},{"choices",decisions}},token,cancellationTokenSource.Token);
                if((string)merged["server_id"]!=serverId || (string)merged["character_id"]!=(string)binding["shared_id"])throw new InvalidDataException("同期先が変更されました。元データを保持しています。もう一度差分を確認してください。");
                if(!SyncStillCurrent(character,server,before))throw new InvalidOperationException("PCに保存しましたが、端末内で変更があったため上書きしませんでした。もう一度同期してください。");
                ApplySyncSnapshot(character,state,merged);
                EnsureBackendMonitorIfNeeded();
                SetStatus("キャラクターと会話を同期しました。");
            }catch(Exception ex){SetStatus(DeviceSyncError(ex));Debug.LogWarning("Device sync: "+ex.GetType().Name);}
            finally{deviceSyncBusy=false;}
        }
        private static string DescribeSyncConflict(JToken side)
        {
            if(side==null || (bool?)side["deleted"]==true)return "削除";
            var value=side["value"];var kind=(string)side["kind"];
            if(kind=="memory")return $"{value["content"]}\n固定: {((bool)value["pinned"]?"する":"しない")}\n記録日時: {value["recorded_utc"]}";
            if(kind=="history")return $"{value["speaker"]} · {value["mode"]} · {value["recorded_utc"]}\n{value["text"]}";
            return (string)value["text"];
        }
        private static string DeviceSyncError(Exception ex)
        {
            if(ex is YuiBackendException backend) {
                switch(backend.StatusCode) {
                    case 401:return "端末の登録が無効です。PCで新しいコードを表示し、この端末を登録し直してください。元データは保持しています。";
                    case 404:return "共有キャラクターが見つかりません。接続先とPCの同期用データを確認してください。元データは保持しています。";
                    case 409:return "別の端末での更新、確認の期限切れ、またはPC側の復元により差分が変わりました。もう一度差分を確認してください。元データは保持しています。";
                    case 413:return "同期データの上限を超えています。今回の変更は反映していません。元データは保持しています。";
                    case 422:return "同期データの形式を確認できません。アプリとBackendを更新してください。元データは保持しています。";
                    case 429:return "差分確認が多すぎます。10分ほど待ってから再度確認してください。元データは保持しています。";
                }
            }
            return ex is InvalidDataException || ex is InvalidOperationException?ex.Message:"同期できませんでした。元データは保持しています。接続と端末の登録を確認してください。";
        }
        private bool HasRegisteredDeviceSync()
        {
            if(client==null || secretMode)return false;
            try{return !string.IsNullOrEmpty(YuiSyncCredentialStore.Read(client.BaseUrl)) && ReadSyncState()[ChatCharacterId()] is JObject;}
            catch(Exception){return false;}
        }
        private void ApplySyncSnapshot(string character,JObject state,JObject snapshot)
        {
            var items=(JArray)snapshot["items"];var profile=CaptureCharacterProfile();
            var memories=new List<YuiCharacterMemoryStore.Entry>();var history=new List<YuiTextArchive.Entry>();
            var existing=ConversationArchive(character).ReadPage(0,18000).Items.ToDictionary(x=>x.Id,x=>x);
            foreach(var item in items) {
                if((bool)item["deleted"])continue;
                var value=item["value"];var id=(string)item["id"];
                switch((string)item["kind"]) {
                    case "profile":if(id=="name")profile.Name=(string)value["text"];else if(id=="instruction")profile.Instruction=(string)value["text"];break;
                    case "memory":memories.Add(new YuiCharacterMemoryStore.Entry{Id=id,Content=(string)value["content"],Pinned=(bool)value["pinned"],CreatedUtc=(string)value["recorded_utc"]});break;
                    case "history":
                        existing.TryGetValue(id,out var old);
                        history.Add(new YuiTextArchive.Entry{Id=id,Speaker=(string)value["speaker"],Text=(string)value["text"],Mode=(string)value["mode"],CreatedUtc=(string)value["recorded_utc"],
                            Metadata=old?.Metadata??new JObject{{"session_id",value["conversation_id"]},{"request_id",value["turn_id"]},{"sync_origin_device",item["origin_device"]}}.ToString(Formatting.None)});break;
                }
            }
            state[character]=new JObject{{"shared_id",snapshot["character_id"]},{"items",items.DeepClone()},{"synced_utc",DateTime.UtcNow.ToString("o")}};
            DateTimeOffset SortTime(YuiTextArchive.Entry e)=>DateTimeOffset.TryParse(e.CreatedUtc,out var time)?time:DateTimeOffset.MinValue;
            var files=new Dictionary<string,string>{
                [ProfileStore.SyncFilePath(character)]=JsonConvert.SerializeObject(profile,Formatting.Indented),
                [CharacterMemoryStore.SyncFilePath(character)]=JsonConvert.SerializeObject(memories),
                [ConversationArchive(character).SyncFilePath]=string.Join("\n",history.OrderBy(SortTime).ThenBy(x=>x.Id,StringComparer.Ordinal).Select(x=>JsonConvert.SerializeObject(x)))+"\n",
                [SyncStateFile]=state.ToString(Formatting.Indented)};
            YuiSyncFileTransaction.Apply(Application.persistentDataPath,files);
            CharacterMemoryStore.Invalidate();characterName=profile.Name;customInstruction=profile.Instruction;historyGeneration++;
            ConfigureAiRuntimeRouter();_=RestoreConversationViewAsync();
        }
        private void CheckDeviceSyncOnConnection()
        {
            var key=client.BaseUrl+"|"+ChatCharacterId();if(syncConnectionChecked==key || deviceSyncBusy || secretMode || !HasRegisteredDeviceSync() || !CanStartDeviceSync())return;
            syncConnectionChecked=key;_=StartDeviceSyncAsync(true);
        }
    }
}
