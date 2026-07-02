using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiVoicevoxCoreBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern IntPtr YuiVoicevoxCoreBridge_Synthesize(string requestJson);

        [DllImport("__Internal")]
        private static extern void YuiVoicevoxCoreBridge_Free(IntPtr pointer);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [DllImport("YuiVoicevoxCoreBridge")]
        private static extern IntPtr YuiVoicevoxCoreBridge_Synthesize(string requestJson);

        [DllImport("YuiVoicevoxCoreBridge")]
        private static extern void YuiVoicevoxCoreBridge_Free(IntPtr pointer);
#endif

        public static bool IsSupported
        {
            get
            {
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static YuiVoicevoxCoreSynthesisResult Synthesize(
            string text,
            int styleId,
            float speedScale,
            float pitchScale,
            float intonationScale,
            float volumeScale,
            float prePhonemeLength,
            float postPhonemeLength)
        {
            if (!IsSupported)
            {
                return YuiVoicevoxCoreSynthesisResult.Error("platform_unsupported", "VOICEVOX Core native bridge is not available.");
            }

            var root = YuiLocalAiPathResolver.VoicevoxRootPath();
            var payload = JsonConvert.SerializeObject(new
            {
                text = text ?? string.Empty,
                style_id = styleId,
                speed_scale = speedScale,
                pitch_scale = pitchScale,
                intonation_scale = intonationScale,
                volume_scale = volumeScale,
                pre_phoneme_length = prePhonemeLength,
                post_phoneme_length = postPhonemeLength,
                open_jtalk_dict_path = Path.Combine(root, "open_jtalk_dic_utf_8-1.11"),
                model_path = Path.Combine(root, "Models", "meimei_himari_1.vvm")
            });

#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            try
            {
                return JsonConvert.DeserializeObject<YuiVoicevoxCoreSynthesisResult>(
                           InvokeNativeJson(() => YuiVoicevoxCoreBridge_Synthesize(payload)))
                       ?? YuiVoicevoxCoreSynthesisResult.Error("invalid_response", "VOICEVOX Core returned no JSON.");
            }
            catch (Exception ex)
            {
                return YuiVoicevoxCoreSynthesisResult.Error("invalid_response", ex.Message);
            }
#else
            return YuiVoicevoxCoreSynthesisResult.Error("platform_unsupported", "VOICEVOX Core native bridge is not available.");
#endif
        }

#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
        private static string InvokeNativeJson(Func<IntPtr> invoke)
        {
            var pointer = invoke();
            if (pointer == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                return PtrToUtf8String(pointer);
            }
            finally
            {
                YuiVoicevoxCoreBridge_Free(pointer);
            }
        }

        private static string PtrToUtf8String(IntPtr pointer)
        {
            var length = 0;
            while (Marshal.ReadByte(pointer, length) != 0)
            {
                length++;
            }

            if (length == 0)
            {
                return string.Empty;
            }

            var buffer = new byte[length];
            Marshal.Copy(pointer, buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }
#endif
    }

    [Serializable]
    public sealed class YuiVoicevoxCoreSynthesisResult
    {
        [JsonProperty("ok")] public bool Ok { get; set; }
        [JsonProperty("error_code")] public string ErrorCode { get; set; }
        [JsonProperty("error_message")] public string ErrorMessage { get; set; }
        [JsonProperty("audio_base64")] public string AudioBase64 { get; set; }
        [JsonProperty("sample_rate")] public int SampleRate { get; set; } = 24000;
        [JsonProperty("duration_ms")] public int DurationMs { get; set; }

        public byte[] AudioBytes()
        {
            return string.IsNullOrWhiteSpace(AudioBase64)
                ? Array.Empty<byte>()
                : Convert.FromBase64String(AudioBase64);
        }

        public static YuiVoicevoxCoreSynthesisResult Error(string code, string message)
        {
            return new YuiVoicevoxCoreSynthesisResult
            {
                Ok = false,
                ErrorCode = code,
                ErrorMessage = message
            };
        }
    }
}
