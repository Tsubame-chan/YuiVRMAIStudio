using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiGoogleAiEdgeBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern IntPtr YuiGoogleAiEdgeBridge_Invoke(string requestJson);

        [DllImport("__Internal")]
        private static extern void YuiGoogleAiEdgeBridge_Free(IntPtr pointer);
        [DllImport("__Internal")] private static extern void YuiGoogleAiEdgeBridge_Cancel(string requestId);
        [DllImport("__Internal")] private static extern void YuiGoogleAiEdgeBridge_Forget(string requestId);
#endif

        public static bool IsSupported
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return true;
#elif UNITY_ANDROID && !UNITY_EDITOR
                return true;
#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
                return YuiDesktopInferenceProcess.IsAvailable;
#else
                return false;
#endif
            }
        }

        public static YuiGoogleAiEdgeBridgeResponse Invoke(YuiGoogleAiEdgeBridgeRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return Error("invalid_request", "Google AI Edge bridge request is null.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var requestJson = JsonConvert.SerializeObject(request);
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                var requestId = Guid.NewGuid().ToString("N");
                var json = Newtonsoft.Json.Linq.JObject.Parse(requestJson);
                json["request_id"] = requestId;
                requestJson = json.ToString(Formatting.None);
                try
                {
                    using var registration = cancellationToken.Register(() => YuiGoogleAiEdgeBridge_Cancel(requestId));
                    cancellationToken.ThrowIfCancellationRequested();
                    var response = Parse(InvokeNativeJson(() => YuiGoogleAiEdgeBridge_Invoke(requestJson)));
                    cancellationToken.ThrowIfCancellationRequested();
                    return response;
                }
                finally { YuiGoogleAiEdgeBridge_Forget(requestId); }
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (var bridge = new AndroidJavaClass("jp.tsubamechan.yuivrm.localai.YuiGoogleAiEdgeBridge"))
                {
                    return Parse(bridge.CallStatic<string>("invoke", requestJson));
                }
#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
                return Parse(YuiDesktopInferenceProcess.Invoke(requestJson, cancellationToken));
#else
                return Error("platform_unsupported", "Google AI Edge bridge is only available on supported player builds.");
#endif
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Debug.LogWarning($"Yui Google AI Edge bridge failed: {ex.Message}");
                return Error("bridge_error", ex.Message);
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
                YuiGoogleAiEdgeBridge_Free(pointer);
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



        private static YuiGoogleAiEdgeBridgeResponse Parse(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                return Error("empty_response", "Google AI Edge bridge returned an empty response.");
            }

            return JsonConvert.DeserializeObject<YuiGoogleAiEdgeBridgeResponse>(responseJson)
                ?? Error("invalid_response", "Google AI Edge bridge returned invalid JSON.");
        }

        private static YuiGoogleAiEdgeBridgeResponse Error(string code, string message)
        {
            return new YuiGoogleAiEdgeBridgeResponse
            {
                Ok = false,
                ErrorCode = code,
                ErrorMessage = message
            };
        }
    }
}
