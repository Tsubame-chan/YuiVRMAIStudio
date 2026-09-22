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
