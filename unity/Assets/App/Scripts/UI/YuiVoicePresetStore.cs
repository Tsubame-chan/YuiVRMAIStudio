using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    [Serializable]
    public sealed class YuiVoicePreset
    {
        public string Name;
        public string TtsMode;
        public int SpeakerId;
        public float VoiceVolume;
        public float SpeedScale;
        public float PitchScale;
        public float IntonationScale;
        public float SynthesisVolumeScale;
        public float PrePhonemeLength;
        public float PostPhonemeLength;
        public string IrodoriVoiceGender;
        public string IrodoriVoiceInstruct;
    }

    public static class YuiVoicePresetStore
    {
        public static List<YuiVoicePreset> Load()
        {
            var json = PlayerPrefs.GetString(YuiPrefsKeys.VoicePresetLibrary, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<YuiVoicePreset>();
            }

            try
            {
                return JsonConvert.DeserializeObject<List<YuiVoicePreset>>(json) ?? new List<YuiVoicePreset>();
            }
            catch (JsonException)
            {
                return new List<YuiVoicePreset>();
            }
        }

        public static void Upsert(YuiVoicePreset preset)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.Name))
            {
                return;
            }

            preset.Name = preset.Name.Trim();
            var presets = Load();
            var index = presets.FindIndex(item => string.Equals(item.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                presets[index] = preset;
            }
            else
            {
                presets.Add(preset);
            }

            Save(presets);
        }

        public static void Delete(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var presets = Load();
            presets.RemoveAll(item => string.Equals(item.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            Save(presets);
        }

        private static void Save(List<YuiVoicePreset> presets)
        {
            presets.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase));
            PlayerPrefs.SetString(YuiPrefsKeys.VoicePresetLibrary, JsonConvert.SerializeObject(presets));
            PlayerPrefs.Save();
        }
    }
}
