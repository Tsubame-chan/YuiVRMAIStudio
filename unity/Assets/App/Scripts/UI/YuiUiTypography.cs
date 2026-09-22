using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    // Main-thread UI initialization only. Embedded font data removes OS font dependencies.
    public static class YuiUiTypography
    {
        // Logical pixels at the main Canvas reference width (1080). CanvasScaler
        // handles window/device scaling; do not shrink long prose to fit a box.
        public const int Title = 42;
        public const int Heading = 34;
        public const int Body = 30;
        public const int Label = 28;
        public const int Button = 28;
        public const int Note = 28;
        public const int Caption = 24;
        public static int Tab => YuiUiLocalization.Language == "ja" ? 26 : 28;
        public static int AtReferenceWidth(int size, float referenceWidth) => Mathf.RoundToInt(size * referenceWidth / 1080f);
        private static Font regular;
        public static Font Regular => regular != null ? regular :
            (regular = Resources.Load<Font>("YuiFonts/NotoSansJP-Regular"));
        public static string JapaneseParagraph(string source)
        {
            // Legacy uGUI prefers an early Latin space over a later CJK boundary,
            // leaving headings such as "OpenAI" alone on a line. Non-breaking
            // spaces let its character wrapping fill the line instead. This is
            // display-only app copy, never user text, code or URLs.
            if (string.IsNullOrEmpty(source) || YuiUiLocalization.Language != "ja") return source;
            return source.Replace(' ', '\u00a0');
        }
        public static void Apply(Transform root)
        {
            if (root == null || Regular == null) return;
            foreach (var text in root.GetComponentsInChildren<Text>(true)) text.font = Regular;
        }
    }
}
