using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiHelpOverlay
    {
        private CancellationTokenSource statusPolling;
        private RectTransform modernContent;
        private ScrollRect modernScroll;
        private Button connectionsTab, guideTab, avatarTab;
        private Text refreshLabel;
        private ProviderStatusResponse latestStatus;
        private bool backendOnline;
        private bool statusChecked;
        private bool guideVisible, avatarGuideVisible;
        private bool modernBuilt;
        private YuiChatPanel chatPanel;
        private GameObject previousSelection;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) { if (helpRoot != null && helpRoot.activeSelf) Hide(); else Show(); }
            if (Input.GetKeyDown(KeyCode.Escape) && helpRoot != null && helpRoot.activeSelf) Hide();
        }
        private void StartStatusPolling()
        {
            StopStatusPolling();
            statusPolling = new CancellationTokenSource();
            _ = PollStatusAsync(statusPolling.Token);
        }
        private void StopStatusPolling()
        {
            statusPolling?.Cancel(); statusPolling?.Dispose(); statusPolling = null;
        }
        private async Task PollStatusAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(6));
                        try
                        {
                            var url = PlayerPrefs.GetString(YuiPrefsKeys.BackendUrl, backendUrl);
                            var client = new YuiBackendClient(url);
                            try { latestStatus = await client.GetProviderStatusAsync(timeout.Token); }
                            catch (YuiBackendException ex) when (ex.StatusCode == 404)
                            {
                                var health = await client.GetHealthAsync(timeout.Token);
                                latestStatus = new ProviderStatusResponse {
                                    Backend = new ProviderSystemStatus { Status = "ok" },
                                    Database = new ProviderSystemStatus { Status = health.Database == "ok" ? "ok" : "unknown" },
                                    Providers = new System.Collections.Generic.Dictionary<string,ProviderStatusItem> {
                                        ["openai"] = new ProviderStatusItem { Status = HealthBool(health,"openai_configured") ? "configured" : "unknown" }
                                    }
                                };
                            }
                            backendOnline = true;
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                        catch (Exception) { latestStatus = null; backendOnline = false; }
                    }
                    if (token.IsCancellationRequested || this == null || helpRoot == null || !helpRoot.activeSelf) return;
                    statusChecked = true;
                    // Keep the guide's scroll position stable while only statuses change.
                    if (!guideVisible) RenderModernHelp();
                    refreshLabel.text = string.Format(YuiUiLocalization.Text("Updated {0} · refreshes automatically"), DateTime.Now.ToString("HH:mm:ss"));
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
            }
            catch (OperationCanceledException) { }
        }
        private void RenderModernHelp()
        {
            if (helpRoot == null) return;
            chatPanel = chatPanel != null ? chatPanel : YuiSceneObjectFinder.FindFirst<YuiChatPanel>();
            var panel = helpRoot.transform.Find("Panel");
            if (panel == null) return;
            SetAnchors(helpRoot.transform, Vector2.zero, Vector2.one);
            if (helpRoot.TryGetComponent<Image>(out var scrim)) scrim.color = new Color(0,0,0,.6f);
            SetAnchors(panel, new Vector2(.045f,.065f), new Vector2(.955f,.95f));
            YuiUiTheme.SurfaceOn(panel.GetComponent<Image>(), YuiUiTheme.Surface);
            if (!modernBuilt)
            {
                modernBuilt = true;
                foreach (Transform child in panel) child.gameObject.SetActive(child == closeButton?.transform);
                var title = HelpText(panel, "HelpTitle", "Help", YuiUiTypography.Title, true);
                SetAnchors(title.transform,new Vector2(.055f,.915f),new Vector2(.7f,.985f));
                if (closeButton != null)
                {
                    closeButton.gameObject.SetActive(true);
                    SetAnchors(closeButton.transform,new Vector2(.80f,.923f),new Vector2(.95f,.98f));
                    var label = closeButton.GetComponentInChildren<Text>(); if (label != null) YuiUiLocalization.Set(label,"Close");
                    YuiUiTheme.ButtonStyle(closeButton);
                }
                connectionsTab = HelpButton(panel,"ConnectionsTab","Connections",()=>SelectHelpPage(false));
                guideTab = HelpButton(panel,"GuideTab","Quick guide",()=>SelectHelpPage(true));
                avatarTab = HelpButton(panel,"AvatarTab","Avatar guide",()=> { avatarGuideVisible=true;guideVisible=true;RenderModernHelp();modernScroll.verticalNormalizedPosition=1; });
                SetAnchors(connectionsTab.transform,new Vector2(.05f,.837f),new Vector2(.34f,.9f));
                SetAnchors(guideTab.transform,new Vector2(.355f,.837f),new Vector2(.645f,.9f));
                SetAnchors(avatarTab.transform,new Vector2(.66f,.837f),new Vector2(.95f,.9f));
                var scrollObject = new GameObject("HelpScroll",typeof(RectTransform),typeof(Image),typeof(ScrollRect));
                scrollObject.transform.SetParent(panel,false); modernScroll=scrollObject.GetComponent<ScrollRect>();
                scrollObject.GetComponent<Image>().color=Color.clear;
                SetAnchors(scrollObject.transform,new Vector2(.05f,.095f),new Vector2(.95f,.81f));
                var viewport = new GameObject("Viewport",typeof(RectTransform),typeof(RectMask2D));
                viewport.transform.SetParent(scrollObject.transform,false); SetAnchors(viewport.transform,Vector2.zero,Vector2.one);
                var content = new GameObject("Content",typeof(RectTransform));content.transform.SetParent(viewport.transform,false);
                modernContent=content.GetComponent<RectTransform>();modernContent.anchorMin=new Vector2(0,1);modernContent.anchorMax=Vector2.one;modernContent.pivot=new Vector2(.5f,1);
                modernContent.anchoredPosition=Vector2.zero;modernContent.sizeDelta=Vector2.zero;
                modernScroll.viewport=viewport.GetComponent<RectTransform>();modernScroll.content=modernContent;modernScroll.horizontal=false;modernScroll.movementType=ScrollRect.MovementType.Clamped;
                modernScroll.scrollSensitivity=45;
                YuiControlAffordance.Scrollbar(modernScroll);
                refreshLabel=HelpText(panel,"RefreshLabel","Checking connections…",YuiUiTypography.Caption,false);
                refreshLabel.color=YuiUiTheme.Muted;SetAnchors(refreshLabel.transform,new Vector2(.055f,.025f),new Vector2(.95f,.075f));
                connectionsTab.navigation=new Navigation { mode=Navigation.Mode.Explicit,selectOnRight=guideTab,selectOnUp=closeButton,selectOnDown=guideTab };
                guideTab.navigation=new Navigation { mode=Navigation.Mode.Explicit,selectOnLeft=connectionsTab,selectOnRight=avatarTab,selectOnUp=closeButton,selectOnDown=avatarTab };
            }
            avatarTab.navigation=new Navigation { mode=Navigation.Mode.Explicit,selectOnLeft=guideTab,selectOnUp=closeButton,selectOnDown=connectionsTab };
            Canvas.ForceUpdateCanvases();
            var selection = EventSystem.current?.currentSelectedGameObject;
            foreach (Transform child in modernContent) child.gameObject.SetActive(false);
            connectionsTab.GetComponent<Image>().color=guideVisible?YuiUiTheme.Field:YuiUiTheme.Selected;
            guideTab.GetComponent<Image>().color=guideVisible && !avatarGuideVisible?YuiUiTheme.Selected:YuiUiTheme.Field;
            avatarTab.GetComponent<Image>().color=avatarGuideVisible?YuiUiTheme.Selected:YuiUiTheme.Field;
            var top=8f;
            if (avatarGuideVisible)
            {
                Guide("Bring your avatar",YuiChatPanel.AvatarImportInstructions(),ref top);
                var action = modernContent.Find("OpenCharacterSettings")?.GetComponent<Button>();
                if (action == null) action = HelpButton(modernContent,"OpenCharacterSettings","My characters",()=> { Hide();chatPanel?.OpenCharacterLibrary(); });
                action.gameObject.SetActive(true);RowRect((RectTransform)action.transform,top,88);top+=100;
            }
            else if (guideVisible)
            {
                Guide("Talk & Work","Talk keeps replies brief. Work shows a fuller answer and speaks only the key points. Copy or save the full result from its message.",ref top);
                Guide("Ask, show, listen","Type a message or use Mic. Use the paperclip to choose a photo. On mobile, take a photo with your camera app first. Log is in the console header; avatars are in Settings → Character. Send becomes Stop while a request or voice is active.",ref top);
                Guide("History","Conversation text stays on this device until you delete it. Open History to browse older messages and saved answers. Secret mode does not save conversations.",ref top);
                Guide("Search & sources","With OpenAI API or an OpenAI backend, ask Yui to search the web or check today's weather in a named city. Sources opens the returned links. On-device AI works offline and does not search the web.",ref top);
                Guide("Voice credits", "VOICEVOX:冥鳴ひまり / 四国めたん / ずんだもん / 九州そら / 小夜/SAYO\nVoice use and sharing must follow each voice library’s terms. See https://github.com/VOICEVOX/voicevox_vvm for the terms.", ref top);
                Guide("Connection & voice","AI and voice are separate choices in Settings. This app's API key is for direct OpenAI access; a backend uses its own key. Voice engines appear when available in your environment.",ref top);
                Guide("Advanced features","Realtime voice and translation, plus the memory database, require a configured backend. Direct OpenAI supports text, microphone transcription, images and web search without a PC.",ref top);
                Guide("Private conversation","The crossed-eye button enables Secret mode: Yui does not save conversation history. Requests still use the AI connection you selected.",ref top);
                var privacy = modernContent.Find("PrivacyInfo")?.GetComponent<Button>();
                if (privacy == null) privacy = HelpButton(modernContent, "PrivacyInfo", "Privacy", () => { Hide(); chatPanel?.ShowPrivacyInformation(); });
                privacy.gameObject.SetActive(true); RowRect((RectTransform)privacy.transform, top, 88); top += 100;
            }
            else
            {
                RenderConnectionGroups(ref top);
            }
            modernContent.sizeDelta=new Vector2(0,top+12);
            // Polling reuses controls; preserve keyboard focus when refreshing them.
            if (selection != null && selection.activeInHierarchy && selection.transform.IsChildOf(modernContent))
                EventSystem.current?.SetSelectedGameObject(selection);
        }
        private void SelectHelpPage(bool guide)
        { avatarGuideVisible=false;guideVisible=guide;RenderModernHelp();modernScroll.verticalNormalizedPosition=1; }
        private void Guide(string title,string body,ref float top)
        {
            var heading=HelpText(modernContent,"Heading"+title,title,YuiUiTypography.Heading,true);
            RowRect(heading.rectTransform,top,58);top+=64;
            var text=HelpText(modernContent,"Body"+title,body,YuiUiTypography.Note,false);text.color=YuiUiTheme.Muted;
            var width=Mathf.Max(400,modernContent.rect.width);
            var height=Mathf.Max(75,text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,text.GetGenerationSettings(new Vector2(width,0)))/text.pixelsPerUnit+18);
            RowRect(text.rectTransform,top,height);top+=height+30;
        }
        private void StatusRow(string name,string state,ref float top,bool nested=false)
        {
            var row=modernContent.Find("Status"+name);
            if(row==null){var go=new GameObject("Status"+name,typeof(RectTransform),typeof(Image));go.transform.SetParent(modernContent,false);row=go.transform;}
            row.gameObject.SetActive(true);RowRect((RectTransform)row,top,72);top+=82;
            if(nested) {var rect=(RectTransform)row;rect.anchoredPosition+=new Vector2(12,0);rect.sizeDelta+=new Vector2(-24,0);}
            YuiUiTheme.SurfaceOn(row.GetComponent<Image>(),YuiUiTheme.Field);
            var label=HelpText(row,"Name",name,YuiUiTypography.Label,false);SetAnchors(label.transform,new Vector2(.025f,.07f),new Vector2(.67f,.93f));
            var status=HelpText(row,"Status",HumanStatus(state),YuiUiTypography.Caption,true);SetAnchors(status.transform,new Vector2(.68f,.07f),new Vector2(.98f,.93f));status.alignment=TextAnchor.MiddleRight;
            status.color=state=="ok"||state=="installed"?new Color32(164,218,184,255):state=="configured"?YuiUiTheme.Accent:state=="offline"||state=="error"||state=="missing_key"?new Color32(240,170,166,255):YuiUiTheme.Muted;
        }
        public static string HumanStatus(string state)
        {
            switch(state){case "ok":return "Online";case "installed":return "Installed";case "configured":return "Configured";case "missing_key":return "No key";case "offline":return "Offline";case "error":return "Error";case "not_installed":return "Not installed";case "not_configured":return "Not configured";case "disabled":return "Disabled";case "degraded":return "Limited";case "unavailable":return "Unavailable";default:return "Not checked";}
        }
        private static void RowRect(RectTransform rect,float top,float height)
        {rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-top);rect.sizeDelta=new Vector2(0,height);}
        private static Text HelpText(Transform parent,string name,string value,int size,bool bold)
        {
            var child=parent.Find(name); Text text;
            if(child==null){var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);text=go.GetComponent<Text>();text.font=YuiChatLogStyle.ResolveFont(null);}
            else text=child.GetComponent<Text>();
            text.gameObject.SetActive(true);YuiUiLocalization.Set(text,value,name.StartsWith("Body",StringComparison.Ordinal));text.fontSize=size;text.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;text.color=YuiUiTheme.Text;text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            return text;
        }
        private static Button HelpButton(Transform parent,string name,string caption,UnityEngine.Events.UnityAction action)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
            var button=go.GetComponent<Button>();button.onClick.AddListener(action);
            var label=HelpText(go.transform,"Label",caption,YuiUiTypography.Button,false);SetAnchors(label.transform,Vector2.zero,Vector2.one);label.alignment=TextAnchor.MiddleCenter;YuiUiTheme.ButtonStyle(button);return button;
        }
    }
}
