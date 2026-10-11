using System;
using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    // Only the small banner receives touches; the rest of the app stays interactive.
    public sealed class YuiActivityBanner : MonoBehaviour
    {
        private RectTransform safe;
        private Text label;
        private Image fill;
        private Button action;
        private Text actionLabel;
        public static YuiActivityBanner Create(string message, bool avatar = false)
        {
            var root = new GameObject("YuiActivityBanner", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32600;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390,844); scaler.matchWidthOrHeight = 0;
            var view = root.AddComponent<YuiActivityBanner>();
            view.safe = new GameObject("Safe",typeof(RectTransform)).GetComponent<RectTransform>(); view.safe.SetParent(root.transform,false);
            var card = new GameObject("Banner",typeof(RectTransform),typeof(Image)); card.transform.SetParent(view.safe,false);
            var rect = (RectTransform)card.transform; rect.anchorMin = new Vector2(0,0); rect.anchorMax = new Vector2(1,0); rect.pivot = new Vector2(.5f,0);
            rect.offsetMin = new Vector2(8,avatar?156:100); rect.offsetMax = new Vector2(-8,avatar?208:152);
            card.GetComponent<Image>().color = new Color32(43,39,55,250); YuiUiTheme.Round(card.GetComponent<Image>());
            view.label = TextAt(card.transform,"Status",message,14,new Vector2(.04f,.12f),new Vector2(.78f,.98f));
            view.label.alignment = TextAnchor.MiddleLeft;
            view.actionLabel = TextAt(card.transform,"Action","",14,new Vector2(.78f,.12f),new Vector2(.98f,.98f));
            view.actionLabel.raycastTarget = true; view.action = view.actionLabel.gameObject.AddComponent<Button>(); view.action.targetGraphic = view.actionLabel;
            var rail = new GameObject("Progress",typeof(RectTransform),typeof(Image)); rail.transform.SetParent(card.transform,false);
            view.fill = rail.GetComponent<Image>(); view.fill.color = YuiUiTheme.Accent;
            var r = view.fill.rectTransform; r.anchorMin = new Vector2(.04f,.06f); r.anchorMax = new Vector2(.04f,.10f); r.offsetMin=r.offsetMax=Vector2.zero;
            view.Update(); return view;
        }
        public void Set(string message, float progress = -1)
        { label.text=message; fill.rectTransform.anchorMax = new Vector2(.04f+.92f*Mathf.Clamp01(progress),.10f); }
        public void SetAction(string caption, Action callback)
        { actionLabel.text=caption; action.onClick.RemoveAllListeners(); action.onClick.AddListener(()=>callback?.Invoke()); }
        public void Close() { gameObject.SetActive(false); Destroy(gameObject); }
        public void SetVerticalOffset(float bottom)
        {
            var rect=(RectTransform)label.transform.parent;
            rect.offsetMin=new Vector2(8,bottom);rect.offsetMax=new Vector2(-8,bottom+52);
        }
        public void PlaceAboveOtherBanners()
        {
            var bottom=100f;
            var others=UnityEngine.Object.FindObjectsByType<YuiActivityBanner>(FindObjectsSortMode.None);
            bool occupied;
            do
            {
                occupied=false;
                foreach(var other in others)
                {
                    if(other==this || !other.gameObject.activeInHierarchy || other.label==null)continue;
                    var rect=(RectTransform)other.label.transform.parent;
                    if(Mathf.Abs(rect.offsetMin.y-bottom)<54f){occupied=true;bottom+=56f;break;}
                }
            } while(occupied);
            SetVerticalOffset(bottom);
        }
        private void Update()
        { if(safe==null)return; var a=Screen.safeArea; safe.anchorMin=new Vector2(a.xMin/Mathf.Max(1,Screen.width),a.yMin/Mathf.Max(1,Screen.height)); safe.anchorMax=new Vector2(a.xMax/Mathf.Max(1,Screen.width),a.yMax/Mathf.Max(1,Screen.height)); safe.offsetMin=safe.offsetMax=Vector2.zero; }
        private static Text TextAt(Transform parent,string name,string message,int size,Vector2 min,Vector2 max)
        { var g=new GameObject(name,typeof(RectTransform),typeof(Text)); g.transform.SetParent(parent,false); var t=g.GetComponent<Text>(); t.font=YuiUiTypography.Regular;t.fontSize=size;t.color=YuiUiTheme.Text;t.text=message;t.supportRichText=false;t.raycastTarget=false;t.alignment=TextAnchor.MiddleCenter;t.rectTransform.anchorMin=min;t.rectTransform.anchorMax=max;t.rectTransform.offsetMin=t.rectTransform.offsetMax=Vector2.zero;return t; }
    }
}
