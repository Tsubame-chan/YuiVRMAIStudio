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
        private float compactHeight;
        public static bool Japanese => YuiUiLocalization.Language == "ja";
        public static string L(string ja, string en) => Japanese ? ja : en;
        public static YuiSimpleDialog Create(string title, string message)
        {
            var root = new GameObject("YuiDialog", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32500;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844); scaler.matchWidthOrHeight = Screen.width>Screen.height?1:0;
            var view = root.AddComponent<YuiSimpleDialog>();
            var shade = Box(root.transform, "Shade", new Color(0,0,0,.72f)); Place(shade.rectTransform, 0,0,1,1);
            view.safe = new GameObject("Safe", typeof(RectTransform)).GetComponent<RectTransform>(); view.safe.SetParent(root.transform,false);
            var card = Box(view.safe,"Card",new Color32(34,33,43,255)); Place(card.rectTransform,.05f,.12f,.95f,.88f); YuiUiTheme.Round(card);
            view.Heading = Label(card.transform,"Title",title,22); Place(view.Heading.rectTransform,.07f,.80f,.93f,.96f);
            var viewport = new GameObject("BodyViewport",typeof(RectTransform),typeof(RectMask2D),typeof(Image),typeof(ScrollRect));
            viewport.transform.SetParent(card.transform,false); Place((RectTransform)viewport.transform,.07f,.37f,.93f,.79f);
            viewport.GetComponent<Image>().color=Color.clear;
            view.Body = Label(viewport.transform,"Body",message,17);
            var bodyRect=view.Body.rectTransform;bodyRect.anchorMin=new Vector2(0,1);bodyRect.anchorMax=Vector2.one;bodyRect.pivot=new Vector2(.5f,1);bodyRect.offsetMin=bodyRect.offsetMax=Vector2.zero;
            view.Body.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.GetComponent<ScrollRect>();scroll.viewport=(RectTransform)viewport.transform;scroll.content=bodyRect;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            view.Body.alignment = TextAnchor.UpperLeft;
            view.Progress = Label(card.transform,"Progress","",14); Place(view.Progress.rectTransform,.07f,.31f,.93f,.37f);
            var rail = Box(card.transform,"Gauge",new Color32(68,62,81,255)); Place(rail.rectTransform,.07f,.29f,.93f,.30f);
            var fill = Box(rail.transform,"Fill",YuiUiTheme.Accent); Place(fill.rectTransform,0,0,1,1);
            view.Gauge = rail.gameObject.AddComponent<Slider>(); view.Gauge.fillRect = fill.rectTransform; view.Gauge.interactable=false; rail.gameObject.SetActive(false);
            var buttons = new GameObject("Actions",typeof(RectTransform),typeof(VerticalLayoutGroup)); buttons.transform.SetParent(card.transform,false);
            Place((RectTransform)buttons.transform,.07f,.025f,.93f,.28f);
            var layout = buttons.GetComponent<VerticalLayoutGroup>(); layout.spacing=8; layout.childForceExpandHeight=true;
            view.actions=buttons.transform; view.ConfigureActionScroll(card.transform,.025f,.28f); view.ShowBodyScrollbar(); view.Update(); return view;
        }
        public Button AddButton(string text, Action action)
        {
            var image=Box(actions,"Action",YuiUiTheme.Field); YuiUiTheme.Round(image);
            image.gameObject.AddComponent<LayoutElement>().minHeight=48;
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var label=Label(image.transform,"Label",text,16); Place(label.rectTransform,.03f,0,.97f,1);
            button.onClick.AddListener(()=>action?.Invoke()); return button;
        }
        public void SetProgress(float value)
        { Gauge.gameObject.SetActive(true); Gauge.value=Mathf.Clamp01(value); Progress.text=value<0?L("準備中…","Preparing…"):$"{Mathf.FloorToInt(Gauge.value*100)}% / 100%"; }
        public void Close() { gameObject.SetActive(false); Destroy(gameObject); }
        public Slider AddSetting(string title,float value,float minimum,float maximum,float step,Action<float> changed)
        {
            var row=Box(actions,"Setting",YuiUiTheme.Field);YuiUiTheme.Round(row);
            row.gameObject.AddComponent<LayoutElement>().minHeight=56;
            var caption=Label(row.transform,"Label",title+" · "+value.ToString("0.##"),14);
            Place(caption.rectTransform,.04f,.35f,.96f,.95f);
            var rail=Box(row.transform,"Slider",YuiUiTheme.Muted);Place(rail.rectTransform,.05f,.14f,.95f,.28f);
            var fill=Box(rail.transform,"Fill",YuiUiTheme.Accent);Place(fill.rectTransform,0,0,1,1);
            var slider=row.gameObject.AddComponent<YuiScrollableSettingSlider>();slider.fillRect=fill.rectTransform;
            slider.minValue=minimum;slider.maxValue=maximum;slider.value=value;
            slider.onValueChanged.AddListener(v=> {
                var rounded=Mathf.Clamp(Mathf.Round(v/step)*step,minimum,maximum);
                caption.text=title+" · "+rounded.ToString("0.##");changed(rounded);
            });
            return slider;
        }
        public InputField AddInput(string title,string value,Action<string> changed)
        {
            var row=Box(actions,"PromptSetting",YuiUiTheme.Field);YuiUiTheme.Round(row);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight=180;
            var caption=Label(row.transform,"Label",title,14);Place(caption.rectTransform,.04f,.64f,.96f,.98f);
            var field=Box(row.transform,"Input",YuiUiTheme.Surface);Place(field.rectTransform,.04f,.04f,.96f,.61f);
            var text=Label(field.transform,"Text",value,15);Place(text.rectTransform,.03f,.03f,.97f,.97f);
            text.alignment=TextAnchor.UpperLeft;
            var input=field.gameObject.AddComponent<InputField>();input.textComponent=text;input.lineType=InputField.LineType.MultiLineNewline;
            input.characterLimit=2000;input.text=value;input.onValueChanged.AddListener(v=>changed(v));
            return input;
        }
        public void Compact(float height = 520)
        {
            compactHeight=height;
            var card = (RectTransform)Heading.transform.parent;
            card.anchorMin = card.anchorMax = new Vector2(.5f,.5f);
            card.pivot = new Vector2(.5f,.5f);
            var scale=Screen.width>Screen.height?Screen.height/844f:Screen.width/390f;
            card.sizeDelta = new Vector2(351, Mathf.Min(height, Screen.height/Mathf.Max(.01f,scale)*.90f));
            card.anchoredPosition = Vector2.zero;
            Place(Heading.rectTransform,.07f,.84f,.93f,.97f);
            Place((RectTransform)Body.transform.parent,.07f,.51f,.93f,.83f);
            ShowBodyScrollbar();
            ConfigureActionScroll(card,.04f,.48f);
        }
        private void ConfigureActionScroll(Transform card,float bottom,float top)
        {
            var existing=card.Find("ModelActionsViewport");
            var viewport=existing!=null?existing.gameObject:new GameObject("ModelActionsViewport",typeof(RectTransform),typeof(RectMask2D),typeof(Image),typeof(ScrollRect));
            if(existing==null)viewport.transform.SetParent(card,false);
            viewport.GetComponent<Image>().color=Color.clear;
            Place((RectTransform)viewport.transform,.07f,bottom,.83f,top);
            actions.SetParent(viewport.transform,false);
            var content=(RectTransform)actions;
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
            content.offsetMin=content.offsetMax=Vector2.zero;
            var layout=actions.GetComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight=false;
            var fitter=actions.GetComponent<ContentSizeFitter>() ?? actions.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.GetComponent<ScrollRect>();scroll.viewport=(RectTransform)viewport.transform;scroll.content=content;
            scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            var oldBar=card.Find("ActionsScrollbar");
            var bar=oldBar!=null?oldBar.GetComponent<Scrollbar>():CreateVerticalScrollbar(card,"ActionsScrollbar");
            Place((RectTransform)bar.transform,.85f,bottom,.98f,top);
            scroll.scrollSensitivity=24;
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }
        public void ShowBodyScrollbar()
        {
            var viewport=(RectTransform)Body.transform.parent;
            viewport.anchorMax=new Vector2(.83f,viewport.anchorMax.y);
            var existing=Heading.transform.parent.Find("BodyScrollbar");
            var bar=existing!=null?existing.GetComponent<Scrollbar>():CreateVerticalScrollbar(Heading.transform.parent,"BodyScrollbar");
            Place((RectTransform)bar.transform,.85f,viewport.anchorMin.y,.98f,viewport.anchorMax.y);
            var scroll=viewport.GetComponent<ScrollRect>();scroll.scrollSensitivity=24;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }
        internal static Scrollbar CreateVerticalScrollbar(Transform parent,string name)
        {
            var rail=Box(parent,name,Color.clear);
            var track=Box(rail.transform,"Track",YuiUiTheme.Muted);Place(track.rectTransform,.45f,0,.55f,1);track.raycastTarget=false;
            var handle=new GameObject("Handle",typeof(RectTransform)).GetComponent<RectTransform>();handle.SetParent(rail.transform,false);
            handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;
            var thumb=Box(handle,"Thumb",YuiUiTheme.Accent);Place(thumb.rectTransform,.38f,0,.62f,1);YuiUiTheme.Round(thumb);thumb.raycastTarget=false;
            var bar=rail.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=thumb;bar.direction=Scrollbar.Direction.BottomToTop;
            return bar;
        }
        public void AddCloseButton(Action action)
        {
            var image=Box(Heading.transform.parent,"Close",YuiUiTheme.Field);Place(image.rectTransform,.84f,.89f,.98f,.98f);YuiUiTheme.Round(image);
            var label=Label(image.transform,"Label","×",22);Place(label.rectTransform,0,0,1,1);
            image.gameObject.AddComponent<Button>().onClick.AddListener(()=>action?.Invoke());
            Heading.rectTransform.anchorMax=new Vector2(.82f,Heading.rectTransform.anchorMax.y);
        }
        private void Update()
        {
            if(safe==null)return;
            var scaler=GetComponent<CanvasScaler>();
            scaler.matchWidthOrHeight=Screen.width>Screen.height?1:0;
            var a=Screen.safeArea;
            var keyboard=TouchScreenKeyboard.visible?TouchScreenKeyboard.area.height:0;
            var bottom=Mathf.Max(a.yMin,keyboard);
            safe.anchorMin=new Vector2(a.xMin/Mathf.Max(1,Screen.width),bottom/Mathf.Max(1,Screen.height));
            safe.anchorMax=new Vector2(a.xMax/Mathf.Max(1,Screen.width),a.yMax/Mathf.Max(1,Screen.height));safe.offsetMin=safe.offsetMax=Vector2.zero;
            if(compactHeight>0)
            {
                var card=(RectTransform)Heading.transform.parent;
                var scale=Screen.width>Screen.height?Screen.height/844f:Screen.width/390f;
                card.sizeDelta=new Vector2(Mathf.Min(351,a.width/Mathf.Max(.01f,scale)*.9f),Mathf.Min(compactHeight,Mathf.Max(1,a.yMax-bottom)/Mathf.Max(.01f,scale)*.9f));
            }
        }
        private static Image Box(Transform parent,string name,Color color)
        { var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);var i=g.GetComponent<Image>();i.color=color;return i; }
        private static Text Label(Transform parent,string name,string value,int size)
        { var g=new GameObject(name,typeof(RectTransform),typeof(Text));g.transform.SetParent(parent,false);var t=g.GetComponent<Text>();t.text=value;t.font=YuiUiTypography.Regular;t.fontSize=size;t.color=YuiUiTheme.Text;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.supportRichText=false;return t; }
        private static void Place(RectTransform r,float x,float y,float xx,float yy)
        { r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(xx,yy);r.offsetMin=r.offsetMax=Vector2.zero; }
    }
}
