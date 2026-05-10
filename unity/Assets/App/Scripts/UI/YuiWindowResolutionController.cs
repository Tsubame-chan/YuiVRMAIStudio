using UnityEngine;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiWindowResolutionController : MonoBehaviour
    {
        public readonly struct ResolutionOption
        {
            public ResolutionOption(string label, int width, int height)
            {
                Label = label;
                Width = width;
                Height = height;
            }

            public string Label { get; }
            public int Width { get; }
            public int Height { get; }
        }

        public static readonly ResolutionOption[] Options =
        {
            new ResolutionOption("Portrait S 360 x 640", 360, 640),
            new ResolutionOption("Portrait M 576 x 1024", 576, 1024),
            new ResolutionOption("Portrait L 720 x 1280", 720, 1280),
            new ResolutionOption("Portrait FHD 1080 x 1920", 1080, 1920),
        };

        [SerializeField] private int presetIndex = 1;

        public int PresetIndex => Mathf.Clamp(presetIndex, 0, Options.Length - 1);

        private void Start()
        {
            presetIndex = Mathf.Clamp(PlayerPrefs.GetInt(YuiPrefsKeys.WindowResolutionPreset, presetIndex), 0, Options.Length - 1);
            ApplyPreset(presetIndex, false);
        }

        public void SetPreset(int index)
        {
            ApplyPreset(index, true);
        }

        private void ApplyPreset(int index, bool save)
        {
            presetIndex = Mathf.Clamp(index, 0, Options.Length - 1);
            var option = Options[presetIndex];
#if UNITY_STANDALONE
            Screen.SetResolution(option.Width, option.Height, FullScreenMode.Windowed);
#endif
            if (save)
            {
                PlayerPrefs.SetInt(YuiPrefsKeys.WindowResolutionPreset, presetIndex);
                PlayerPrefs.Save();
            }
        }
    }
}
