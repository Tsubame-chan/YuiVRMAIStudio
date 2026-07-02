using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiPlatformVisionBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern IntPtr YuiPlatformVisionBridge_Analyze(string requestJson);

        [DllImport("__Internal")]
        private static extern void YuiPlatformVisionBridge_Free(IntPtr pointer);
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

        public static YuiPlatformVisionResult Analyze(YuiLocalAiVisionRequest request)
        {
            if (!IsSupported)
            {
                return YuiPlatformVisionResult.Error("platform_unsupported", "Platform image recognition is not available.");
            }

            if (request?.ImageBytes == null || request.ImageBytes.Length == 0)
            {
                return YuiPlatformVisionResult.Error("invalid_image", "Image bytes are required.");
            }

            var tempPath = Path.Combine(Application.temporaryCachePath, $"yui-vision-{Guid.NewGuid():N}{ExtensionForMimeType(request.MimeType)}");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath));
                File.WriteAllBytes(tempPath, request.ImageBytes);
                var payload = JsonConvert.SerializeObject(new
                {
                    image_path = tempPath,
                    mime_type = request.MimeType ?? "image/jpeg",
                    prompt_type = request.PromptType ?? "file"
                });

#if UNITY_IOS && !UNITY_EDITOR
                return Parse(InvokeNativeJson(() => YuiPlatformVisionBridge_Analyze(payload)));
#else
                return YuiPlatformVisionResult.Error("platform_unsupported", "Platform image recognition is not available.");
#endif
            }
            catch (Exception ex)
            {
                return YuiPlatformVisionResult.Error("bridge_error", ex.Message);
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

        private static string ExtensionForMimeType(string mimeType)
        {
            return string.Equals(mimeType, "image/png", StringComparison.OrdinalIgnoreCase)
                ? ".png"
                : ".jpg";
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
                YuiPlatformVisionBridge_Free(pointer);
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

        private static YuiPlatformVisionResult Parse(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<YuiPlatformVisionResult>(json ?? "")
                    ?? YuiPlatformVisionResult.Error("invalid_response", "Platform vision returned no JSON.");
            }
            catch (Exception ex)
            {
                return YuiPlatformVisionResult.Error("invalid_response", ex.Message);
            }
        }
    }

    public sealed class YuiPlatformVisionResult
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("labels")]
        public string[] Labels { get; set; } = Array.Empty<string>();

        [JsonProperty("recognized_text")]
        public string RecognizedText { get; set; }

        [JsonProperty("error_code")]
        public string ErrorCode { get; set; }

        [JsonProperty("error_message")]
        public string ErrorMessage { get; set; }

        public static YuiPlatformVisionResult Error(string code, string message)
        {
            return new YuiPlatformVisionResult
            {
                Ok = false,
                ErrorCode = code,
                ErrorMessage = message
            };
        }
    }
}
