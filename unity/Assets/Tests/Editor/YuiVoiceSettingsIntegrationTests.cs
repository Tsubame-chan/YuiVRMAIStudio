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
            Assert.That(voices.Select(x=>x.Id),Is.EqualTo(new[]{10001,10002}));
            var tuning=YuiTtsTuningPrefs.Sanitize("irodori-native",new YuiSavedTtsTuning(10002,1,0,1,1,.1f,.1f));
            Assert.That(tuning.SpeakerId,Is.EqualTo(10002));
            Assert.That(YuiTtsTuningPrefs.NormalizeMode("irodori-native"),Is.Not.EqualTo(YuiTtsTuningPrefs.NormalizeMode("server")));
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
                // Unity deliberately shows AutoHide scrollbars in EditMode; visibility is verified in Player.
                Assert.That(scroll.content.rect.height,Is.LessThan(scroll.viewport.rect.height));
                dialog.Body.text=string.Join("\n",Enumerable.Repeat("Long explanation",100));
                Canvas.ForceUpdateCanvases();late.Invoke(scroll,null);
                Assert.That(scroll.verticalScrollbar,Is.Not.Null);
                Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
            } finally {Object.DestroyImmediate(dialog.gameObject);}
        }
        [Test] public void AdvancedDialogProvidesScrollbarAndVerticalDragDoesNotChangeValue()
        {
            var events=new GameObject("TestEvents",typeof(EventSystem));
            var dialog=YuiSimpleDialog.Create("Test","Body");
            try {
                var slider=dialog.AddSetting("Temperature",.5f,0,1,.05f,_=>{});
                for(var i=0;i<8;i++)dialog.AddButton("Action",()=>{});
                dialog.Compact(640);
                Canvas.ForceUpdateCanvases();
                var scroll=slider.GetComponentInParent<ScrollRect>();
                Assert.That(scroll,Is.Not.Null);
                Assert.That(dialog.Body.GetComponentInParent<ScrollRect>(),Is.SameAs(scroll));
                Assert.That(dialog.GetComponentsInChildren<ScrollRect>().Length,Is.EqualTo(1));
                Assert.That(dialog.GetComponentsInChildren<Scrollbar>().Length,Is.EqualTo(1));
                Assert.That(scroll.verticalScrollbar,Is.Not.Null);
                Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
                var gesture=new PointerEventData(events.GetComponent<EventSystem>()){button=PointerEventData.InputButton.Left,pressPosition=new Vector2(100,100),position=new Vector2(102,160)};
                var control=(YuiScrollableSettingSlider)slider;
                control.OnPointerDown(gesture);control.OnBeginDrag(gesture);control.OnDrag(gesture);control.OnPointerUp(gesture);control.OnEndDrag(gesture);
                Assert.That(slider.value,Is.EqualTo(.5f));
            } finally { Object.DestroyImmediate(dialog.gameObject);Object.DestroyImmediate(events); }
        }
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
