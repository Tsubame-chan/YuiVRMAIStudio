using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YuiPhysicalAI.UI;
using System.Linq;
namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiVoiceSettingsIntegrationTests
    {
        [Test] public void IrodoriHasItsOwnEngineAndVoiceCatalog()
        {
            Assert.That(YuiBackendMonitorPolicy.ShouldMonitorBackend(YuiPhysicalAI.Core.YuiConversationModes.LocalAi,"irodori-native",false),Is.False);
            var capabilities=YuiCapabilityMatrix.FromProviderStatus(null,false,false,true,false).WithIrodori(true,true);
            Assert.That(capabilities.Tts("irodori-native").Ready,Is.True);
            Assert.That(capabilities.Tts("irodori-native").RequiresBackend,Is.False);
            var modes=YuiVoiceEnvironmentOptions.Build(null,false,false,true,false,true,"server",false,false,true,true);
            Assert.That(modes.Any(x=>x.Key=="irodori-native"));
            var voices=YuiTtsVoiceOptionCatalog.OptionsForMode("irodori-native",null);
            Assert.That(voices.Select(x=>x.Id),Is.EqualTo(new[]{10005,10002,10003}));
            var tuning=YuiTtsTuningPrefs.Sanitize("irodori-native",new YuiSavedTtsTuning(10002,1,0,1,1,.1f,.1f));
            Assert.That(tuning.SpeakerId,Is.EqualTo(10002));
            Assert.That(YuiTtsTuningPrefs.NormalizeMode("irodori-native"),Is.Not.EqualTo(YuiTtsTuningPrefs.NormalizeMode("server")));
        }
        [TestCase(10005,10005,"bright_natural")]
        [TestCase(10002,10002,"gentle_friend")]
        [TestCase(10003,10003,"calm_natural")]
        [TestCase(10001,10005,"bright_natural")]
        [TestCase(14,10005,"bright_natural")]
        public void IrodoriAcceptedVoicesAndRetiredVoiceMigration(int saved, int expected, string voice)
        {
            var tuning=YuiTtsTuningPrefs.Sanitize("irodori-native",new YuiSavedTtsTuning(saved,1,0,1,1,.1f,.1f));
            Assert.That(tuning.SpeakerId,Is.EqualTo(expected));
            Assert.That(YuiPhysicalAI.LocalAI.YuiIrodoriSpeech.VoiceForSpeaker(tuning.SpeakerId),Is.EqualTo(voice));
            Assert.That(Resources.Load<TextAsset>("YuiVoicePacks/IrodoriReferences/"+voice).bytes.Length,Is.GreaterThan(44));
            Assert.That(YuiTtsTuningPrefs.DefaultSpeakerForMode("irodori-native"),Is.EqualTo(10005));
        }
        [Test] public void BodyScrollbarUsesOverflowLayoutForShortAndLongText()
        {
            var dialog=YuiSimpleDialog.Create("Test","Short text.");
            try {
                dialog.Compact(640);
                var scroll=dialog.Body.GetComponentInParent<ScrollRect>();
                Assert.That(scroll.verticalScrollbarVisibility,Is.EqualTo(ScrollRect.ScrollbarVisibility.AutoHide));
                var late=typeof(ScrollRect).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                Canvas.ForceUpdateCanvases();late.Invoke(scroll,null);
                var measure=typeof(YuiSimpleDialog).GetMethod("RefreshContentWidth",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                measure.Invoke(dialog,null);
                Assert.That(scroll.viewport.offsetMax.x,Is.EqualTo(0),"Short dialogs must not reserve a hidden scrollbar gutter.");
                // Unity deliberately shows AutoHide scrollbars in EditMode; visibility is verified in Player.
                Assert.That(scroll.content.rect.height,Is.LessThan(scroll.viewport.rect.height));
                dialog.Body.text=string.Join("\n",Enumerable.Repeat("Long explanation",100));
                Canvas.ForceUpdateCanvases();late.Invoke(scroll,null);
                measure.Invoke(dialog,null);
                Assert.That(scroll.viewport.offsetMax.x,Is.EqualTo(-44),"Long dialogs must retain the scrollbar touch area.");
                Assert.That(scroll.verticalScrollbar,Is.Not.Null);
                Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
                dialog.Body.text="Short again.";
                Canvas.ForceUpdateCanvases();measure.Invoke(dialog,null);
                Assert.That(scroll.viewport.offsetMax.x,Is.EqualTo(0));
            } finally {Object.DestroyImmediate(dialog.gameObject);}
        }
        [TestCase(true)]
        [TestCase(false)]
        public void CompactHeaderReservesCloseSpaceRegardlessOfCallOrder(bool closeFirst)
        {
            var dialog=YuiSimpleDialog.Create("処理を停止してから変更してください","Body");
            try {
                if(closeFirst)dialog.AddCloseButton(dialog.Close);
                dialog.Compact(440);
                if(!closeFirst)dialog.AddCloseButton(dialog.Close);
                Canvas.ForceUpdateCanvases();
                var close=(RectTransform)dialog.Heading.transform.parent.Find("Close");
                var heading=dialog.Heading.rectTransform;
                var h=new Vector3[4];var c=new Vector3[4];heading.GetWorldCorners(h);close.GetWorldCorners(c);
                Assert.That(h[2].x,Is.LessThan(c[0].x),"Title must reserve the close control's width.");
                Assert.That(heading.rect.height,Is.GreaterThanOrEqualTo(dialog.Heading.preferredHeight-.5f));
                Assert.That(close.rect.width,Is.EqualTo(44).Within(.1f));
            }finally{Object.DestroyImmediate(dialog.gameObject);}
        }
        [Test] public void ContentFitNoticeShrinksAndLongBodyKeepsOverflow()
        {
            var dialog=YuiSimpleDialog.Create("処理中","会話が終わってから保存できます。");
            try {
                dialog.AddButton("閉じる",dialog.Close);dialog.AddCloseButton(dialog.Close);dialog.FitToContent(440);
                var update=typeof(YuiSimpleDialog).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var measure=typeof(YuiSimpleDialog).GetMethod("RefreshContentWidth",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                Canvas.ForceUpdateCanvases();measure.Invoke(dialog,null);Canvas.ForceUpdateCanvases();measure.Invoke(dialog,null);
                var card=(RectTransform)dialog.Heading.transform.parent;var shortHeight=card.rect.height;
                var scroll=dialog.Body.GetComponentInParent<ScrollRect>();
                Assert.That(shortHeight,Is.LessThan(300));
                Assert.That(scroll.content.rect.height,Is.LessThanOrEqualTo(scroll.viewport.rect.height));
                dialog.Body.text=string.Join("\n",Enumerable.Repeat("Long explanation",100));
                Canvas.ForceUpdateCanvases();measure.Invoke(dialog,null);update.Invoke(dialog,null);Canvas.ForceUpdateCanvases();measure.Invoke(dialog,null);
                Assert.That(card.rect.height,Is.GreaterThan(shortHeight));
                Assert.That(card.rect.height,Is.LessThanOrEqualTo(440));
                Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
                Assert.That(scroll.viewport.offsetMax.x,Is.EqualTo(-44));
            }finally{Object.DestroyImmediate(dialog.gameObject);}
        }
        [Test] public void TwelveModelRowsScrollWhileTabsAndFooterStayOutsideViewport()
        {
            var dialog=YuiSimpleDialog.Create("On-device AI","Choose a model.");
            try {
                dialog.SettingsTabs(0,new[]{"Models","Add","Settings"},_=>{});
                for(int i=0;i<12;i++)dialog.AddModelChoice("A very long imported model file name with quantization Q4_K_M "+i,"GGUF · Use this model",()=>{},i==0);
                var save=dialog.AddFooterButton("Save",()=>{},true);dialog.AddCloseButton(dialog.Close);dialog.Compact(640);
                var update=typeof(YuiSimpleDialog).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var measure=typeof(YuiSimpleDialog).GetMethod("RefreshContentWidth",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                Canvas.ForceUpdateCanvases();update.Invoke(dialog,null);Canvas.ForceUpdateCanvases();measure.Invoke(dialog,null);
                var scroll=dialog.Body.GetComponentInParent<ScrollRect>();
                Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
                Assert.That(save.GetComponentInParent<ScrollRect>(),Is.Null);
                Assert.That(dialog.Heading.transform.parent.Find("DialogTabs").GetComponentInParent<ScrollRect>(),Is.Null);
                foreach(var text in scroll.content.GetComponentsInChildren<Text>()) {
                    if(text.transform.parent.name=="ModelChoice" && (text.name=="Label" || text.name=="Status")) {
                        Assert.That(text.rectTransform.rect.height,Is.GreaterThanOrEqualTo(text.preferredHeight-.5f));
                        Assert.That(text.verticalOverflow,Is.EqualTo(VerticalWrapMode.Overflow));
                        Assert.That(text.cachedTextGenerator.Populate(text.text,text.GetGenerationSettings(text.rectTransform.rect.size)),Is.True);
                        Assert.That(text.cachedTextGenerator.vertexCount,Is.GreaterThan(4),text.name+" must draw visible glyphs after layout measurement");
                    }
                }
                var footer=(RectTransform)save.transform.parent;var top=new Vector3[4];var bottom=new Vector3[4];scroll.viewport.GetWorldCorners(top);footer.GetWorldCorners(bottom);
                Assert.That(bottom[1].y,Is.LessThanOrEqualTo(top[0].y));
            }finally{Object.DestroyImmediate(dialog.gameObject);}
        }
        [TestCase("download",.239f,"23% / 100%")]
        [TestCase("download",2f,"100% / 100%")]
        public void DownloadStatusUsesPercentage(string stage,float percent,string expected)
        {
            var p=new YuiPhysicalAI.LocalAI.YuiLocalAiAssetDownloadProgress("Irodori",239,1000,percent,stage);
            Assert.That(YuiLocalAiDownloadOverlay.DownloadProgressLabel(p),Is.EqualTo(expected));
        }
        [Test] public void VerificationIsNotPresentedAsDownloadPercentage()
        {
            var p=new YuiPhysicalAI.LocalAI.YuiLocalAiAssetDownloadProgress("Irodori",1000,1000,1,"verify");
            Assert.That(YuiLocalAiDownloadOverlay.DownloadProgressLabel(p),Does.Not.Contain("%"));
        }
        [Test] public void AdvancedDialogProvidesScrollbarAndVerticalDragDoesNotChangeValue()
        {
            var events=new GameObject("TestEvents",typeof(EventSystem));
            var dialog=YuiSimpleDialog.Create("Test","Body");
            try {
                var slider=dialog.AddSetting("Output limit (thinking + reply)",.5f,0,1,.05f,_=>{});
                for(var i=0;i<8;i++)dialog.AddButton("Action",()=>{});
                dialog.Compact(640);
                Canvas.ForceUpdateCanvases();
                typeof(YuiSimpleDialog).GetMethod("RefreshContentWidth",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(dialog,null);
                Canvas.ForceUpdateCanvases();
                var caption=slider.transform.parent.Find("Label").GetComponent<Text>();
                Assert.That(caption.verticalOverflow,Is.EqualTo(VerticalWrapMode.Overflow));
                Assert.That(caption.rectTransform.rect.height,Is.GreaterThanOrEqualTo(caption.preferredHeight));
                Assert.That(caption.cachedTextGenerator.Populate(caption.text,caption.GetGenerationSettings(caption.rectTransform.rect.size)),Is.True);
                Assert.That(caption.cachedTextGenerator.vertexCount,Is.GreaterThan(4),"Slider caption must draw after measurement");
                Assert.That(((RectTransform)slider.transform.parent).rect.height-caption.rectTransform.rect.height,Is.GreaterThanOrEqualTo(43.5f));
                var scroll=slider.GetComponentInParent<ScrollRect>();
                Assert.That(scroll,Is.Not.Null);
                Assert.That(dialog.Body.GetComponentInParent<ScrollRect>(),Is.SameAs(scroll));
                Assert.That(dialog.GetComponentsInChildren<ScrollRect>().Length,Is.EqualTo(1));
                Assert.That(dialog.GetComponentsInChildren<Scrollbar>().Length,Is.EqualTo(1));
                Assert.That(scroll.verticalScrollbar,Is.Not.Null);
                Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
                var gesture=new PointerEventData(events.GetComponent<EventSystem>()){button=PointerEventData.InputButton.Left,pressPosition=new Vector2(100,100),position=new Vector2(102,160)};
                Assert.That(((RectTransform)slider.transform).rect.height,Is.GreaterThanOrEqualTo(44),"Thin track must retain a usable touch target");
                Assert.That(slider.fillRect.rect.height,Is.LessThanOrEqualTo(4.1f));
                Assert.That(slider.handleRect,Is.Not.Null);
                Assert.That(slider.handleRect.rect.height,Is.InRange(12,16),"Slider must not stretch the thumb across its touch target");
                var control=(YuiScrollableSettingSlider)slider;
                control.OnPointerDown(gesture);control.OnBeginDrag(gesture);control.OnDrag(gesture);control.OnPointerUp(gesture);control.OnEndDrag(gesture);
                Assert.That(slider.value,Is.EqualTo(.5f));
            } finally { Object.DestroyImmediate(dialog.gameObject);Object.DestroyImmediate(events); }
        }
        [TestCase("downloading",true)]
        [TestCase("preparing",true)]
        [TestCase("ready",false)]
        [TestCase("failed",false)]
        public void BackendDownloadPreparationIsAnActiveJob(string phase,bool active)
            => Assert.That(YuiChatPanel.BackendAssetPhaseActive(phase),Is.EqualTo(active));
        [TestCase(1f,44f)]
        [TestCase(2.5f,120f)]
        public void VisibleScrollbarWidthDoesNotGrowWithDragTarget(float scale,float targetWidth)
        {
            var root=new GameObject("Canvas",typeof(Canvas));
            try {
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.scaleFactor=scale;
                var bar=(Scrollbar)typeof(YuiSimpleDialog).GetMethod("CreateVerticalScrollbar",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{root.transform,"Bar"});
                ((RectTransform)bar.transform).sizeDelta=new Vector2(targetWidth,300);
                typeof(YuiScrollbarVisual).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(bar.GetComponent<YuiScrollbarVisual>(),null);
                Canvas.ForceUpdateCanvases();
                var thumb=(RectTransform)bar.transform.Find("Handle/Thumb");
                var displayScale=Screen.width>Screen.height?Screen.height/844f:Screen.width/390f;
                Assert.That(thumb.rect.width*canvas.scaleFactor,Is.EqualTo(3.8f*displayScale).Within(.01f));
                Assert.That(thumb.rect.width,Is.LessThan(targetWidth/4));
            } finally {Object.DestroyImmediate(root);}
        }
    }
}
