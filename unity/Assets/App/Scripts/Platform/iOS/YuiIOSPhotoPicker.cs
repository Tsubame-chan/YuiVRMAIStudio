using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace YuiPhysicalAI.Platform
{
    public sealed class YuiIOSPhotoPicker : MonoBehaviour
    {
        private const string BridgeObjectName = "YuiIOSPhotoPickerBridge";
        private const string Cancelled = "__YUI_CANCELLED__";
        private const string ErrorPrefix = "__YUI_ERROR__:";

        private static TaskCompletionSource<YuiFilePicker.Result> pending;
        private static YuiIOSPhotoPicker bridge;

        public static Task<YuiFilePicker.Result> OpenImageAsync()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (pending != null)
            {
                return Task.FromResult(new YuiFilePicker.Result(false, null, "写真選択はすでに開いています。"));
            }

            EnsureBridge();
            pending = new TaskCompletionSource<YuiFilePicker.Result>();
            YuiIOSPhotoPicker_OpenPhotoLibrary(BridgeObjectName);
            return pending.Task;
#else
            return Task.FromResult(new YuiFilePicker.Result(false, null, "iOS実機以外ではiOS写真ライブラリを開けません。"));
#endif
        }

        public void OnIOSPhotoPickerResult(string message)
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
                completion.TrySetResult(new YuiFilePicker.Result(false, null, "写真ライブラリから画像を読み込めませんでした。"));
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
            bridge = owner.GetComponent<YuiIOSPhotoPicker>();
            if (bridge == null)
            {
                bridge = owner.AddComponent<YuiIOSPhotoPicker>();
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void YuiIOSPhotoPicker_OpenPhotoLibrary(string callbackObjectName);
#endif
    }
}
