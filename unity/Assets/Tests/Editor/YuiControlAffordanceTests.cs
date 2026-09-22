using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.UI;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiControlAffordanceTests
    {
        [TestCase(2)]
        [TestCase(6)]
        [TestCase(12)]
        public void Dropdown_TemplateDoesNotTurnUnusedRowsIntoBottomPadding(int optionCount)
        {
            var go=DefaultControls.CreateDropdown(new DefaultControls.Resources());
            try
            {
                var dropdown=go.GetComponent<Dropdown>();dropdown.ClearOptions();
                for(var i=0;i<optionCount;i++) dropdown.options.Add(new Dropdown.OptionData("Option "+i));
                var prepare=typeof(YuiSettingsOverlay).GetMethod("PrepareDropdownTemplateRuntime",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(Dropdown),typeof(float),typeof(float)},null);
                prepare.Invoke(null,new object[]{dropdown,432f,72f});
                var content=(RectTransform)dropdown.template.Find("Viewport/Content");
                var item=(RectTransform)content.Find("Item");
                // uGUI computes popup padding from these two bounds before cloning rows.
                var bottom=item.rect.min.y-content.rect.min.y+item.localPosition.y;
                var top=item.rect.max.y-content.rect.max.y+item.localPosition.y;
                Assert.AreEqual(0f,bottom-top,.01f,"No fictitious padding below the options");
                Assert.AreEqual(72f,item.rect.height,.01f);
            }
            finally {Object.DestroyImmediate(go);}
        }
        [Test]
        public void SettingsFocus_LeavesInputForItsLabeledSwitchWithoutChangingEitherValue()
        {
            var previousEvents=UnityEngine.EventSystems.EventSystem.current;
            var eventObject=new GameObject("Events",typeof(UnityEngine.EventSystems.EventSystem));
            var events=eventObject.GetComponent<UnityEngine.EventSystems.EventSystem>();
            // EditMode does not dispatch this non-ExecuteAlways component's lifecycle.
            typeof(UnityEngine.EventSystems.EventSystem).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(events,null);
            var scope=new GameObject("Settings",typeof(RectTransform));
            var inputObject=new GameObject("Backend",typeof(RectTransform),typeof(InputField));inputObject.transform.SetParent(scope.transform,false);
            var switchObject=new GameObject("Recovery",typeof(RectTransform),typeof(Toggle));switchObject.transform.SetParent(scope.transform,false);
            try
            {
                var input=inputObject.GetComponent<InputField>();var toggle=switchObject.GetComponent<Toggle>();toggle.SetIsOnWithoutNotify(true);
                input.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnDown=toggle};
                toggle.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=input};
                UnityEngine.EventSystems.EventSystem.current=events;
                events.SetSelectedGameObject(inputObject);
                YuiControlAffordance.MoveSettingsFocus(scope.transform,false);Assert.AreSame(switchObject,events.currentSelectedGameObject);Assert.IsTrue(toggle.isOn);
                YuiControlAffordance.MoveSettingsFocus(scope.transform,true);Assert.AreSame(inputObject,events.currentSelectedGameObject);
            }
            finally
            {
                Object.DestroyImmediate(scope);
                typeof(UnityEngine.EventSystems.EventSystem).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(events,null);
                Object.DestroyImmediate(eventObject);
                if(previousEvents!=null) UnityEngine.EventSystems.EventSystem.current=previousEvents;
            }
        }
        [TestCase(1080,1920)]
        [TestCase(1920,1080)]
        [TestCase(850,1511)]
        public void Conversation_WorkExpandsReadingAreaWithoutEnlargingComposer(int width,int height)
        {
            var parent=new GameObject("SafeArea",typeof(RectTransform));((RectTransform)parent.transform).sizeDelta=new Vector2(width,height);
            var go=new GameObject("Conversation",typeof(RectTransform));go.transform.SetParent(parent.transform,false);go.SetActive(false);
            try
            {
                var panel=go.AddComponent<YuiChatPanel>();
                var flags=BindingFlags.NonPublic|BindingFlags.Instance;
                var attach=new GameObject("Attach",typeof(RectTransform),typeof(Button));attach.transform.SetParent(go.transform,false);
                typeof(YuiChatPanel).GetField("composerAttachButton",flags).SetValue(panel,attach.GetComponent<Button>());
                var field=new GameObject("Input",typeof(RectTransform),typeof(InputField));field.transform.SetParent(go.transform,false);
                typeof(YuiChatPanel).GetField("inputField",flags).SetValue(panel,field.GetComponent<InputField>());
                var refresh=typeof(YuiChatPanel).GetMethod("RefreshConversationLayout",flags);
                var mode=typeof(YuiChatPanel).GetField("chatInteractionMode",flags);
                mode.SetValue(panel,YuiChatRequestModes.Talk);refresh.Invoke(panel,null);
                var talkHeight=((RectTransform)go.transform).rect.height;
                var composerHeight=((RectTransform)field.transform).rect.height;
                mode.SetValue(panel,YuiChatRequestModes.Work);refresh.Invoke(panel,null);
                Assert.GreaterOrEqual(((RectTransform)go.transform).rect.height,talkHeight);
                Assert.AreEqual(composerHeight,((RectTransform)field.transform).rect.height);
                Assert.Greater(((RectTransform)field.transform).rect.width,200);
                Assert.LessOrEqual(((RectTransform)go.transform).rect.width,1080);
                Assert.Less(((RectTransform)go.transform).rect.height,height);
            }
            finally {Object.DestroyImmediate(parent);}
        }
        [Test]
        public void Symbols_LoadAsTwoDimensionalTexturesOnPlayerResourcePaths()
        {
            foreach(var name in new[]{"settings","visibility_off","help","close","expand_more","attach_file","check"})
            {
                var texture=Resources.Load<Texture2D>("YuiSymbols/"+name);
                Assert.IsNotNull(texture,name);Assert.AreEqual(192,texture.width,name);
                Assert.IsNotNull(YuiToolbarIconUtility.LoadSymbol(name),name);
            }
        }
        [Test]
        public void RecoverySwitch_LabelsCorrectChildAndPreservesValueAndExistingListener()
        {
            var parent=new GameObject("Settings",typeof(RectTransform));
            try
            {
                var create=typeof(YuiSettingsOverlay).GetMethod("EnsureRuntimeToggle",BindingFlags.Static|BindingFlags.NonPublic);
                var toggle=(Toggle)create.Invoke(null,new object[]{parent.transform,null,"AutoAiFallbackToggle","Backend offline -> Local Gemma"});
                toggle.SetIsOnWithoutNotify(false);
                int changes=0;toggle.onValueChanged.AddListener(value=>changes++);
                var view=toggle.gameObject.AddComponent<YuiSettingsSwitch>();view.Configure();view.Configure();
                Assert.IsFalse(toggle.isOn);Assert.AreEqual(0,changes);
                Assert.That(toggle.transform.Find("Label").GetComponent<Text>().text,Is.EqualTo(YuiUiLocalization.Text("Continue with on-device AI")));
                Assert.IsFalse(toggle.transform.Find("Background").gameObject.activeSelf);
                var thumb=(RectTransform)toggle.transform.Find("SwitchTrack/Thumb");
                Assert.AreEqual(0,thumb.anchorMin.x);
                toggle.isOn=true;Assert.AreEqual(1,changes);Assert.AreEqual(1,thumb.anchorMin.x);
                toggle.SetIsOnWithoutNotify(false);view.Refresh();Assert.AreEqual(0,thumb.anchorMin.x);
            }
            finally {Object.DestroyImmediate(parent);}
        }
        [Test]
        public void Background_KeepsSavedIndicesAndRestoresCameraSkybox()
        {
            var had=PlayerPrefs.HasKey(YuiPrefsKeys.BackgroundPreset);var saved=PlayerPrefs.GetInt(YuiPrefsKeys.BackgroundPreset);
            var go=new GameObject("Background test",typeof(Camera));var original=new Material(Shader.Find("Skybox/Procedural"));
            var ambient=RenderSettings.ambientLight;
            var ambientMode=RenderSettings.ambientMode;
            try
            {
                Assert.AreEqual(0,(int)YuiBackgroundPreset.Studio);Assert.AreEqual(4,(int)YuiBackgroundPreset.UnityDefault);Assert.AreEqual(5,(int)YuiBackgroundPreset.SoftGradient);
                Assert.AreEqual(6,YuiBackgroundManager.OptionLabels.Length);
                var sky=go.AddComponent<Skybox>();sky.material=original;sky.enabled=true;
                var manager=go.AddComponent<YuiBackgroundManager>();
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight=new Color(.3f,.4f,.5f);
                var sceneAmbient=RenderSettings.ambientLight;
                typeof(YuiBackgroundManager).GetField("targetCamera",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(manager,go.GetComponent<Camera>());
                manager.SetPreset(YuiBackgroundPreset.SoftGradient);
                Assert.AreEqual("Yui/Background/SoftGradient",sky.material.shader.name);
                Assert.AreEqual(CameraClearFlags.Skybox,go.GetComponent<Camera>().clearFlags);
                var sample=sky.material;
                var originalBottom=sample.GetVector("_BottomColor");
                manager.SetPreset(YuiBackgroundPreset.Studio);Assert.AreSame(sample,sky.material);
                Assert.AreNotEqual(originalBottom,sample.GetVector("_BottomColor"));
                Assert.AreEqual(CameraClearFlags.Skybox,go.GetComponent<Camera>().clearFlags);
                manager.SetPreset(YuiBackgroundPreset.SoftGradient);Assert.AreSame(sample,sky.material);
                manager.SetPreset(YuiBackgroundPreset.UnityDefault);Assert.AreSame(original,sky.material);
                foreach(YuiBackgroundPreset background in System.Enum.GetValues(typeof(YuiBackgroundPreset)))
                {
                    manager.SetPreset(background);
                    Assert.AreEqual(sceneAmbient,RenderSettings.ambientLight,"Backdrop must not recolor avatars");
                    Assert.AreEqual(UnityEngine.Rendering.AmbientMode.Flat,RenderSettings.ambientMode);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);Object.DestroyImmediate(original);RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=ambientMode;
                if(had)PlayerPrefs.SetInt(YuiPrefsKeys.BackgroundPreset,saved);else PlayerPrefs.DeleteKey(YuiPrefsKeys.BackgroundPreset);PlayerPrefs.Save();
            }
        }
        [Test]
        public void ControlStyling_DoesNotReplaceUserTextOrDuplicateScrollbar()
        {
            var root=new GameObject("Scroll",typeof(RectTransform),typeof(ScrollRect));
            var field=new GameObject("VoicePresetNameInput",typeof(RectTransform),typeof(Image),typeof(InputField));field.transform.SetParent(root.transform,false);
            var text=new GameObject("Text",typeof(RectTransform),typeof(Text));text.transform.SetParent(field.transform,false);
            try
            {
                var input=field.GetComponent<InputField>();input.targetGraphic=field.GetComponent<Image>();input.textComponent=text.GetComponent<Text>();input.textComponent.font=YuiUiTypography.Regular;input.text="My voice 01";
                YuiControlAffordance.Input(input);YuiControlAffordance.Input(input);Assert.AreEqual("My voice 01",input.text);Assert.AreEqual(1,field.GetComponents<Outline>().Length);
                Assert.IsNotNull(input.placeholder);Assert.AreEqual(YuiUiLocalization.Text("e.g. Calm voice"),((Text)input.placeholder).text);
                var scroll=root.GetComponent<ScrollRect>();scroll.viewport=(RectTransform)field.transform;
                YuiControlAffordance.Scrollbar(scroll);YuiControlAffordance.Scrollbar(scroll);
                Assert.AreEqual(1,root.GetComponentsInChildren<Scrollbar>().Length);Assert.IsNotNull(scroll.verticalScrollbar.handleRect);
                Assert.AreEqual(ScrollRect.ScrollbarVisibility.AutoHide,scroll.verticalScrollbarVisibility);
            }
            finally {Object.DestroyImmediate(root);}
        }
    }
}
