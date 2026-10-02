using System;
using UnityEngine;
using YuiPhysicalAI.Api;
namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private string backendVoiceProfileId="", backendVoiceProfileName="", backendVoiceServer="";
        public async void ChooseBackendVoiceProfile()
        {
            if(!CanSwitchLocalModel){SetStatus("会話や音声を停止してから声を変更してください。");return;}
            var owner=ChatCharacterId();var server=backendUrl;
            var dialog=YuiSimpleDialog.Create(YuiSimpleDialog.L("Backendの声", "Backend voices"),YuiSimpleDialog.L("声一覧を取得しています…", "Loading saved voices…"));
            dialog.AddButton(YuiSimpleDialog.L("閉じる", "Close"),()=>dialog.Close());
            try {
                // Metadata only; no conversation or microphone data is sent here.
                var result=await client.GetVoiceProfilesAsync(cancellationTokenSource.Token);
                if(dialog==null)return;
                dialog.Body.text=YuiSimpleDialog.L("コンソールで保存した声を、このキャラクターに割り当てます。声の調整はBackend側で行います。", "Assign a voice saved in Backend Console to this character. Edit its parameters in the console.");
                foreach(var item in result.Items ?? Array.Empty<BackendVoiceProfile>()) {
                    var profile=item;
                    dialog.AddButton(profile.Name+" · "+profile.Provider,()=>{
                        if(!CanSwitchLocalModel || owner!=ChatCharacterId() || server!=backendUrl){SetStatus("キャラクターまたは接続先が変わりました。声を選び直してください。");return;}
                        backendVoiceProfileId=profile.Id;backendVoiceProfileName=profile.Name;backendVoiceServer=server;ttsMode="backend-profile";
                        SaveCharacterProfile();ConfigureAiRuntimeRouter();ConfigureChatdollKitVoicevoxTts();
                        SetStatus("Backendの声: "+profile.Name);dialog.Close();
                    });
                }
                if(result.Items==null || result.Items.Length==0)dialog.Body.text=YuiSimpleDialog.L("保存された声がありません。Backend Console → AIと音声 → 読み上げで作成してください。", "No saved voices. Create one in Backend Console → AI and voice → Speech.");
                dialog.Compact(600);
            }catch(Exception ex){if(dialog!=null)dialog.Body.text=YuiSimpleDialog.L("声一覧を取得できません。Backendの接続とバージョンを確認してください。", "Could not load saved voices. Check the Backend connection and version.");Debug.LogWarning("Backend voice list: "+ex.GetType().Name);}
        }
    }
}
