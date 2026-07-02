using System;
using System.Collections.Generic;
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
            new YuiTtsVoiceOption("櫻歌ミコ / 第二形態", 44),
            new YuiTtsVoiceOption("櫻歌ミコ / ロリ", 45),
            new YuiTtsVoiceOption("小夜/SAYO / ノーマル", 46),
            new YuiTtsVoiceOption("ナースロボ＿タイプＴ / ノーマル", 47),
            new YuiTtsVoiceOption("ナースロボ＿タイプＴ / 楽々", 48),
            new YuiTtsVoiceOption("九州そら / ノーマル", 16),
            new YuiTtsVoiceOption("九州そら / あまあま", 15),
            new YuiTtsVoiceOption("九州そら / セクシー", 17),
            new YuiTtsVoiceOption("四国めたん / ノーマル", 2),
            new YuiTtsVoiceOption("四国めたん / あまあま", 0),
            new YuiTtsVoiceOption("四国めたん / ツンツン", 6),
            new YuiTtsVoiceOption("四国めたん / セクシー", 4),
            new YuiTtsVoiceOption("四国めたん / ささやき", 36),
            new YuiTtsVoiceOption("四国めたん / ヒソヒソ", 37),
            new YuiTtsVoiceOption("ずんだもん / ノーマル", 3),
            new YuiTtsVoiceOption("ずんだもん / あまあま", 1),
            new YuiTtsVoiceOption("ずんだもん / ツンツン", 7),
            new YuiTtsVoiceOption("ずんだもん / セクシー", 5),
            new YuiTtsVoiceOption("ずんだもん / ささやき", 22),
            new YuiTtsVoiceOption("ずんだもん / ヒソヒソ", 38),
            new YuiTtsVoiceOption("ずんだもん / ヘロヘロ", 75),
            new YuiTtsVoiceOption("ずんだもん / なみだめ", 76),
        };

        private static readonly YuiTtsVoiceOption[] FallbackAivisVoiceOptions =
        {
            new YuiTtsVoiceOption("女性ボイス①", 1431611904),
        };

        public static IReadOnlyList<YuiTtsVoiceOption> VoicevoxOptions => VoicevoxVoiceOptions;
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
