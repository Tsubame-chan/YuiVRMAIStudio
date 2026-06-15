using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    public static class YuiChatLogStyle
    {
        public const float BubbleMaxWidthRatio = 0.78f;
        public const float BubbleMinWidth = 180f;
        public const float BubbleFallbackWidth = 360f;

        public static readonly Color SystemBackground = new Color(0.18f, 0.19f, 0.22f, 0.96f);
        public static readonly Color UserBackground = new Color(0.23f, 0.33f, 0.78f, 0.96f);
        public static readonly Color AssistantBackground = new Color(0.16f, 0.18f, 0.28f, 0.96f);
        public static readonly Color SystemSpeaker = new Color(1f, 0.82f, 0.44f, 1f);
        public static readonly Color Speaker = new Color(0.82f, 0.86f, 1f, 1f);
        public static readonly Color Body = new Color(0.94f, 0.95f, 1f, 1f);

        public static Font ResolveFont(Text source)
        {
            if (source != null && source.font != null)
            {
                return source.font;
            }

            return Font.CreateDynamicFontFromOSFont(new[] { "Hiragino Sans", "Yu Gothic", "Meiryo", "Arial" }, 13)
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

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
