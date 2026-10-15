using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiHelpConnectionTests
    {
        [Test]
        public void GuideLanguageSwitchReusesRowsAndUpdatesTheirText()
        {
            var locale=YuiUiLocalization.Language;
            var host=new GameObject("Guide host");
            var root=new GameObject("Help",typeof(RectTransform),typeof(Image));
            var panel=new GameObject("Panel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(root.transform,false);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            try
            {
                var help=host.AddComponent<YuiHelpOverlay>();help.Configure(root,null,null);root.SetActive(true);
                typeof(YuiHelpOverlay).GetField("guideVisible",flags).SetValue(help,true);
                void Render()=>typeof(YuiHelpOverlay).GetMethod("RenderModernHelp",flags).Invoke(help,null);
                YuiUiLocalization.SetLanguage("en");Render();
                var content=panel.transform.Find("HelpScroll/Viewport/Content");
                var originalCount=content.childCount;
                Assert.That(content.Find("BodyLocalImportGuide").GetComponent<Text>().text,Does.Contain(".gguf").And.Contain(".litertlm").And.Contain(".safetensors"));
                for(var i=0;i<4;i++)
                {
                    YuiUiLocalization.SetLanguage(i%2==0?"ja":"en");Render();
                    Assert.That(content.childCount,Is.EqualTo(originalCount),"Switching language must not accumulate duplicate guide rows.");
                    Assert.That(content.Find("HeadingPersonalityGuide").GetComponent<Text>().text,
                        Is.EqualTo(i%2==0?"性格と回答の方針":"Personality and response style"));
                }
            }
            finally {Object.DestroyImmediate(root);Object.DestroyImmediate(host);YuiUiLocalization.SetLanguage(locale);}
        }

        [Test]
        public void BackendDownloadTargetsThisBuildsReleaseRatherThanGithubLatest()
        {
            var url=(string)typeof(YuiSettingsOverlay).GetMethod("BackendReleaseUrl",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            Assert.That(url,Is.EqualTo("https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/"+YuiPhysicalAI.Editor.YuiReleaseVersionMetadata.Read("desktop_tag")));
        }

        [TestCase("ja")]
        [TestCase("en")]
        public void BackendAcquisitionHasFixedCloseAndPlatformGuides(string language)
        {
            var locale=YuiUiLocalization.Language;
            YuiSimpleDialog dialog=null;
            try
            {
                YuiUiLocalization.SetLanguage(language);
                typeof(YuiSettingsOverlay).GetMethod("ShowBackendAcquisitionHelp",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{true});
                dialog=Object.FindFirstObjectByType<YuiSimpleDialog>();
                Assert.IsNotNull(dialog);
                Assert.IsNotNull(dialog.Heading.transform.parent.Find("Close").GetComponent<Button>(),"Long instructions need a close control outside the scrolling body.");
                Assert.That(dialog.Body.text,Does.Contain("Start_Yui_Backend.command").And.Contain("Start_Yui_Backend.bat").And.Contain("127.0.0.1"));
                var labels=System.Linq.Enumerable.Select(dialog.GetComponentsInChildren<Button>(),b=>b.GetComponentInChildren<Text>()?.text??"");
                Assert.IsTrue(System.Linq.Enumerable.Any(labels,t=>t.Contains("Mac")&&!t.Contains("Windows")));
                Assert.IsTrue(System.Linq.Enumerable.Any(labels,t=>t.Contains("Windows")&&!t.Contains("Mac")));
            }
            finally {if(dialog!=null)Object.DestroyImmediate(dialog.gameObject);YuiUiLocalization.SetLanguage(locale);}
        }

        [Test]
        public void UnusedDefaultEndpointsAndMissingKeysDoNotFillTheSummary()
        {
            Assert.IsFalse(Visible("lmstudio",new ProviderStatusItem {Status="configured",BaseUrl="http://localhost:1234"}));
            Assert.IsFalse(Visible("gemini",new ProviderStatusItem {Status="missing_key",RequiresApiKey=true}));
            Assert.IsTrue(Visible("voicevox",new ProviderStatusItem {Status="ok"}));
            Assert.IsTrue(Visible("xai",new ProviderStatusItem {Status="configured",RequiresApiKey=true}));
            Assert.IsTrue(Visible("aivis",new ProviderStatusItem {Status="offline",Selectable=true}));
        }
        [Test]
        public void FailedSelectedServicesRemainVisibleAndFullInventoryIsAvailable()
        {
            var failed=new ProviderStatusItem {Status="error"};
            Assert.IsTrue(YuiHelpOverlay.ShouldShowBackendService("http_tts",failed,"openai",YuiConversationModes.LocalAi,"server-http",false));
            Assert.IsTrue(YuiHelpOverlay.ShouldShowBackendService("openai",new ProviderStatusItem {Status="missing_key"},"openai",YuiConversationModes.BackendAi,"silent",false));
            Assert.IsTrue(YuiHelpOverlay.ShouldShowBackendService("aivis",failed,"openai",YuiConversationModes.RealtimeAivis,"silent",false));
            Assert.IsTrue(YuiHelpOverlay.ShouldShowBackendService("openai",failed,"lmstudio",YuiConversationModes.RealtimeVoice,"silent",false));
            Assert.IsTrue(YuiHelpOverlay.ShouldShowBackendService("gemini",null,"openai",YuiConversationModes.LocalAi,"silent",true));
        }
        private static bool Visible(string key,ProviderStatusItem item) =>
            YuiHelpOverlay.ShouldShowBackendService(key,item,"openai",YuiConversationModes.DirectOpenAi,"silent",false);

        [Test]
        public void CollapsedBackendWarnsAboutSelectedVoiceButNotUnusedMissingKeys()
        {
            var status=new ProviderStatusResponse {ChatProvider="openai",Backend=new ProviderSystemStatus {Status="ok"},Providers=new Dictionary<string,ProviderStatusItem> {
                ["openai"]=new ProviderStatusItem {Status="configured",RequiresApiKey=true},
                ["gemini"]=new ProviderStatusItem {Status="missing_key",RequiresApiKey=true},
                ["http_tts"]=new ProviderStatusItem {Status="offline"}
            }};
            Assert.That(YuiHelpOverlay.BackendGroupStatus(status,YuiConversationModes.LocalAi,"silent"),Is.EqualTo("ok"));
            Assert.That(YuiHelpOverlay.BackendGroupStatus(status,YuiConversationModes.LocalAi,"server-http"),Is.EqualTo("degraded"));
        }

        [Test]
        public void GroupsRetainExpansionAcrossRefreshAndDoNotKeepStaleOnlineRows()
        {
            var host=new GameObject("Help host");
            var root=new GameObject("Help",typeof(RectTransform),typeof(Image));
            var panel=new GameObject("Panel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(root.transform,false);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            try
            {
                var help=host.AddComponent<YuiHelpOverlay>();help.Configure(root,null,null);root.SetActive(true);
                void Set(string key,object value)=>typeof(YuiHelpOverlay).GetField(key,flags).SetValue(help,value);
                void Render()=>typeof(YuiHelpOverlay).GetMethod("RenderModernHelp",flags).Invoke(help,null);
                Set("backendOnline",true);Set("statusChecked",true);
                Set("latestStatus",new ProviderStatusResponse {ChatProvider="openai",Backend=new ProviderSystemStatus {Status="ok"},Database=new ProviderSystemStatus {Status="ok"},Providers=new Dictionary<string,ProviderStatusItem> {
                    ["openai"]=new ProviderStatusItem {Status="configured",RequiresApiKey=true},
                    ["voicevox"]=new ProviderStatusItem {Status="ok"},
                    ["gemini"]=new ProviderStatusItem {Status="missing_key",RequiresApiKey=true}
                }});
                Render();var content=(RectTransform)typeof(YuiHelpOverlay).GetField("modernContent",flags).GetValue(help);
                Assert.IsNull(content.Find("StatusDatabase"));
                var acquisition=content.Find("GetBackend").GetComponent<Button>();
                Assert.IsTrue(acquisition.gameObject.activeSelf,"Backend acquisition must be discoverable without expanding connection diagnostics.");
                Assert.That(content.Find("BackendGroup").GetComponent<Button>().navigation.selectOnDown,Is.EqualTo(acquisition));
                content.Find("BackendGroup").GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(content.Find("StatusDatabase").gameObject.activeSelf);
                Assert.IsNull(content.Find("StatusGemini"));
                Render();Assert.IsTrue(content.Find("StatusDatabase").gameObject.activeSelf);
                content.Find("AllServices").GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(content.Find("StatusGemini").gameObject.activeSelf);
                Set("backendOnline",false);Set("latestStatus",null);Render();
                Assert.IsFalse(content.Find("StatusDatabase").gameObject.activeSelf);
                Assert.IsFalse(content.Find("StatusGemini").gameObject.activeSelf);
                Assert.That(content.Find("BackendGroup/Status").GetComponent<Text>().text,Is.EqualTo(YuiUiLocalization.Text("Offline")));
                content.Find("BackendGroup").GetComponent<Button>().onClick.Invoke();
                Assert.IsFalse(content.Find("BodyBackendOfflineNote").gameObject.activeSelf);
            }
            finally {Object.DestroyImmediate(root);Object.DestroyImmediate(host);}
        }
    }
}
