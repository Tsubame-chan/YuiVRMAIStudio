using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    public static class YuiChatLogStyle
    {
        public const float BubbleMaxWidthRatio = 0.84f;
        public const float BubbleMinWidth = 180f;
        public const float BubbleFallbackWidth = 360f;
        public const int SpeakerFontSize = YuiUiTypography.Caption;
        public const int BodyFontSize = YuiUiTypography.Body;
        public const int ActionFontSize = YuiUiTypography.Caption;

        public static readonly Color SystemBackground = YuiUiTheme.Field;
        public static readonly Color UserBackground = YuiUiTheme.Selected;
        public static readonly Color AssistantBackground = new Color32(38,36,44,255);
        public static readonly Color SystemSpeaker = new Color(1f, 0.82f, 0.44f, 1f);
        public static readonly Color Speaker = YuiUiTheme.Accent;
        public static readonly Color Body = YuiUiTheme.Text;

        public static Font ResolveFont(Text source) => YuiUiTypography.Regular;

        public static Sprite CreateRoundedBubbleSprite()
        {
            const int size = 48;
            const int radius = 18;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "YuiGeneratedRoundedBubble",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x < radius ? radius - x : x >= size - radius ? x - (size - radius - 1) : 0;
                    var dy = y < radius ? radius - y : y >= size - radius ? y - (size - radius - 1) : 0;
                    var inside = dx == 0 && dy == 0 || dx * dx + dy * dy <= radius * radius;
                    pixels[y * size + x] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }
    }
}
