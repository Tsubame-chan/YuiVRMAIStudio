using UnityEngine;
using UnityEngine.UI;
namespace YuiPhysicalAI.UI
{
    [DisallowMultipleComponent]
    public sealed class YuiLocalizedLabel : MonoBehaviour
    {
        private Text label;
        private string source;
        private string appliedLanguage;
        private bool paragraph;
        public void SetSource(string value, bool japaneseParagraph = false)
        {
            if (source == value && paragraph == japaneseParagraph && appliedLanguage == YuiUiLocalization.Language && label != null && label.font == YuiUiTypography.Regular) return;
            source = value; paragraph = japaneseParagraph; Refresh();
        }
        private void OnEnable() { YuiUiLocalization.Changed -= Refresh; YuiUiLocalization.Changed += Refresh; Refresh(); }
        private void OnDisable() { YuiUiLocalization.Changed -= Refresh; }
        private void Refresh()
        {
            if (source == null) return;
            if (label == null) label = GetComponent<Text>();
            label.font = YuiUiTypography.Regular;
            appliedLanguage = YuiUiLocalization.Language;
            var translated = YuiUiLocalization.Text(source);
            label.text = paragraph ? YuiUiTypography.JapaneseParagraph(translated) : translated;
        }
    }
}
