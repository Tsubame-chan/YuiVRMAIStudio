using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    // A vertical gesture belongs to the dialog; only horizontal drags change the value.
    public sealed class YuiScrollableSettingSlider : Slider, IBeginDragHandler, IEndDragHandler
    {
        private ScrollRect parentScroll;
        private bool dragging, scrolling;
        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            dragging = scrolling = false;
            parentScroll = GetComponentInParent<ScrollRect>();
            Select();
        }
        public override void OnInitializePotentialDrag(PointerEventData eventData) { eventData.useDragThreshold = true; }
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;
            dragging = true;
            var delta = eventData.position - eventData.pressPosition;
            scrolling = parentScroll != null && Mathf.Abs(delta.y) > Mathf.Abs(delta.x);
            if (scrolling) { parentScroll.OnInitializePotentialDrag(eventData); parentScroll.OnBeginDrag(eventData); }
        }
        public override void OnDrag(PointerEventData eventData)
        {
            if (scrolling) parentScroll.OnDrag(eventData);
            else base.OnDrag(eventData);
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (scrolling && parentScroll != null) parentScroll.OnEndDrag(eventData);
            dragging = scrolling = false;
        }
        public override void OnPointerUp(PointerEventData eventData)
        {
            if (!dragging) base.OnPointerDown(eventData);
            base.OnPointerUp(eventData);
        }
    }
}
