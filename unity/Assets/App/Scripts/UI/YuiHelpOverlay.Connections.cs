using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiHelpOverlay
    {
        private bool deviceExpanded, backendExpanded, allBackendServices;
        private Button deviceGroup, backendGroup, allServicesButton;

        private void RenderConnectionGroups(ref float top)
        {
            var heading=HelpText(modernContent,"ConnectionHeading","Your connections",YuiUiTypography.Heading,true);
            RowRect(heading.rectTransform,top,58);top+=76;
            var localAi=chatPanel!=null && chatPanel.HasInstalledLocalChat;
            var localVoice=chatPanel!=null && chatPanel.HasInstalledNativeVoicevox;
            var count=(localAi?1:0)+(localVoice?1:0);
            deviceGroup=ConnectionGroup("DeviceGroup","On-device features",string.Format(YuiUiLocalization.Text("{0} installed"),count),
                deviceExpanded,()=>{deviceExpanded=!deviceExpanded;RenderModernHelp();},ref top);
            if(deviceExpanded)
            {
                StatusRow("On-device AI",localAi?"installed":"not_installed",ref top,true);
                StatusRow("On-device VOICEVOX",localVoice?"installed":"not_installed",ref top,true);
                top+=12;
            }
            StatusRow("App OpenAI key",chatPanel!=null && !string.IsNullOrWhiteSpace(chatPanel.OpenAiApiKey)?"configured":"missing_key",ref top);
            var mode=chatPanel!=null?chatPanel.ConversationMode:PlayerPrefs.GetString(YuiPrefsKeys.ConversationMode,YuiConversationModes.LocalAi);
            var voice=chatPanel!=null?chatPanel.TtsMode:PlayerPrefs.GetString(YuiPrefsKeys.TtsMode,"silent");
            var state=backendOnline?BackendGroupStatus(latestStatus,mode,voice):statusChecked?"offline":"unknown";
            backendGroup=ConnectionGroup("BackendGroup","Backend",HumanStatus(state),backendExpanded,
                ()=>{backendExpanded=!backendExpanded;RenderModernHelp();},ref top);
            var badge=backendGroup.transform.Find("Status").GetComponent<Text>();
            badge.color=state=="degraded"?new Color32(240,195,135,255):backendOnline?YuiUiTheme.Accent:YuiUiTheme.Muted;
            if(backendExpanded)
            {
                if(backendOnline)
                {
                    StatusRow("Database",latestStatus?.Database?.Status??"unknown",ref top,true);
                    var hidden=0;
                    if(latestStatus?.Providers!=null)
                    {
                        foreach(var pair in latestStatus.Providers)
                        {
                            if(!ShouldShowBackendService(pair.Key,pair.Value,latestStatus.ChatProvider,mode,voice,allBackendServices)) {hidden++;continue;}
                            StatusRow(ProviderLabel(pair.Key,pair.Value),pair.Value?.Status??"unknown",ref top,true);
                        }
                    }
                    if(hidden>0 || allBackendServices)
                    {
                        if(allServicesButton==null) allServicesButton=HelpButton(modernContent,"AllServices","Show all services",()=>
                        {allBackendServices=!allBackendServices;RenderModernHelp();});
                        allServicesButton.gameObject.SetActive(true);
                        YuiUiLocalization.Set(allServicesButton.GetComponentInChildren<Text>(),allBackendServices?"Show relevant services":"Show all services");
                        RowRect((RectTransform)allServicesButton.transform,top,68);top+=86;
                    }
                }
                else ConnectionNote("BackendOfflineNote","No backend connection. On-device AI and direct OpenAI access work without one.",ref top);
            }
            top+=20;
            ConnectionNote("ConnectionLegend","Installed = local data is present. Configured = settings exist; API access and credit are not verified here.",ref top);
            connectionsTab.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnRight=guideTab,selectOnUp=closeButton,selectOnDown=deviceGroup};
            deviceGroup.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=connectionsTab,selectOnDown=backendGroup};
            backendGroup.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=deviceGroup,
                selectOnDown=allServicesButton!=null && allServicesButton.gameObject.activeSelf?allServicesButton:connectionsTab};
            if(allServicesButton!=null) allServicesButton.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=backendGroup,selectOnDown=connectionsTab};
        }

        // Unused default endpoint URLs do not prove installation. Keep selected
        // providers visible even when broken, and keep the full inventory accessible.
        public static bool ShouldShowBackendService(string key,ProviderStatusItem item,string chatProvider,string mode,string voice,bool showAll)
        {
            if(showAll || string.Equals(key,chatProvider,StringComparison.OrdinalIgnoreCase)) return true;
            if(key=="openai" && YuiConversationModes.IsRealtime(mode)) return true;
            var selectedVoice=YuiTtsRuntimeRouting.BackendProviderForMode(voice);
            if(selectedVoice=="http") selectedVoice="http_tts";
            if(key==selectedVoice || key=="aivis" && YuiConversationModes.IsRealtimeAivis(mode)
                || key=="voicevox" && YuiConversationModes.IsRealtimeVoicevox(mode)) return true;
            return item!=null && (item.Status=="ok" || item.Selectable || item.RequiresApiKey && item.Status=="configured");
        }
        public static string BackendGroupStatus(ProviderStatusResponse status,string mode,string voice)
        {
            if(status?.Backend?.Status!=null && status.Backend.Status!="ok") return status.Backend.Status;
            if(status?.Database?.Status=="error") return "degraded";
            if(status?.Providers!=null) foreach(var pair in status.Providers)
                if(ShouldShowBackendService(pair.Key,pair.Value,status.ChatProvider,mode,voice,false)
                    && (pair.Value?.Status=="error" || pair.Value?.Status=="offline" || pair.Value?.Status=="missing_key" || pair.Value?.Status=="degraded")) return "degraded";
            return "ok";
        }
        private static string ProviderLabel(string key,ProviderStatusItem item)
        {
            switch(key)
            {
                case "openai":return "OpenAI";
                case "xai":return "xAI";
                case "gemini":return "Gemini";
                case "voicevox":return "VOICEVOX";
                case "aivis":return "AivisSpeech";
                case "http_tts":return (item?.Engine??"").StartsWith("irodori",StringComparison.OrdinalIgnoreCase)?"Irodori":"External voice";
                case "lmstudio":return "LM Studio";
                case "litert_lm":return "LiteRT-LM";
                default:return key;
            }
        }
        private Button ConnectionGroup(string id,string caption,string status,bool expanded,UnityEngine.Events.UnityAction action,ref float top)
        {
            var row=modernContent.Find(id);Button button;
            if(row==null) button=HelpButton(modernContent,id,caption,action);else button=row.GetComponent<Button>();
            button.gameObject.SetActive(true);RowRect((RectTransform)button.transform,top,88);top+=100;
            var label=HelpText(button.transform,"Label",caption,YuiUiTypography.Label,true);
            SetAnchors(label.transform,new Vector2(.035f,.07f),new Vector2(.57f,.93f));
            var badge=HelpText(button.transform,"Status",status,YuiUiTypography.Caption,false);
            SetAnchors(badge.transform,new Vector2(.58f,.07f),new Vector2(.9f,.93f));badge.alignment=TextAnchor.MiddleRight;badge.color=YuiUiTheme.Muted;
            var arrow=HelpText(button.transform,"Expand",expanded?"−":"+",YuiUiTypography.Heading,false);
            SetAnchors(arrow.transform,new Vector2(.92f,.07f),new Vector2(.98f,.93f));arrow.alignment=TextAnchor.MiddleCenter;
            return button;
        }
        private void ConnectionNote(string id,string source,ref float top)
        {
            var note=HelpText(modernContent,"Body"+id,source,YuiUiTypography.Note,false);note.color=YuiUiTheme.Muted;
            var height=Mathf.Max(60,note.cachedTextGeneratorForLayout.GetPreferredHeight(note.text,note.GetGenerationSettings(new Vector2(Mathf.Max(400,modernContent.rect.width),0)))/note.pixelsPerUnit+12);
            RowRect(note.rectTransform,top,height);top+=height+16;
        }
    }
}
