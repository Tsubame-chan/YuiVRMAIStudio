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
                var tutorial = modernContent.Find("Tutorial")?.GetComponent<Button>();
                if (tutorial == null) tutorial = HelpButton(modernContent, "Tutorial", YuiSimpleDialog.L("チュートリアルをもう一度見る", "Show tutorial again"), () => { Hide(); YuiTutorial.Show(); });
                YuiUiLocalization.Set(tutorial.GetComponentInChildren<Text>(),YuiSimpleDialog.L("チュートリアルをもう一度見る", "Show tutorial again"));
                tutorial.gameObject.SetActive(true); RowRect((RectTransform)tutorial.transform, top, 88); top += 100;
                Guide(YuiSimpleDialog.L("端末内AIを選ぶ", "Choose on-device AI"), YuiSimpleDialog.L("標準の2Bは同梱済みです。設定の「端末内AI」から、より高品質な4Bを追加ダウンロードできます。4Bは応答時間・必要容量・端末の負荷が増えます。取得中もアプリを使え、完了後に自分で切り替えられます。", "Standard 2B is included. Download the more capable 4B from On-device AI in Settings. It needs more time, storage and memory. Keep using the app during download, then select it when ready."), ref top, "LocalModelsGuide");
                Guide(YuiSimpleDialog.L("推奨: OpenAI APIで会話", "Recommended: OpenAI API"), YuiSimpleDialog.L("高品質な会話には、設定 → AIのOpenAI APIをおすすめします。OpenAI APIキーとインターネット接続が必要で、通信料とOpenAIのAPI利用料金がかかります。ChatGPTの月額プランとは別料金です。", "For high-quality conversations, choose OpenAI API in Settings → AI. It requires an OpenAI API key and internet access. Data charges and OpenAI API fees apply, separately from a ChatGPT subscription."), ref top, "RecommendedApiGuide");
                Guide(YuiSimpleDialog.L("性格と回答の方針", "Personality and response style"), YuiSimpleDialog.L("設定 → キャラクターの「キャラクターの性格・口調」で役柄や話し方を指定します。設定 → AIの「回答の方針」で結論を先にするなどの回答形式を指定します。両方ともAPIと端末内AIに引き継ぎます。変更した設定は保存してください。", "Set role and tone under Settings → Character → Character personality / Tone. Set answer format under Settings → AI → Response style. Both apply to API and on-device AI. Save your settings after editing."), ref top, "PersonalityGuide");
                Guide(YuiSimpleDialog.L("端末内AIの詳細設定", "Advanced on-device AI settings"), YuiSimpleDialog.L("端末内AIのモデル選択から「選択中のモデル · 詳細設定」を開けます。モデルごと・Talk/Workごとに、コンテキスト、生成・推論上限、温度、Top K/Top P、待ち時間、LLMへの追加指示を保存できます。値を増やすと応答時間やメモリ使用量が増えます。困ったら「このモードを既定値に戻す」で戻せます。\n\nモデルへの追加指示は、性格・口調と共通の回答方針に加えて読み込まれます。APIには適用しません。", "Open Selected model → Advanced from the on-device model list. Each model has separate Talk/Work settings for context, output/thinking limits, temperature, Top K/Top P, timeout and LLM instructions. Higher limits can use more memory and take longer. Use Reset this mode to restore defaults.\n\nModel instructions are read alongside personality and shared response style. These advanced settings do not apply to API connections."), ref top, "LocalAdvancedGuide");
                Guide("Talk & Work","Talk keeps replies brief. Work shows a fuller answer and speaks only the key points. Copy or save the full result from its message.",ref top);
                Guide("Ask, show, listen","Type a message or use Mic. Use the paperclip to choose a photo. On mobile, take a photo with your camera app first. Log is in the console header; avatars are in Settings → Character. Send becomes Stop while a request or voice is active.",ref top);
                Guide("History","Conversation text stays on this device until you delete it. Open History to browse older messages and saved answers. Secret mode does not save conversations.",ref top);
                Guide("Character memory","Preferences and promises are stored separately for each character on this device and used by both local AI and API. Settings → Character → Manage character memories lets you add, edit, prioritize or delete them. Only relevant excerpts are passed to the AI; recall is not guaranteed. Conversation history and memory are deleted separately. Backend memories remain on your server.",ref top,"CharacterMemoryGuide");
                Guide("Search & sources","With OpenAI API or an OpenAI backend, ask Yui to search the web or check today's weather in a named city. Sources opens the returned links. On-device AI works offline and does not search the web.",ref top);
                Guide("Voice credits", "VOICEVOX:冥鳴ひまり / 四国めたん / ずんだもん / 九州そら / 小夜/SAYO\nVoice use and sharing must follow each voice library’s terms. See https://github.com/VOICEVOX/voicevox_vvm for the terms.", ref top);
                Guide("Connection & voice","AI and voice are separate choices in Settings. This app's API key is for direct OpenAI access; a backend uses its own key. Voice engines appear when available in your environment.",ref top);
                Guide("Advanced features","Realtime voice and translation require a configured backend. Direct OpenAI supports text, microphone transcription, images and web search without a PC.",ref top);
                Guide("Private conversation","Secret mode can use this character’s existing memories, but does not save its conversations or create new memories. Other characters cannot read them. Requests still use your selected AI connection.",ref top);
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
        private void Guide(string title,string body,ref float top,string key=null)
        {
            var heading=HelpText(modernContent,"Heading"+(key ?? title),title,YuiUiTypography.Heading,true);
            RowRect(heading.rectTransform,top,58);top+=64;
            var text=HelpText(modernContent,"Body"+(key ?? title),body,YuiUiTypography.Note,false);text.color=YuiUiTheme.Muted;
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
