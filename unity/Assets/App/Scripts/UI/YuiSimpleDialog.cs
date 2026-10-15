using System;
using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    // Shared compact, safe-area dialog for onboarding and model management.
    public sealed class YuiSimpleDialog : MonoBehaviour
    {
        public Text Heading, Body, Progress;
        public Slider Gauge;
        private RectTransform safe;
        private Transform actions;
        private RectTransform tabsRect, footerRect;
        private float footerHeight;

        private float compactHeight;
        private bool fitContent;
        private float fittedHeight;
        private RectTransform closeRect;
        private Action closeAction;
        private Button primaryAction;
        private float headerHeight = 76;
        private float headerWidth = -1;
        private string headerText;
        private bool headerHasClose;

        private string measuredText;
        private int measuredActions = -1;
        private bool settingsStyle;
        private void RoundControl(Image image) { if(settingsStyle){YuiUiTheme.RoundSettings(image);image.pixelsPerUnitMultiplier=2;}else YuiUiTheme.Round(image); }
        private Vector2 measuredViewport;
        private static Vector2 ReferenceResolution => !Application.isMobilePlatform ? new Vector2(844,844) : Screen.width>Screen.height ? new Vector2(844,390) : new Vector2(390,844);
        private static float UiScale => !Application.isMobilePlatform ? Screen.height/844f : Screen.width>Screen.height ? Screen.height/390f : Screen.width/390f;
        private static float CardWidth => !Application.isMobilePlatform ? 560 : Screen.width>Screen.height ? 640 : 351;
        public static bool Japanese => YuiUiLocalization.Language == "ja";
        public static string L(string ja, string en) => Japanese ? ja : en;
        public static YuiSimpleDialog Create(string title, string message)
        {
            var root = new GameObject("YuiDialog", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32500;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution; scaler.matchWidthOrHeight = !Application.isMobilePlatform || Screen.width>Screen.height?1:0;
            var view = root.AddComponent<YuiSimpleDialog>();
            var shade = Box(root.transform, "Shade", new Color(0,0,0,.72f)); Place(shade.rectTransform, 0,0,1,1);
            view.safe = new GameObject("Safe", typeof(RectTransform)).GetComponent<RectTransform>(); view.safe.SetParent(root.transform,false);
            var card = Box(view.safe,"Card",new Color32(34,33,43,255)); Place(card.rectTransform,.05f,.12f,.95f,.88f); YuiUiTheme.Round(card);
            view.Heading = Label(card.transform,"Title",title,22); Place(view.Heading.rectTransform,.07f,.80f,.93f,.96f);
            var viewport = new GameObject("DialogViewport",typeof(RectTransform),typeof(RectMask2D),typeof(Image),typeof(ScrollRect));
            viewport.transform.SetParent(card.transform,false);
            viewport.GetComponent<Image>().color=Color.clear;
            var content=new GameObject("DialogContent",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform,false);
            var contentRect=(RectTransform)content.transform;
            contentRect.anchorMin=new Vector2(0,1);contentRect.anchorMax=Vector2.one;contentRect.pivot=new Vector2(.5f,1);
            contentRect.offsetMin=contentRect.offsetMax=Vector2.zero;
            var flow=content.GetComponent<VerticalLayoutGroup>();flow.spacing=16;flow.childForceExpandHeight=false;flow.childControlHeight=true;flow.childControlWidth=true;
            content.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            view.Body = Label(content.transform,"Body",message,17);
            view.Body.alignment = TextAnchor.UpperLeft;
            var scroll=viewport.GetComponent<ScrollRect>();scroll.viewport=(RectTransform)viewport.transform;scroll.content=contentRect;
            scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            view.Progress = Label(card.transform,"Progress","",14); Place(view.Progress.rectTransform,.07f,.06f,.93f,.12f);
            var rail = Box(card.transform,"Gauge",new Color32(68,62,81,255)); Place(rail.rectTransform,.07f,.04f,.93f,.05f);
            var fill = Box(rail.transform,"Fill",YuiUiTheme.Accent); Place(fill.rectTransform,0,0,1,1);
            view.Gauge = rail.gameObject.AddComponent<Slider>(); view.Gauge.fillRect = fill.rectTransform; view.Gauge.interactable=false; rail.gameObject.SetActive(false);
            var buttons = new GameObject("Actions",typeof(RectTransform),typeof(VerticalLayoutGroup)); buttons.transform.SetParent(content.transform,false);
            var layout = buttons.GetComponent<VerticalLayoutGroup>(); layout.spacing=8; layout.childForceExpandHeight=true;
            view.actions=buttons.transform; view.ConfigureActionScroll(card.transform,.04f,.79f); view.Update(); return view;
        }
        public Button AddButton(string text, Action action) => AddButtonTo(actions,text,action);
        private Button AddButtonTo(Transform parent,string text,Action action)
        {
            var image=Box(parent,"Action",YuiUiTheme.Field); RoundControl(image);
            var outline=image.gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(1,-1);
            outline.effectColor=YuiControlAffordance.OutlineColor;outline.useGraphicAlpha=false;
            image.gameObject.AddComponent<LayoutElement>().minHeight=48;
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var label=Label(image.transform,"Label",text,16); Place(label.rectTransform,.03f,0,.97f,1);
            button.onClick.AddListener(()=>action?.Invoke()); return button;
        }
        public Button AddFooterButton(string text,Action action,bool primary=false)
        {
            if(footerRect==null) {
                footerRect=new GameObject("DialogFooter",typeof(RectTransform),typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
                footerRect.SetParent(Heading.transform.parent,false);
                footerRect.anchorMin=new Vector2(.07f,0);footerRect.anchorMax=new Vector2(.93f,0);footerRect.pivot=new Vector2(.5f,0);
                footerRect.anchoredPosition=new Vector2(0,16);
                var flow=footerRect.GetComponent<VerticalLayoutGroup>();flow.spacing=8;flow.childControlWidth=flow.childControlHeight=true;flow.childForceExpandHeight=false;
            }
            var button=AddButtonTo(footerRect,text,action);YuiUiTheme.ButtonStyle(button,primary);RoundControl(button.GetComponent<Image>());button.GetComponentInChildren<Text>().fontSize=16;
            if(primary)primaryAction=button;
            measuredActions=-1;return button;
        }
        public void SettingsTabs(int selected,string[] labels,Action<int> choose)
        {
            settingsStyle=true;
            actions.GetComponent<VerticalLayoutGroup>().spacing=12;
            Heading.GetComponentInParent<Image>().color=YuiUiTheme.Surface;
            tabsRect=new GameObject("DialogTabs",typeof(RectTransform),typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            tabsRect.SetParent(Heading.transform.parent,false);
            tabsRect.anchorMin=new Vector2(.07f,1);tabsRect.anchorMax=new Vector2(.93f,1);tabsRect.pivot=new Vector2(.5f,1);tabsRect.sizeDelta=new Vector2(0,48);
            var flow=tabsRect.GetComponent<HorizontalLayoutGroup>();flow.spacing=4;flow.childControlWidth=flow.childControlHeight=true;flow.childForceExpandWidth=true;
            for(int i=0;i<labels.Length;i++) {
                var index=i;var image=Box(tabsRect,"Tab"+i,i==selected?YuiUiTheme.Selected:YuiUiTheme.Surface);YuiUiTheme.Round(image);
                RoundControl(image);image.gameObject.AddComponent<LayoutElement>().minHeight=44;
                var button=image.gameObject.AddComponent<Button>();var text=Label(image.transform,"Label",labels[i],16);Place(text.rectTransform,0,0,1,1);
                button.onClick.AddListener(()=>choose(index));
            }
            headerWidth=-1;measuredActions=-1;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(tabsRect.Find("Tab"+selected).gameObject);
        }
        public Toggle AddToggle(string title,bool value,Action<bool> changed)
        {
            var image=Box(actions,"ToggleSetting",YuiUiTheme.Field);YuiUiTheme.Round(image);image.gameObject.AddComponent<LayoutElement>().minHeight=52;
            var label=Label(image.transform,"Label",title,16);Place(label.rectTransform,.04f,0,.78f,1);label.alignment=TextAnchor.MiddleLeft;
            var box=Box(image.transform,"Checkbox",YuiControlAffordance.InputSurface);Place(box.rectTransform,.83f,.25f,.94f,.75f);YuiUiTheme.Round(box);
            var check=Box(box.transform,"Check",YuiUiTheme.Accent);Place(check.rectTransform,.2f,.2f,.8f,.8f);YuiUiTheme.Round(check);
            var toggle=image.gameObject.AddComponent<Toggle>();toggle.targetGraphic=image;toggle.graphic=check;toggle.isOn=value;toggle.onValueChanged.AddListener(v=>changed(v));return toggle;
        }
        public void SetProgress(float value)
        { Gauge.gameObject.SetActive(true); Gauge.value=Mathf.Clamp01(value); Progress.text=value<0?L("準備中…","Preparing…"):$"{Mathf.FloorToInt(Gauge.value*100)}% / 100%"; }
        public void Close() { gameObject.SetActive(false); Destroy(gameObject); }
        public Slider AddSetting(string title,float value,float minimum,float maximum,float step,Action<float> changed)
        {
            var row=Box(actions,"Setting",settingsStyle?Color.clear:YuiUiTheme.Field);RoundControl(row);
            row.gameObject.AddComponent<LayoutElement>().minHeight=64;
            var caption=Label(row.transform,"Label",title+" · "+value.ToString("0.##"),14);
            Place(caption.rectTransform,.04f,.625f,.96f,1);caption.alignment=TextAnchor.UpperLeft;caption.verticalOverflow=VerticalWrapMode.Overflow;
            var hit=Box(row.transform,"SliderHitArea",Color.clear);hit.rectTransform.anchorMin=new Vector2(.05f,0);hit.rectTransform.anchorMax=new Vector2(.95f,0);hit.rectTransform.pivot=new Vector2(.5f,0);hit.rectTransform.sizeDelta=new Vector2(0,44);
            var rail=Box(hit.transform,"Slider",YuiUiTheme.Muted);
            rail.rectTransform.anchorMin=new Vector2(0,.5f);rail.rectTransform.anchorMax=new Vector2(1,.5f);rail.rectTransform.sizeDelta=new Vector2(0,4);rail.raycastTarget=false;
            var fill=Box(rail.transform,"Fill",YuiUiTheme.Accent);Place(fill.rectTransform,0,0,1,1);
            var handleArea=new GameObject("HandleSlideArea",typeof(RectTransform)).GetComponent<RectTransform>();handleArea.SetParent(hit.transform,false);
            handleArea.anchorMin=new Vector2(0,.5f);handleArea.anchorMax=new Vector2(1,.5f);handleArea.sizeDelta=new Vector2(0,14);
            var knob=Box(handleArea,"Handle",YuiUiTheme.Accent);RoundControl(knob);
            knob.rectTransform.sizeDelta=new Vector2(14,0);knob.raycastTarget=false;
            var slider=hit.gameObject.AddComponent<YuiScrollableSettingSlider>();slider.fillRect=fill.rectTransform;slider.handleRect=knob.rectTransform;slider.targetGraphic=knob;
            slider.minValue=minimum;slider.maxValue=maximum;slider.value=value;
            slider.onValueChanged.AddListener(v=> {
                var rounded=Mathf.Clamp(Mathf.Round(v/step)*step,minimum,maximum);
                caption.text=title+" · "+rounded.ToString("0.##");measuredActions=-1;changed(rounded);
            });
            return slider;
        }
        public InputField AddInput(string title,string value,Action<string> changed,bool multiline=true)
        {
            var row=Box(actions,"PromptSetting",YuiUiTheme.Field);RoundControl(row);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight=multiline?144:72;
            var caption=Label(row.transform,"Label",title,14);Place(caption.rectTransform,.04f,.64f,.96f,.98f);
            var field=Box(row.transform,"Input",YuiUiTheme.Surface);Place(field.rectTransform,.04f,.04f,.96f,.61f);
            var text=Label(field.transform,"Text",value,15);Place(text.rectTransform,.03f,.03f,.97f,.97f);
            text.alignment=TextAnchor.UpperLeft;
            var input=field.gameObject.AddComponent<InputField>();input.textComponent=text;input.lineType=multiline?InputField.LineType.MultiLineNewline:InputField.LineType.SingleLine;
            input.characterLimit=multiline?2000:128;input.text=value;input.onValueChanged.AddListener(v=>changed(v));
            return input;
        }
        public void Compact(float height = 520)
        {
            compactHeight=height;
            var card = (RectTransform)Heading.transform.parent;
            card.anchorMin = card.anchorMax = new Vector2(.5f,.5f);
            card.pivot = new Vector2(.5f,.5f);
            var landscape=Screen.width>Screen.height;
            var scale=UiScale;
            card.sizeDelta = new Vector2(CardWidth, Mathf.Min(height, Screen.height/Mathf.Max(.01f,scale)*.90f));
            card.anchoredPosition = Vector2.zero;
            ConfigureActionScroll(card,.04f,.83f);
            headerWidth=-1;LayoutHeader(card);

        }
        // Opt in for short notices and model management. Long content still
        // uses the same maximum height and shared scrolling controls.
        public void FitToContent(float maximumHeight = 520)
        {
            fitContent=true; fittedHeight=0; Compact(maximumHeight);
        }
        private void LayoutHeader(RectTransform card)
        {
            card.ForceUpdateRectTransforms();
            bool hasClose=closeRect!=null;
            if(headerText==Heading.text && Mathf.Approximately(headerWidth,card.rect.width) && headerHasClose==hasClose)return;
            headerText=Heading.text;headerWidth=card.rect.width;headerHasClose=hasClose;
            measuredActions=-1;
            var title=Heading.rectTransform;
            title.anchorMin=new Vector2(0,1);title.anchorMax=Vector2.one;title.pivot=new Vector2(.5f,1);
            title.offsetMin=new Vector2(24,-76);title.offsetMax=new Vector2(closeRect!=null?-76:-24,-16);
            title.ForceUpdateRectTransforms();
            Heading.alignment=TextAnchor.UpperLeft;
            var titleHeight=Mathf.Max(44,Heading.preferredHeight)+32;
            title.offsetMin=new Vector2(24,-titleHeight+16);
            headerHeight=titleHeight;
            if(tabsRect!=null){tabsRect.anchoredPosition=new Vector2(0,-titleHeight);headerHeight+=64;}
            var viewport=(RectTransform)card.Find("DialogViewport");
            viewport.anchorMax=new Vector2(viewport.anchorMax.x,1);
            viewport.offsetMax=new Vector2(viewport.offsetMax.x,-headerHeight);
            var bar=(RectTransform)card.Find("DialogScrollbar");
            bar.anchorMax=new Vector2(bar.anchorMax.x,1);bar.offsetMax=new Vector2(0,-headerHeight);
            if(closeRect!=null) {
                closeRect.anchorMin=closeRect.anchorMax=Vector2.one;closeRect.pivot=Vector2.one;
                closeRect.sizeDelta=new Vector2(44,44);closeRect.anchoredPosition=new Vector2(-16,-16);
            }
        }
        public Button AddModelChoice(string title,string status,Action action,bool selected)
        {
            var button=AddButton(title,action);
            button.gameObject.name="ModelChoice";button.GetComponent<LayoutElement>().minHeight=52;
            var image=button.GetComponent<Image>();image.color=selected?YuiUiTheme.Selected:YuiUiTheme.Field;
            var outline=image.GetComponent<Outline>();
            outline.effectColor=selected?YuiUiTheme.Accent:YuiControlAffordance.OutlineColor;outline.useGraphicAlpha=false;outline.enabled=selected;
            var label=button.GetComponentInChildren<Text>();label.fontSize=15;label.alignment=TextAnchor.UpperLeft;label.verticalOverflow=VerticalWrapMode.Overflow;
            label.rectTransform.anchorMin=new Vector2(0,1);label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.offsetMin=new Vector2(14,-26);label.rectTransform.offsetMax=new Vector2(-52,-6);
            var detail=Label(button.transform,"Status",status,12);detail.color=YuiUiTheme.Muted;detail.alignment=TextAnchor.UpperLeft;detail.verticalOverflow=VerticalWrapMode.Overflow;
            detail.rectTransform.anchorMin=new Vector2(0,1);detail.rectTransform.anchorMax=Vector2.one;
            detail.rectTransform.offsetMin=new Vector2(14,-46);detail.rectTransform.offsetMax=new Vector2(-52,-28);
            YuiControlAffordance.SelectionMark(button,selected);
            var mark=(RectTransform)button.transform.Find("SelectionMark");mark.sizeDelta=new Vector2(24,24);mark.anchoredPosition=new Vector2(-26,0);
            if(!selected) {
                var arrow=Label(button.transform,"ChoiceArrow","○",18);
                arrow.rectTransform.anchorMin=arrow.rectTransform.anchorMax=new Vector2(1,.5f);
                arrow.rectTransform.sizeDelta=new Vector2(36,36);arrow.rectTransform.anchoredPosition=new Vector2(-28,0);
            }
            return button;
        }
        private void ConfigureActionScroll(Transform card,float bottom,float top)
        {
            var viewport=(RectTransform)card.Find("DialogViewport");
            Place(viewport,.07f,bottom,.93f,top);
            measuredActions = -1;
            var layout=actions.GetComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight=false;layout.childControlHeight=true;layout.childControlWidth=true;
            var scroll=viewport.GetComponent<ScrollRect>();
            var existing=card.Find("DialogScrollbar");
            var bar=existing!=null?existing.GetComponent<Scrollbar>():CreateVerticalScrollbar(card,"DialogScrollbar");
            var rect=(RectTransform)bar.transform;
            rect.anchorMin=new Vector2(.93f,bottom);rect.anchorMax=new Vector2(.93f,top);rect.pivot=new Vector2(1,.5f);
            rect.offsetMin=new Vector2(-44,0);rect.offsetMax=Vector2.zero;
            scroll.scrollSensitivity=24;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }
        internal static Scrollbar CreateVerticalScrollbar(Transform parent,string name)
        {
            var rail=Box(parent,name,Color.clear);
            var track=Box(rail.transform,"Track",YuiUiTheme.Muted);Place(track.rectTransform,.45f,0,.55f,1);track.raycastTarget=false;
            var handle=new GameObject("Handle",typeof(RectTransform)).GetComponent<RectTransform>();handle.SetParent(rail.transform,false);
            handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;
            var thumb=Box(handle,"Thumb",YuiUiTheme.Accent);Place(thumb.rectTransform,.38f,0,.62f,1);YuiUiTheme.Round(thumb);thumb.raycastTarget=false;
            rail.gameObject.AddComponent<YuiScrollbarVisual>();
            var bar=rail.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=thumb;bar.direction=Scrollbar.Direction.BottomToTop;
            return bar;
        }
        public void InvokePrimaryAction() {if(primaryAction!=null && primaryAction.IsActive() && primaryAction.IsInteractable())primaryAction.onClick.Invoke();}
        public void RequestClose() => closeAction?.Invoke();
        public void AddCloseButton(Action action)
        {
            closeAction=action;
            var image=Box(Heading.transform.parent,"Close",YuiUiTheme.Field);Place(image.rectTransform,.84f,.89f,.98f,.98f);YuiUiTheme.Round(image);
            var label=Label(image.transform,"Label","×",22);Place(label.rectTransform,0,0,1,1);
            image.gameObject.AddComponent<Button>().onClick.AddListener(()=>action?.Invoke());
            closeRect=image.rectTransform;
            if(compactHeight>0)LayoutHeader((RectTransform)Heading.transform.parent);
            else Heading.rectTransform.anchorMax=new Vector2(.82f,Heading.rectTransform.anchorMax.y);
            measuredActions=-1;
        }
        private void Update()
        {
            if(safe==null)return;
            var scaler=GetComponent<CanvasScaler>();
            scaler.referenceResolution=ReferenceResolution;
            scaler.matchWidthOrHeight=!Application.isMobilePlatform || Screen.width>Screen.height?1:0;
            var a=Screen.safeArea;
            var keyboard=TouchScreenKeyboard.visible?TouchScreenKeyboard.area.height:0;
            var bottom=Mathf.Max(a.yMin,keyboard);
            safe.anchorMin=new Vector2(a.xMin/Mathf.Max(1,Screen.width),bottom/Mathf.Max(1,Screen.height));
            safe.anchorMax=new Vector2(a.xMax/Mathf.Max(1,Screen.width),a.yMax/Mathf.Max(1,Screen.height));safe.offsetMin=safe.offsetMax=Vector2.zero;
            var viewport=(RectTransform)Heading.transform.parent.Find("DialogViewport");
            var lower=Gauge.gameObject.activeSelf || !string.IsNullOrEmpty(Progress.text) ? .14f : .04f;
            if(footerRect!=null) {
                footerHeight=LayoutUtility.GetPreferredHeight(footerRect);footerRect.sizeDelta=new Vector2(0,footerHeight);
                lower=0;
                viewport.offsetMin=new Vector2(0,footerHeight+32);
            }
            viewport.anchorMin=new Vector2(viewport.anchorMin.x,lower);
            var bar=(RectTransform)Heading.transform.parent.Find("DialogScrollbar");
            bar.anchorMin=new Vector2(bar.anchorMin.x,lower);
            if(footerRect!=null)bar.offsetMin=new Vector2(bar.offsetMin.x,footerHeight+32);
            if(compactHeight>0)
            {
                var card=(RectTransform)Heading.transform.parent;
                var landscape=Screen.width>Screen.height;
                var scale=UiScale;
                card.sizeDelta=new Vector2(Mathf.Min(CardWidth,a.width/Mathf.Max(.01f,scale)*.9f),Mathf.Min(fitContent&&fittedHeight>0?Mathf.Min(compactHeight,fittedHeight):compactHeight,Mathf.Max(1,a.yMax-bottom)/Mathf.Max(.01f,scale)*.9f));
                LayoutHeader(card);
            }
        }
        private void LateUpdate() { RefreshContentWidth(); }
        private void RefreshContentWidth()
        {
            if (Body == null || actions == null) return;
            var scroll = Body.GetComponentInParent<ScrollRect>();
            var viewport = scroll.viewport;
            viewport.ForceUpdateRectTransforms();
            // Measure at the full width, so a wrapped paragraph cannot keep an
            // unnecessary scrollbar gutter after the content becomes shorter.
            var fullSize = viewport.rect.size - new Vector2(viewport.offsetMax.x, 0);
            if (measuredActions == actions.childCount && measuredText == Body.text && measuredViewport == fullSize) return;
            measuredActions = actions.childCount; measuredText = Body.text; measuredViewport = fullSize;
            viewport.offsetMax = new Vector2(0,viewport.offsetMax.y);
            viewport.ForceUpdateRectTransforms();
            MeasureActionRows(scroll.content);
            if(fitContent) {
                var lower=viewport.anchorMin.y;
                fittedHeight=Mathf.Max(160,(headerHeight+scroll.content.rect.height+16+(footerRect!=null?footerHeight+32:0))/(1-lower));
                Update();viewport.ForceUpdateRectTransforms();
            }
            if (scroll.content.rect.height > viewport.rect.height + .5f)
            {
                viewport.offsetMax = new Vector2(-44,viewport.offsetMax.y);
                viewport.ForceUpdateRectTransforms();
                MeasureActionRows(scroll.content);
            }
        }
        private void MeasureActionRows(RectTransform content)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            foreach(Transform row in actions) {
                if(row.name=="Setting") {
                    var caption=row.Find("Label").GetComponent<Text>();
                    var height=Mathf.Ceil(caption.preferredHeight)+2;
                    caption.rectTransform.anchorMin=new Vector2(.04f,1);caption.rectTransform.anchorMax=new Vector2(.96f,1);
                    caption.rectTransform.offsetMin=new Vector2(0,-height);caption.rectTransform.offsetMax=Vector2.zero;
                    row.GetComponent<LayoutElement>().preferredHeight=Mathf.Max(64,height+44);
                }
                else if(row.name=="ModelChoice") {
                    var title=row.Find("Label").GetComponent<Text>();var detail=row.Find("Status").GetComponent<Text>();
                    // Legacy uGUI can cull every glyph at an exact preferred-height boundary after pixel rounding.
                    var titleHeight=Mathf.Ceil(title.preferredHeight)+2;var detailHeight=Mathf.Ceil(detail.preferredHeight)+2;
                    title.rectTransform.offsetMin=new Vector2(14,-6-titleHeight);title.rectTransform.offsetMax=new Vector2(-52,-6);
                    detail.rectTransform.offsetMin=new Vector2(14,-8-titleHeight-detailHeight);detail.rectTransform.offsetMax=new Vector2(-52,-8-titleHeight);
                    row.GetComponent<LayoutElement>().preferredHeight=Mathf.Max(52,titleHeight+detailHeight+14);
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
        private static Image Box(Transform parent,string name,Color color)
        { var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);var i=g.GetComponent<Image>();i.color=color;return i; }
        private static Text Label(Transform parent,string name,string value,int size)
        { var g=new GameObject(name,typeof(RectTransform),typeof(Text));g.transform.SetParent(parent,false);var t=g.GetComponent<Text>();t.text=value;t.font=YuiUiTypography.Regular;t.fontSize=size;t.color=YuiUiTheme.Text;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.supportRichText=false;return t; }
        private static void Place(RectTransform r,float x,float y,float xx,float yy)
        { r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(xx,yy);r.offsetMin=r.offsetMax=Vector2.zero; }
    }
}
