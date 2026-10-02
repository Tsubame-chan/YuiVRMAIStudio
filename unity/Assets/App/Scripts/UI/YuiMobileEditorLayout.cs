using UnityEngine;
namespace YuiPhysicalAI.UI
{
    public sealed class YuiMobileEditorLayout : MonoBehaviour
    {
        private RectTransform rect;private Vector2 original;
        private void Awake(){rect=(RectTransform)transform;original=rect.offsetMin;}
        private void LateUpdate()
        {
            if(!Application.isMobilePlatform)return;
            var canvas=GetComponentInParent<Canvas>();
            var height=TouchScreenKeyboard.visible?TouchScreenKeyboard.area.height:0;
            rect.offsetMin=new Vector2(original.x,original.y+height/Mathf.Max(.01f,canvas!=null?canvas.scaleFactor:1));
        }
        private void OnDisable(){if(rect!=null)rect.offsetMin=original;}
    }
}
