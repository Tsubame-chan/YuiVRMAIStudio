using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public static class YuiPopupPlacement
    {
        // Canvas-local coordinates, independent of DPI and the current Talk/Work height.
        public static Rect Above(Rect bounds, Rect anchor, Vector2 desiredSize, float margin = 16)
        {
            var width = Mathf.Min(desiredSize.x, Mathf.Max(0, bounds.width - margin * 2));
            var height = Mathf.Min(desiredSize.y, Mathf.Max(0, bounds.height - margin * 2));
            var x = Mathf.Clamp(anchor.xMin, bounds.xMin + margin, bounds.xMax - margin - width);
            var y = anchor.yMax + 12;
            if (y + height > bounds.yMax - margin) y = anchor.yMin - 12 - height;
            y = Mathf.Clamp(y, bounds.yMin + margin, bounds.yMax - margin - height);
            return new Rect(x, y, width, height);
        }
    }
}
