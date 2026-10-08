using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    public static class YuiControlAffordance
    {
        public static void MoveSettingsFocus(Transform scope,bool reverse)
        {
            var events=UnityEngine.EventSystems.EventSystem.current;
            var current=events?.currentSelectedGameObject;
            if(current==null || !current.transform.IsChildOf(scope)) return;
            var selectable=current.GetComponent<Selectable>();
            if(selectable==null) return;
            var next=reverse ? selectable.FindSelectableOnUp() : selectable.FindSelectableOnDown();
            if(next!=null && next.IsInteractable() && next.transform.IsChildOf(scope)) next.Select();
        }
        public static readonly Color InputSurface = new Color32(20,19,24,255);
        public static readonly Color OutlineColor = new Color32(121,115,133,255);

        public static void SelectionMark(Button button,bool selected)
        {
            var mark=EnsureImage(button.transform,"SelectionMark");
            mark.sprite=YuiToolbarIconUtility.LoadSymbol("check");mark.color=YuiUiTheme.Accent;mark.raycastTarget=false;mark.preserveAspect=true;
            var rect=mark.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(1,.5f);rect.sizeDelta=new Vector2(36,36);rect.anchoredPosition=new Vector2(-36,0);
            mark.gameObject.SetActive(selected);
        }

        public static void Input(InputField input)
        {
            if(input.targetGraphic is Image image) YuiUiTheme.SurfaceOn(image,InputSurface);
            var outline=input.GetComponent<Outline>() ?? input.gameObject.AddComponent<Outline>();
            outline.effectColor=OutlineColor;outline.effectDistance=new Vector2(2,-2);outline.useGraphicAlpha=false;
            input.customCaretColor=true;input.caretColor=YuiUiTheme.Accent;input.caretWidth=2;
            if(input.textComponent!=null) Inset(input.textComponent.rectTransform,22,22);
            if(input.placeholder==null && (input.name=="VoicePresetNameInput" || input.name=="CustomVrmNameInput"))
            {
                var go=new GameObject("Placeholder",typeof(RectTransform),typeof(Text));go.transform.SetParent(input.transform,false);
                var text=go.GetComponent<Text>();text.font=YuiUiTypography.Regular;text.fontSize=YuiUiTypography.Body;
                text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;input.placeholder=text;
            }
            if(input.placeholder is Text placeholder)
            {
                placeholder.color=YuiUiTheme.Muted;placeholder.fontStyle=FontStyle.Normal;
                Inset(placeholder.rectTransform,22,22);
                if(input.name=="VoicePresetNameInput") YuiUiLocalization.Set(placeholder,"e.g. Calm voice");
                else if(input.name=="CustomVrmNameInput") YuiUiLocalization.Set(placeholder,"e.g. Summer outfit");
            }
        }

        public static void DropdownArrow(Dropdown dropdown)
        {
            var old=dropdown.transform.Find("Arrow");if(old!=null) old.gameObject.SetActive(false);
            var image=EnsureImage(dropdown.transform,"SelectChevron");image.sprite=YuiToolbarIconUtility.LoadSymbol("expand_more");
            image.color=YuiUiTheme.Text;image.preserveAspect=true;image.raycastTarget=false;
            var rect=image.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(1,.5f);rect.sizeDelta=new Vector2(38,38);rect.anchoredPosition=new Vector2(-36,0);
            if(dropdown.captionText!=null) Inset(dropdown.captionText.rectTransform,24,72);
        }

        public static void Scrollbar(ScrollRect scroll)
        {
            if(scroll==null || scroll.viewport==null) return;
            var existing=scroll.transform.Find("ScrollPosition");
            var bar=existing!=null?existing.GetComponent<Scrollbar>():YuiSimpleDialog.CreateVerticalScrollbar(scroll.transform,"ScrollPosition");
            var r=(RectTransform)bar.transform;
            r.anchorMin=new Vector2(1,0);r.anchorMax=Vector2.one;r.pivot=new Vector2(1,.5f);
            r.offsetMin=new Vector2(-44,10);r.offsetMax=new Vector2(0,-10);
            bar.navigation=new Navigation {mode=Navigation.Mode.None};
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.scrollSensitivity=Mathf.Max(24,scroll.scrollSensitivity);
            // Some older lists use the ScrollRect itself as their viewport.
            // Reserve space in their content, without moving the entire list.
            if(scroll.viewport==scroll.transform)
                scroll.content.offsetMax=new Vector2(-44,scroll.content.offsetMax.y);
            else
                scroll.viewport.offsetMax=new Vector2(-44,scroll.viewport.offsetMax.y);
        }
        private static Image EnsureImage(Transform parent,string name)
        {
            var child=parent.Find(name);
            if(child==null) {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);child=go.transform;}
            return child.GetComponent<Image>();
        }
        private static void Inset(RectTransform rect,float left,float right)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(left,10);rect.offsetMax=new Vector2(-right,-10);}
    }
}
