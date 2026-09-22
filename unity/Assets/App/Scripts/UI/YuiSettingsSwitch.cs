using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    // Presentation only: the existing Toggle owns the value and Save owns persistence.
    [RequireComponent(typeof(Toggle))]
    public sealed class YuiSettingsSwitch : MonoBehaviour
    {
        private Toggle toggle;
        private Image track;
        private Image thumb;

        public void Configure()
        {
            toggle = GetComponent<Toggle>();
            var root = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            YuiUiTheme.SurfaceOn(root,YuiUiTheme.Field);
            toggle.targetGraphic = root;
            toggle.graphic = null;
            var old = transform.Find("Background");
            if (old != null) old.gameObject.SetActive(false);
            track = ImageAt("SwitchTrack",transform);
            YuiUiTheme.Round(track);
            var r = track.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(1,.5f);
            r.pivot = new Vector2(1,.5f); r.sizeDelta = new Vector2(104,56); r.anchoredPosition = new Vector2(-20,0);
            thumb = ImageAt("Thumb",track.transform); YuiUiTheme.Round(thumb);
            thumb.rectTransform.sizeDelta = new Vector2(44,44);
            var label = transform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(24,6); label.rectTransform.offsetMax = new Vector2(-150,-6);
                label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
                YuiUiLocalization.Set(label,"Continue with on-device AI",true);
            }
            toggle.onValueChanged.RemoveListener(OnValueChanged);
            toggle.onValueChanged.AddListener(OnValueChanged);
            Refresh();
        }
        private static Image ImageAt(string name,Transform parent)
        {
            var child=parent.Find(name);
            if(child==null) { var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);child=go.transform; }
            var image=child.GetComponent<Image>();image.raycastTarget=false;return image;
        }
        private void OnEnable() { if(toggle!=null) Refresh(); }
        private void OnValueChanged(bool value) => Refresh();
        public void Refresh()
        {
            if(track==null || thumb==null || toggle==null) return;
            track.color = toggle.isOn ? YuiUiTheme.Accent : new Color32(128,122,140,255);
            thumb.color = toggle.isOn ? new Color32(46,34,64,255) : YuiUiTheme.Text;
            var rect=thumb.rectTransform;
            rect.anchorMin=rect.anchorMax=new Vector2(toggle.isOn ? 1 : 0,.5f);
            rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(toggle.isOn ? -28 : 28,0);
        }
    }
}
