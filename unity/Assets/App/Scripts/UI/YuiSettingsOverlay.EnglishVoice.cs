using UnityEngine;
using UnityEngine.UI;
namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiSettingsOverlay
    {
        private void EnglishVoiceSliders(Transform content, ref float row)
        {
            EnsureEnglishSlider(content, "EnglishSpeed", speedSlider, 0.5f, 2f, chatPanel?.EnglishSpeed ?? 1f);
            SliderRow(content, "EnglishSpeed", "Speed", ref row);
        }
        private void EnsureEnglishSlider(Transform content, string prefix, Slider template, float min, float max, float current)
        {
            var slider = content.Find(prefix + "Slider")?.GetComponent<Slider>();
            if (slider == null)
            {
                slider = Instantiate(template, content); slider.name = prefix + "Slider";
                slider.onValueChanged = new Slider.SliderEvent(); slider.minValue = min; slider.maxValue = max;
                ModernText(content, prefix + "Label", "Speed", YuiUiTypography.Label);
                var value = ModernText(content, prefix + "Value", "", YuiUiTypography.Note);
                value.alignment = TextAnchor.MiddleRight;
                slider.onValueChanged.AddListener(v => {
                    if (chatPanel != null) { chatPanel.EnglishSpeed = v; }
                    value.text = v.ToString("0.00") + "×";
                });
            }
            slider.SetValueWithoutNotify(current);
            content.Find(prefix + "Value").GetComponent<Text>().text = current.ToString("0.00") + "×";
        }
    }
}
