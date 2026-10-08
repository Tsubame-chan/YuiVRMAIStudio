using UnityEngine;

namespace YuiPhysicalAI.UI
{
    // Visible dimensions are independent of the transparent drag target and Canvas units.
    public sealed class YuiScrollbarVisual : MonoBehaviour
    {
        private void LateUpdate()
        {
            var canvas=GetComponentInParent<Canvas>();
            if(canvas==null)return;
            var displayScale=Screen.width>Screen.height ? Screen.height/844f : Screen.width/390f;
            var units=displayScale/Mathf.Max(.01f,canvas.rootCanvas.scaleFactor);
            SetWidth(transform.Find("Track") as RectTransform,1.6f*units);
            SetWidth(transform.Find("Handle/Thumb") as RectTransform,3.8f*units);
        }
        private static void SetWidth(RectTransform rect,float width)
        {
            if(rect==null)return;
            rect.anchorMin=new Vector2(.5f,0);rect.anchorMax=new Vector2(.5f,1);
            rect.offsetMin=new Vector2(-width*.5f,0);rect.offsetMax=new Vector2(width*.5f,0);
        }
    }
}
