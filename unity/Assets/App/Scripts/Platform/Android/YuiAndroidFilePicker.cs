using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace YuiPhysicalAI.Platform
{
    public sealed class YuiAndroidFilePicker : MonoBehaviour
    {
        private const string BridgeObjectName = "YuiAndroidFilePickerBridge";
        private const string JavaClassName = "jp.tsubamechan.yuivrm.localai.YuiAndroidFilePicker";
        private const string Cancelled = "__YUI_CANCELLED__";
        private const string ErrorPrefix = "__YUI_ERROR__:";

        private static TaskCompletionSource<YuiFilePicker.Result> pending;
        private static YuiAndroidFilePicker bridge;

        public static Task<YuiFilePicker.Result> OpenImageAsync()
        {
            return OpenAsync("image");
        }

        public static Task<YuiFilePicker.Result> OpenVrmAsync()
        {
            return OpenAsync("vrm");
        }

        private static Task<YuiFilePicker.Result> OpenAsync(string mode)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (pending != null)
            {
                return Task.FromResult(new YuiFilePicker.Result(false, null, "ファイル選択はすでに開いています。"));
            }

            EnsureBridge();
            pending = new TaskCompletionSource<YuiFilePicker.Result>();
            try
            {
                using (var bridgeClass = new AndroidJavaClass(JavaClassName))
                {
                    bridgeClass.CallStatic("open", mode, BridgeObjectName);
                }
            }
            catch (Exception ex)
            {
                var completion = pending;
                pending = null;
                return Task.FromResult(new YuiFilePicker.Result(false, null, $"Androidファイルピッカーを起動できませんでした: {ex.Message}"));
            }

            return pending.Task;
#else
            return Task.FromResult(new YuiFilePicker.Result(false, null, "Android実機以外ではAndroidファイルピッカーを開けません。"));
#endif
        }

        public void OnAndroidFilePickerResult(string message)
        {
            var completion = pending;
            pending = null;
            if (completion == null)
            {
                return;
            }

            if (string.Equals(message, Cancelled, StringComparison.Ordinal))
            {
                completion.TrySetResult(new YuiFilePicker.Result(false, null, null));
                return;
            }

            if (!string.IsNullOrEmpty(message) && message.StartsWith(ErrorPrefix, StringComparison.Ordinal))
            {
                completion.TrySetResult(new YuiFilePicker.Result(false, null, message.Substring(ErrorPrefix.Length)));
                return;
            }

            if (string.IsNullOrWhiteSpace(message) || !File.Exists(message))
            {
                completion.TrySetResult(new YuiFilePicker.Result(false, null, "選択したファイルをアプリ内へコピーできませんでした。"));
                return;
            }

            completion.TrySetResult(new YuiFilePicker.Result(true, message, null));
        }

        private static void EnsureBridge()
        {
            if (bridge != null)
            {
                return;
            }

            var existing = GameObject.Find(BridgeObjectName);
            var owner = existing != null ? existing : new GameObject(BridgeObjectName);
            DontDestroyOnLoad(owner);
            bridge = owner.GetComponent<YuiAndroidFilePicker>();
            if (bridge == null)
            {
                bridge = owner.AddComponent<YuiAndroidFilePicker>();
            }
        }
    }
}
