using System;
using System.Collections.Generic;
using System.Linq;
using YuiPhysicalAI.LocalAI;
using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.UI
{
    public readonly struct YuiTtsVoiceOption
    {
        public YuiTtsVoiceOption(string label, int id)
        {
            Label = string.IsNullOrWhiteSpace(label) ? id.ToString() : label.Trim();
            Id = id;
        }

        public string Label { get; }
        public int Id { get; }
    }

    public static class YuiTtsVoiceOptionCatalog
    {
        private static readonly YuiTtsVoiceOption[] VoicevoxVoiceOptions =
        {
            new YuiTtsVoiceOption("冥鳴ひまり / ノーマル", 14),
            new YuiTtsVoiceOption("櫻歌ミコ / ノーマル", 43),
            new YuiTtsVoiceOption("小夜/SAYO / ノーマル", 46),
            new YuiTtsVoiceOption("ナースロボ＿タイプＴ / ノーマル", 47),
            new YuiTtsVoiceOption("九州そら / ノーマル", 16),
            new YuiTtsVoiceOption("四国めたん / ノーマル", 2),
            new YuiTtsVoiceOption("ずんだもん / ノーマル", 3),
        };

        private static readonly YuiTtsVoiceOption[] FallbackAivisVoiceOptions =
        {
            new YuiTtsVoiceOption("女性ボイス①", 1431611904),
        };

        // Persisted presets may still reference a variation hidden by the beta UI.
        // Preserve the character while selecting its standard delivery.
        public static int StandardVoicevoxId(int id)
        {
            switch (id)
            {
                case 44: case 45: return 43;
                case 48: case 49: case 50: return 47;
                case 15: case 17: case 18: case 19: return 16;
                case 0: case 4: case 6: case 36: case 37: return 2;
                case 1: case 5: case 7: case 22: case 38: case 75: case 76: return 3;
                default: return id;
            }
        }

        public static IReadOnlyList<YuiTtsVoiceOption> VoicevoxOptions => VoicevoxVoiceOptions;
        public static IReadOnlyList<YuiTtsVoiceOption> EmbeddedVoicevoxOptions => VoicevoxVoiceOptions.Where(v => YuiVoicevoxModelCatalog.IsAvailable(v.Id)).ToArray();
        public static IReadOnlyList<YuiTtsVoiceOption> FallbackAivisOptions => FallbackAivisVoiceOptions;

        public static IReadOnlyList<YuiTtsVoiceOption> OptionsForMode(
            string mode,
            IReadOnlyList<TtsVoiceOption> backendAivisOptions)
        {
            if (string.Equals(mode, "aivis-native", StringComparison.OrdinalIgnoreCase))
            {
                return FallbackAivisVoiceOptions;
            }

            return string.Equals(YuiTtsTuningPrefs.NormalizeMode(mode), "aivis", StringComparison.OrdinalIgnoreCase)
                ? AivisOptions(backendAivisOptions)
                : VoicevoxVoiceOptions;
        }

        public static IReadOnlyList<YuiTtsVoiceOption> AivisOptions(IReadOnlyList<TtsVoiceOption> backendOptions)
        {
            if (backendOptions == null || backendOptions.Count == 0)
            {
                return FallbackAivisVoiceOptions;
            }

            var result = new List<YuiTtsVoiceOption>(backendOptions.Count);
            foreach (var option in backendOptions)
            {
                if (option == null || option.Id <= 0)
                {
                    continue;
                }

                result.Add(new YuiTtsVoiceOption(
                    string.IsNullOrWhiteSpace(option.Label) ? option.Id.ToString() : option.Label,
                    option.Id));
            }

            return result.Count == 0 ? FallbackAivisVoiceOptions : result;
        }

        public static int VoiceIdAt(IReadOnlyList<YuiTtsVoiceOption> options, int index, int fallback)
        {
            return options != null && index >= 0 && index < options.Count ? options[index].Id : fallback;
        }

        public static int VoiceIndexForId(IReadOnlyList<YuiTtsVoiceOption> options, int speakerId)
        {
            if (options == null)
            {
                return 0;
            }

            for (var i = 0; i < options.Count; i++)
            {
                if (options[i].Id == speakerId)
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
