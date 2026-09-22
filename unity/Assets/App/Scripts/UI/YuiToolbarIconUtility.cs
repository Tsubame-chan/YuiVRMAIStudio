using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    public static class YuiToolbarIconUtility
    {
        private const string SettingsIconPath = "YuiSymbols/settings";
        private const string SecretIconPath = "YuiSymbols/visibility_off";
        private const string HelpIconPath = "YuiSymbols/help";

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        public static void ApplySettingsIcon(Button button)
        {
            ApplyFloatingIcon(button, SettingsIconPath, 0);
        }

        public static void ApplySecretIcon(Button button)
        {
            ApplyFloatingIcon(button, SecretIconPath, 1);
        }

        public static void ApplyHelpIcon(Button button)
        {
            ApplyFloatingIcon(button, HelpIconPath, 2);
        }

        public static void ApplyCloseIcon(Button button) => ApplyIcon(button, "YuiSymbols/close", 22f);
        public static void ApplyAttachmentIcon(Button button)
        {
            ApplyIcon(button, "YuiSymbols/attach_file", 22f);
            var old=button != null ? button.transform.Find("Paperclip") : null;
            if(old!=null) old.gameObject.SetActive(false);
        }

        private static void ApplyFloatingIcon(Button button, string path, int index)
        {
            ApplyIcon(button, path, 24f);
            if (button == null) return;
            // Larger targets than the icon itself, with a consistent gap between actions.
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(112,112);
            rect.anchoredPosition = new Vector2(-32,-40-index*128);
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

            YuiUiTheme.ButtonStyle(button);
            HideTextLabels(button.transform);

            var icon = EnsureIconImage(button.transform);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = YuiUiTheme.Text;

            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(.5f,.5f);
            iconRect.sizeDelta = new Vector2(48,48);
            iconRect.anchoredPosition = Vector2.zero;
            icon.transform.SetAsLastSibling();
        }

        public static Sprite LoadSymbol(string name) => LoadSprite("YuiSymbols/" + name);

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
