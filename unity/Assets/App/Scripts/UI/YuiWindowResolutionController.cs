using UnityEngine;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiWindowResolutionController : MonoBehaviour
    {
        private const int DefaultPresetIndex = 0;
        private const int PresetListVersion = 2;
        private const int MinimumAntiAliasing = 4;
        private const float PortraitAspect = 9f / 16f;
        private const float StandaloneDisplayPadding = 0.96f;

        public readonly struct ResolutionOption
        {
            public ResolutionOption(string label, int width, int height, bool responsive = false)
            {
                Label = label;
                Width = width;
                Height = height;
                Responsive = responsive;
            }

            public string Label { get; }
            public int Width { get; }
            public int Height { get; }
            public bool Responsive { get; }
        }

        public static readonly ResolutionOption[] Options =
        {
            new ResolutionOption("Auto Fit 9:16", 1440, 2560, true),
            new ResolutionOption("Portrait S 360 x 640", 360, 640),
            new ResolutionOption("Portrait M 576 x 1024", 576, 1024),
            new ResolutionOption("Portrait L 720 x 1280", 720, 1280),
            new ResolutionOption("Portrait XL 900 x 1600", 900, 1600),
            new ResolutionOption("Portrait FHD 1080 x 1920", 1080, 1920),
            new ResolutionOption("Portrait Retina Fit 972 x 1728", 972, 1728),
            new ResolutionOption("Portrait 2.5K 1440 x 2560", 1440, 2560),
            new ResolutionOption("Portrait 4K 2160 x 3840", 2160, 3840),
        };

        [SerializeField] private int presetIndex = DefaultPresetIndex;

        public int PresetIndex => Mathf.Clamp(presetIndex, 0, Options.Length - 1);

        private void Start()
        {
            ApplyDisplayQualityDefaults();
            presetIndex = Mathf.Clamp(LoadPresetIndex(), 0, Options.Length - 1);
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
            var size = ResolveWindowSize(option);
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
#endif
            if (save)
            {
                PlayerPrefs.SetInt(YuiPrefsKeys.WindowResolutionPreset, presetIndex);
                PlayerPrefs.Save();
            }
        }

        private static int LoadPresetIndex()
        {
            var storedPreset = PlayerPrefs.GetInt(YuiPrefsKeys.WindowResolutionPreset, DefaultPresetIndex);
            if (!PlayerPrefs.HasKey(YuiPrefsKeys.WindowResolutionPreset))
            {
                PlayerPrefs.SetInt(YuiPrefsKeys.WindowResolutionPreset, storedPreset);
                PlayerPrefs.SetInt(YuiPrefsKeys.WindowResolutionPresetListVersion, PresetListVersion);
                PlayerPrefs.Save();
                return storedPreset;
            }

            if (PlayerPrefs.GetInt(YuiPrefsKeys.WindowResolutionPresetListVersion, 1) < PresetListVersion)
            {
                storedPreset = UpgradeLegacyPresetIndex(storedPreset);
                PlayerPrefs.SetInt(YuiPrefsKeys.WindowResolutionPreset, storedPreset);
                PlayerPrefs.SetInt(YuiPrefsKeys.WindowResolutionPresetListVersion, PresetListVersion);
                PlayerPrefs.Save();
            }

            if (PlayerPrefs.GetInt(YuiPrefsKeys.WindowResolutionPresetDefaultUpgraded, 0) == 0 && storedPreset < DefaultPresetIndex)
            {
                storedPreset = DefaultPresetIndex;
                PlayerPrefs.SetInt(YuiPrefsKeys.WindowResolutionPreset, storedPreset);
                PlayerPrefs.SetInt(YuiPrefsKeys.WindowResolutionPresetDefaultUpgraded, 1);
                PlayerPrefs.Save();
            }

            return storedPreset;
        }

        private static int UpgradeLegacyPresetIndex(int legacyIndex)
        {
            switch (legacyIndex)
            {
                case 0:
                    return 1;
                case 1:
                    return 2;
                case 2:
                    return 3;
                case 3:
                    return 5;
                case 4:
                    return 7;
                default:
                    return DefaultPresetIndex;
            }
        }

        private static Vector2Int ResolveWindowSize(ResolutionOption option)
        {
            var width = Mathf.Max(360, option.Width);
            var height = Mathf.Max(640, option.Height);
            var aspect = width > 0 && height > 0
                ? (float)width / height
                : PortraitAspect;

            var display = Screen.currentResolution;
            var maxWidth = Mathf.FloorToInt(display.width * StandaloneDisplayPadding);
            var maxHeight = Mathf.FloorToInt(display.height * StandaloneDisplayPadding);
            if (maxWidth <= 0 || maxHeight <= 0)
            {
                return new Vector2Int(width, height);
            }

            if (height > maxHeight)
            {
                height = maxHeight;
                width = Mathf.RoundToInt(height * aspect);
            }

            if (width > maxWidth)
            {
                width = maxWidth;
                height = Mathf.RoundToInt(width / aspect);
            }

            return new Vector2Int(Mathf.Max(360, width), Mathf.Max(640, height));
        }

        private static void ApplyDisplayQualityDefaults()
        {
            if (QualitySettings.names.Length > 0)
            {
                QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            }

            QualitySettings.antiAliasing = Mathf.Max(QualitySettings.antiAliasing, MinimumAntiAliasing);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, 2f);
        }
    }
}
