using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiScrollIntoView : MonoBehaviour, ISelectHandler
    {
        public void OnSelect(BaseEventData eventData)
        {
            var scroll=GetComponentInParent<ScrollRect>();
            if(scroll==null || scroll.viewport==null || scroll.content==null) return;
            Canvas.ForceUpdateCanvases();
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,(RectTransform)transform);
            var view=scroll.viewport.rect;
            float offset=bounds.max.y>view.yMax ? bounds.max.y-view.yMax : bounds.min.y<view.yMin ? bounds.min.y-view.yMin : 0;
            var position=scroll.content.anchoredPosition;
            position.y=Mathf.Clamp(position.y-offset,0,Mathf.Max(0,scroll.content.rect.height-view.height));
            scroll.StopMovement();scroll.content.anchoredPosition=position;
        }
    }
}
