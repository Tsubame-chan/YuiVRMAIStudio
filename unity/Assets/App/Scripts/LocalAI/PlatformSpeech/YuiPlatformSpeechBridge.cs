using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiPlatformSpeechBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern IntPtr YuiPlatformSpeechBridge_Synthesize(string requestJson);

        [DllImport("__Internal")]
        private static extern IntPtr YuiPlatformSpeechBridge_Transcribe(string requestJson);

        [DllImport("__Internal")]
        private static extern void YuiPlatformSpeechBridge_Free(IntPtr pointer);
#endif

        public static bool IsSupported
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static YuiPlatformSpeechSynthesisResult Synthesize(YuiLocalAiSpeechRequest request)
        {
            if (!IsSupported)
            {
                return YuiPlatformSpeechSynthesisResult.Error("platform_unsupported", "Platform speech synthesis is not available.");
            }

            var payload = JsonConvert.SerializeObject(new
            {
                text = request?.Text ?? string.Empty,
                language_code = "ja-JP",
                speed_scale = request?.SpeedScale ?? 1.0f,
                pitch_scale = request?.PitchScale ?? 0.0f
            });

#if UNITY_IOS && !UNITY_EDITOR
            return ParseSynthesis(InvokeNativeJson(() => YuiPlatformSpeechBridge_Synthesize(payload)));
#else
            return YuiPlatformSpeechSynthesisResult.Error("platform_unsupported", "Platform speech synthesis is not available.");
#endif
        }

        public static YuiPlatformSpeechTranscriptionResult Transcribe(YuiLocalAiAudioRequest request)
        {
            if (!IsSupported)
            {
                return YuiPlatformSpeechTranscriptionResult.Error("platform_unsupported", "Platform speech recognition is not available.");
            }

            if (request?.AudioBytes == null || request.AudioBytes.Length <= 44)
            {
                return YuiPlatformSpeechTranscriptionResult.Error("invalid_audio", "Recorded audio is empty.");
            }

            var tempPath = Path.Combine(Application.temporaryCachePath, $"yui-stt-{Guid.NewGuid():N}.wav");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath));
                File.WriteAllBytes(tempPath, request.AudioBytes);
                var payload = JsonConvert.SerializeObject(new
                {
                    audio_path = tempPath,
                    language_code = "ja-JP"
                });

#if UNITY_IOS && !UNITY_EDITOR
                return ParseTranscription(InvokeNativeJson(() => YuiPlatformSpeechBridge_Transcribe(payload)));
#else
                return YuiPlatformSpeechTranscriptionResult.Error("platform_unsupported", "Platform speech recognition is not available.");
#endif
            }
            catch (Exception ex)
            {
                return YuiPlatformSpeechTranscriptionResult.Error("bridge_error", ex.Message);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (Exception)
                {
                    // Best-effort temp cleanup.
                }
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
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
                YuiPlatformSpeechBridge_Free(pointer);
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

        private static YuiPlatformSpeechSynthesisResult ParseSynthesis(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<YuiPlatformSpeechSynthesisResult>(json ?? "")
                    ?? YuiPlatformSpeechSynthesisResult.Error("invalid_response", "Platform speech returned no JSON.");
            }
            catch (Exception ex)
            {
                return YuiPlatformSpeechSynthesisResult.Error("invalid_response", ex.Message);
            }
        }

        private static YuiPlatformSpeechTranscriptionResult ParseTranscription(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<YuiPlatformSpeechTranscriptionResult>(json ?? "")
                    ?? YuiPlatformSpeechTranscriptionResult.Error("invalid_response", "Platform speech returned no JSON.");
            }
            catch (Exception ex)
            {
                return YuiPlatformSpeechTranscriptionResult.Error("invalid_response", ex.Message);
            }
        }
    }

    [Serializable]
    public sealed class YuiPlatformSpeechSynthesisResult
    {
        [JsonProperty("ok")] public bool Ok { get; set; }
        [JsonProperty("error_code")] public string ErrorCode { get; set; }
        [JsonProperty("error_message")] public string ErrorMessage { get; set; }
        [JsonProperty("audio_base64")] public string AudioBase64 { get; set; }
        [JsonProperty("sample_rate")] public int SampleRate { get; set; } = 24000;
        [JsonProperty("duration_ms")] public int DurationMs { get; set; }

        public byte[] AudioBytes()
        {
            return string.IsNullOrWhiteSpace(AudioBase64) ? Array.Empty<byte>() : Convert.FromBase64String(AudioBase64);
        }

        public static YuiPlatformSpeechSynthesisResult Error(string code, string message)
        {
            return new YuiPlatformSpeechSynthesisResult { Ok = false, ErrorCode = code, ErrorMessage = message };
        }
    }

    [Serializable]
    public sealed class YuiPlatformSpeechTranscriptionResult
    {
        [JsonProperty("ok")] public bool Ok { get; set; }
        [JsonProperty("error_code")] public string ErrorCode { get; set; }
        [JsonProperty("error_message")] public string ErrorMessage { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("confidence")] public float? Confidence { get; set; }

        public static YuiPlatformSpeechTranscriptionResult Error(string code, string message)
        {
            return new YuiPlatformSpeechTranscriptionResult { Ok = false, ErrorCode = code, ErrorMessage = message };
        }
    }
}
