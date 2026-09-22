using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    // Shared by native Unity surfaces. No WebView or per-frame texture generation.
    public static class YuiUiTheme
    {
        public static readonly Color Surface = new Color32(28, 27, 32, 248);
        public static readonly Color Field = new Color32(43, 41, 49, 255);
        public static readonly Color Selected = new Color32(70, 59, 88, 255);
        public static readonly Color Accent = new Color32(208, 188, 255, 255);
        public static readonly Color Text = new Color32(235, 230, 240, 255);
        public static readonly Color Muted = new Color32(185, 180, 194, 255);
        private static Sprite rounded;
        public static void Round(Image image)
        {
            if (image == null) return;
            if (rounded == null) rounded = YuiChatLogStyle.CreateRoundedBubbleSprite();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
        }
        public static void SurfaceOn(Image image, Color color)
        { if (image != null) { Round(image); image.color = color; } }
        public static void ButtonStyle(Button button, bool primary = false)
        {
            if (button == null) return;
            SurfaceOn(button.targetGraphic as Image, primary ? Accent : Field);
            var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(.88f,.85f,.95f);
            colors.pressedColor = new Color(.7f,.65f,.8f); colors.selectedColor = new Color(.92f,.88f,1f);
            button.colors = colors;
            foreach (var text in button.GetComponentsInChildren<Text>(true))
            { text.font = YuiUiTypography.Regular; text.color = primary ? new Color32(45,32,65,255) : Text; text.fontSize = YuiUiTypography.Button; text.resizeTextForBestFit = false; }
        }
    }
}
