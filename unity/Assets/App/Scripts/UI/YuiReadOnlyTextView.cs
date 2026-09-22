using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    // Reading has its own scroll surface: an InputField is for editing and does
    // not provide ordinary wheel/touch scrolling for long archived responses.
    [RequireComponent(typeof(ScrollRect))]
    public sealed class YuiReadOnlyTextView : MonoBehaviour
    {
        private readonly List<Text> blocks = new List<Text>();
        private RectTransform content;
        private ScrollRect scroll;
        private float lastWidth = -1;

        public void Configure(string value)
        {
            scroll = GetComponent<ScrollRect>();
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(transform, false);
            var viewRect = viewport.GetComponent<RectTransform>();
            viewRect.anchorMin=Vector2.zero;viewRect.anchorMax=Vector2.one;
            viewRect.offsetMin=new Vector2(18,16);viewRect.offsetMax=new Vector2(-18,-16);
            var body = new GameObject("Content", typeof(RectTransform));body.transform.SetParent(viewport.transform,false);
            content=body.GetComponent<RectTransform>();content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;
            content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
            foreach(var chunk in Split(value))
            {
                var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(content,false);
                var text=go.GetComponent<Text>();text.font=YuiUiTypography.Regular;text.fontSize=YuiUiTypography.Body;
                text.color=YuiUiTheme.Text;text.supportRichText=false;text.text=chunk;text.alignment=TextAnchor.UpperLeft;
                text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;
                blocks.Add(text);
            }
            scroll.viewport=viewRect;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=45;
            YuiControlAffordance.Scrollbar(scroll);
            Canvas.ForceUpdateCanvases();Layout();scroll.verticalNormalizedPosition=1;
        }

        public static IEnumerable<string> Split(string value)
        {
            value=value??"";
            for(var start=0;start<value.Length;)
            {
                var count=Mathf.Min(4096,value.Length-start);
                if(start+count<value.Length)
                {
                    var newline=value.LastIndexOf('\n',start+count-1,count/2);
                    if(newline>=start+count/2)count=newline-start+1;
                    else if(char.IsHighSurrogate(value[start+count-1]))count--;
                }
                yield return value.Substring(start,count);start+=count;
            }
        }

        private void LateUpdate() { if(scroll!=null && !Mathf.Approximately(lastWidth,scroll.viewport.rect.width)) Layout(); }
        private void Layout()
        {
            lastWidth=scroll.viewport.rect.width;var width=Mathf.Max(20,lastWidth);var top=0f;
            foreach(var text in blocks)
            {
                var height=Mathf.Max(YuiUiTypography.Body,text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,text.GetGenerationSettings(new Vector2(width,0)))/text.pixelsPerUnit);
                var rect=text.rectTransform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);
                rect.anchoredPosition=new Vector2(0,-top);rect.sizeDelta=new Vector2(0,height);top+=height;
            }
            content.sizeDelta=new Vector2(0,top+12);
        }
    }
}
