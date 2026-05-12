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

        private void Awake()
        {
            var savedPresetIndex = Mathf.Clamp(PlayerPrefs.GetInt(YuiPrefsKeys.WindowResolutionPreset, presetIndex), 0, Options.Length - 1);
            presetIndex = ResolvePresetForCurrentDisplay(savedPresetIndex);
            if (presetIndex != savedPresetIndex)
            {
                Debug.LogWarning(
                    $"Yui window resolution preset {savedPresetIndex} does not fit this display; using preset {presetIndex}.");
                PlayerPrefs.SetInt(YuiPrefsKeys.WindowResolutionPreset, presetIndex);
                PlayerPrefs.Save();
            }

            ApplyPreset(presetIndex, false);
        }

        public void SetPreset(int index)
        {
            ApplyPreset(index, true);
        }

        private void ApplyPreset(int index, bool save)
        {
            presetIndex = ResolvePresetForCurrentDisplay(index);
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

        private static int ResolvePresetForCurrentDisplay(int requestedIndex)
        {
            var index = Mathf.Clamp(requestedIndex, 0, Options.Length - 1);
            var maxWidth = Mathf.Max(360, GetDisplayWidth() - 80);
            var maxHeight = Mathf.Max(640, GetDisplayHeight() - 120);

            for (var i = index; i >= 0; i--)
            {
                var option = Options[i];
                if (option.Width <= maxWidth && option.Height <= maxHeight)
                {
                    return i;
                }
            }

            return 0;
        }

        private static int GetDisplayWidth()
        {
            if (Display.main != null && Display.main.systemWidth > 0)
            {
                return Display.main.systemWidth;
            }

            return Screen.currentResolution.width > 0 ? Screen.currentResolution.width : Screen.width;
        }

        private static int GetDisplayHeight()
        {
            if (Display.main != null && Display.main.systemHeight > 0)
            {
                return Display.main.systemHeight;
            }

            return Screen.currentResolution.height > 0 ? Screen.currentResolution.height : Screen.height;
        }
    }
}
