using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    public static class YuiToolbarIconUtility
    {
        private const string SettingsIconPath = "YuiToolbarIcons/settings";
        private const string SecretIconPath = "YuiToolbarIcons/secret";
        private const string HelpIconPath = "YuiToolbarIcons/help";

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        public static void ApplySettingsIcon(Button button)
        {
            ApplyIcon(button, SettingsIconPath, 8f);
        }

        public static void ApplySecretIcon(Button button)
        {
            ApplyIcon(button, SecretIconPath, 7f);
        }

        public static void ApplyHelpIcon(Button button)
        {
            ApplyIcon(button, HelpIconPath, 7f);
        }

        private static void ApplyIcon(Button button, string resourcePath, float padding)
        {
            if (button == null)
            {
                return;
            }

            var sprite = LoadSprite(resourcePath);
            if (sprite == null)
            {
                return;
            }

            HideTextLabels(button.transform);

            var icon = EnsureIconImage(button.transform);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = Color.white;

            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(padding, padding);
            iconRect.offsetMax = new Vector2(-padding, -padding);
            icon.transform.SetAsLastSibling();
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            if (SpriteCache.TryGetValue(resourcePath, out var sprite))
            {
                return sprite;
            }

            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogWarning($"Yui toolbar icon not found: Resources/{resourcePath}");
                return null;
            }

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            SpriteCache[resourcePath] = sprite;
            return sprite;
        }

        private static Image EnsureIconImage(Transform parent)
        {
            var iconTransform = parent.Find("Icon");
            if (iconTransform != null && iconTransform.TryGetComponent<Image>(out var existing))
            {
                return existing;
            }

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(parent, false);
            return iconObject.GetComponent<Image>();
        }

        private static void HideTextLabels(Transform parent)
        {
            var labels = parent.GetComponentsInChildren<Text>(true);
            foreach (var label in labels)
            {
                label.gameObject.SetActive(false);
            }
        }
    }
}
