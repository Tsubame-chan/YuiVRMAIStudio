using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiAivisNativeBridge
    {
        private static bool androidExtractionAttempted;
        private static bool loggedAivisRootDiagnostics;

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
#if UNITY_IOS
        [DllImport("__Internal")]
#else
        [DllImport("YuiAivisNativeBridge")]
#endif
        private static extern IntPtr YuiAivisNativeBridge_Synthesize(string requestJson);

#if UNITY_IOS
        [DllImport("__Internal")]
#else
        [DllImport("YuiAivisNativeBridge")]
#endif
        private static extern IntPtr YuiAivisNativeBridge_GetStatus(string requestJson);

#if UNITY_IOS
        [DllImport("__Internal")]
#else
        [DllImport("YuiAivisNativeBridge")]
#endif
        private static extern void YuiAivisNativeBridge_Free(IntPtr pointer);
#endif

        public static bool IsPlatformSupported
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return true;
#elif UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static YuiAivisNativeStatus GetStatus()
        {
            var payload = JsonConvert.SerializeObject(new
            {
                root_path = RootPath(),
                platform = RuntimePlatformName()
            });

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            var nativeRuntimeLinked = TryGetNativeStatus(payload, out var nativeStatus);
            var status = YuiAivisNativeStatus.FromCoreStatus(
                YuiAivisCoreProbe.Evaluate(RootPath(), nativeRuntimeLinked, RuntimePlatformName()));

            if (nativeStatus != null && nativeStatus.MissingComponents != null)
            {
                status.MissingComponents = status.MissingComponents
                    .Concat(nativeStatus.MissingComponents)
                    .Where(component => !string.IsNullOrWhiteSpace(component))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                status.RuntimeReady = status.RuntimeReady
                    && nativeStatus.RuntimeReady
                    && status.MissingComponents.Length == 0;
            }

            return status;
#else
            return YuiAivisNativeStatus.FromCoreStatus(YuiAivisCoreProbe.Evaluate(RootPath(), nativeRuntimeLinked: false, RuntimePlatformName()));
#endif
        }

        public static YuiAivisNativeSynthesisResult Synthesize(
            string text,
            int voiceId,
            float speedScale,
            float pitchScale,
            float intonationScale,
            float volumeScale,
            float prePhonemeLength,
            float postPhonemeLength)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return YuiAivisNativeSynthesisResult.Error("invalid_request", "Aivis text is empty.");
            }

            var root = RootPath();
            var voice = YuiAivisNativeVoiceCatalog.FindVoice(root, voiceId);
            if (voice == null)
            {
                return YuiAivisNativeSynthesisResult.Error("voice_missing", $"Aivis voice is not configured: {voiceId}");
            }

            var status = GetStatus();
            if (status == null || !status.RuntimeReady)
            {
                return YuiAivisNativeSynthesisResult.Error(
                    "runtime_unavailable",
                    status?.ErrorMessage ?? "Aivis native runtime is not ready.",
                    status?.MissingComponents ?? Array.Empty<string>());
            }

            var payload = JsonConvert.SerializeObject(new
            {
                text,
                voice_id = voice.Id,
                voice_key = voice.Key,
                display_name = voice.DisplayName,
                root_path = root,
                model_path = Path.Combine(root, voice.ModelPath),
                hyper_parameters_path = Path.Combine(root, voice.HyperParametersPath),
                style_vectors_path = Path.Combine(root, voice.StyleVectorsPath),
                bert_model_path = Path.Combine(root, "Runtime", "JapaneseBert", "model_fp16.onnx"),
                bert_tokenizer_path = Path.Combine(root, "Runtime", "JapaneseBert", "tokenizer.json"),
                bert_vocab_path = Path.Combine(root, "Runtime", "JapaneseBert", "vocab.txt"),
                open_jtalk_dict_path = Path.Combine(VoicevoxRootPath(), "open_jtalk_dic_utf_8-1.11"),
                voicevox_model_path = Path.Combine(VoicevoxRootPath(), "Models", "meimei_himari_1.vvm"),
                voicevox_speaker_id = voice.VoicevoxSpeakerId > 0 ? voice.VoicevoxSpeakerId : 14,
                speaker_id = voice.SpeakerId,
                style_id = voice.DefaultStyleId,
                sampling_rate = voice.SamplingRate,
                speed_scale = speedScale,
                pitch_scale = pitchScale,
                intonation_scale = intonationScale,
                volume_scale = volumeScale,
                pre_phoneme_length = prePhonemeLength,
                post_phoneme_length = postPhonemeLength
            });

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            try
            {
                return JsonConvert.DeserializeObject<YuiAivisNativeSynthesisResult>(
                           InvokeNativeJson(() => YuiAivisNativeBridge_Synthesize(payload)))
                       ?? YuiAivisNativeSynthesisResult.Error("invalid_response", "Aivis native bridge returned no JSON.");
            }
            catch (Exception ex)
            {
                return YuiAivisNativeSynthesisResult.Error("invalid_response", ex.Message);
            }
#else
            return YuiAivisNativeSynthesisResult.Error("platform_unsupported", "Aivis native bridge is not available on this platform yet.");
#endif
        }

        private static string RootPath()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureAndroidAivisAssetsExtracted();
            return Path.Combine(AndroidLocalAiRootPath(), "Aivis");
#else
            var root = YuiLocalAiPathResolver.AivisRootPath();
            LogAivisRootDiagnostics(root);
            return root;
#endif
        }

        private static string VoicevoxRootPath()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureAndroidAivisAssetsExtracted();
            return Path.Combine(AndroidLocalAiRootPath(), "Voicevox");
#else
            return YuiLocalAiPathResolver.VoicevoxRootPath();
#endif
        }

        private static void LogAivisRootDiagnostics(string root)
        {
            if (loggedAivisRootDiagnostics)
            {
                return;
            }

            loggedAivisRootDiagnostics = true;
            Debug.Log(
                $"Aivis root resolved: root={root}, exists={Directory.Exists(root)}, candidates={YuiLocalAiPathResolver.DebugCandidateSummary()}");
        }

        private static string RuntimePlatformName()
        {
#if UNITY_IOS
            return "ios";
#elif UNITY_ANDROID
            return "android";
#elif UNITY_STANDALONE_OSX
            return "macos";
#elif UNITY_STANDALONE_WIN
            return "windows";
#else
            return "unknown";
#endif
        }

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        private static bool TryGetNativeStatus(string payload, out YuiAivisNativeStatus status)
        {
            status = null;
            try
            {
                status = JsonConvert.DeserializeObject<YuiAivisNativeStatus>(
                    InvokeNativeJson(() => YuiAivisNativeBridge_GetStatus(payload)));
                return status != null && status.NativeRuntimeLinked;
            }
            catch (Exception ex)
            {
                status = YuiAivisNativeStatus.Error("native_status_unavailable", ex.Message);
                status.MissingComponents = YuiAivisRuntimeAssets.RequiredComponentNames();
                return false;
            }
        }

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
                YuiAivisNativeBridge_Free(pointer);
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

#if UNITY_ANDROID && !UNITY_EDITOR
        private static string AndroidLocalAiRootPath()
        {
            return Path.Combine(Application.persistentDataPath, "YuiLocalAI");
        }

        private static void EnsureAndroidAivisAssetsExtracted()
        {
            if (androidExtractionAttempted)
            {
                return;
            }

            androidExtractionAttempted = true;
            try
            {
                using (var extractor = new AndroidJavaClass("jp.tsubamechan.yuivrm.localai.YuiAivisAssetExtractor"))
                {
                    var response = extractor.CallStatic<string>("ensureExtracted", AndroidLocalAiRootPath());
                    var parsed = JObject.Parse(response ?? "{}");
                    if (!parsed.Value<bool>("ok"))
                    {
                        Debug.LogWarning($"Aivis Android asset extraction failed: {parsed.Value<string>("error_message")}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Aivis Android asset extraction failed: {ex.Message}");
            }
        }
#endif
    }

    [Serializable]
    public sealed class YuiAivisNativeStatus
    {
        [JsonProperty("ok")] public bool Ok { get; set; }
        [JsonProperty("error_code")] public string ErrorCode { get; set; }
        [JsonProperty("error_message")] public string ErrorMessage { get; set; }
        [JsonProperty("runtime_ready")] public bool RuntimeReady { get; set; }
        [JsonProperty("models_ready")] public bool ModelsReady { get; set; }
        [JsonProperty("native_runtime_linked")] public bool NativeRuntimeLinked { get; set; }
        [JsonProperty("text_frontend_linked")] public bool TextFrontendLinked { get; set; }
        [JsonProperty("root_path")] public string RootPath { get; set; }
        [JsonProperty("catalog_path")] public string CatalogPath { get; set; }
        [JsonProperty("missing_components")] public string[] MissingComponents { get; set; } = Array.Empty<string>();

        public static YuiAivisNativeStatus Error(string code, string message)
        {
            return new YuiAivisNativeStatus
            {
                Ok = false,
                ErrorCode = code,
                ErrorMessage = message,
                RuntimeReady = false,
                ModelsReady = false,
                MissingComponents = Array.Empty<string>()
            };
        }

        public static YuiAivisNativeStatus FromCoreStatus(YuiAivisCoreStatus status)
        {
            if (status == null)
            {
                return Error("invalid_status", "Aivis core status was empty.");
            }

            return new YuiAivisNativeStatus
            {
                Ok = status.Ok,
                ErrorCode = status.ErrorCode,
                ErrorMessage = status.ErrorMessage,
                RuntimeReady = status.RuntimeReady,
                ModelsReady = status.ModelsReady,
                RootPath = status.RootPath,
                CatalogPath = status.CatalogPath,
                MissingComponents = status.MissingComponents ?? Array.Empty<string>()
            };
        }
    }

    [Serializable]
    public sealed class YuiAivisNativeSynthesisResult
    {
        [JsonProperty("ok")] public bool Ok { get; set; }
        [JsonProperty("error_code")] public string ErrorCode { get; set; }
        [JsonProperty("error_message")] public string ErrorMessage { get; set; }
        [JsonProperty("audio_base64")] public string AudioBase64 { get; set; }
        [JsonProperty("sample_rate")] public int SampleRate { get; set; } = 44100;
        [JsonProperty("duration_ms")] public int DurationMs { get; set; }
        [JsonProperty("missing_components")] public string[] MissingComponents { get; set; } = Array.Empty<string>();

        public byte[] AudioBytes()
        {
            return string.IsNullOrWhiteSpace(AudioBase64)
                ? Array.Empty<byte>()
                : Convert.FromBase64String(AudioBase64);
        }

        public static YuiAivisNativeSynthesisResult Error(string code, string message)
        {
            return Error(code, message, Array.Empty<string>());
        }

        public static YuiAivisNativeSynthesisResult Error(string code, string message, string[] missingComponents)
        {
            return new YuiAivisNativeSynthesisResult
            {
                Ok = false,
                ErrorCode = code,
                ErrorMessage = message,
                MissingComponents = missingComponents ?? Array.Empty<string>()
            };
        }
    }
}
