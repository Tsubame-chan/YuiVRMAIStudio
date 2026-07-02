using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace YuiPhysicalAI.Platform
{
    public sealed class YuiIOSDocumentPicker : MonoBehaviour
    {
        private const string BridgeObjectName = "YuiIOSDocumentPickerBridge";
        private const string Cancelled = "__YUI_CANCELLED__";
        private const string ErrorPrefix = "__YUI_ERROR__:";

        private static TaskCompletionSource<YuiFilePicker.Result> pending;
        private static YuiIOSDocumentPicker bridge;

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
#if UNITY_IOS && !UNITY_EDITOR
            if (pending != null)
            {
                return Task.FromResult(new YuiFilePicker.Result(false, null, "ファイル選択はすでに開いています。"));
            }

            EnsureBridge();
            pending = new TaskCompletionSource<YuiFilePicker.Result>();
            YuiIOSDocumentPicker_Open(mode, BridgeObjectName);
            return pending.Task;
#else
            return Task.FromResult(new YuiFilePicker.Result(false, null, "iOS実機以外ではiOSドキュメントピッカーを開けません。"));
#endif
        }

        public void OnIOSDocumentPickerResult(string message)
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
            bridge = owner.GetComponent<YuiIOSDocumentPicker>();
            if (bridge == null)
            {
                bridge = owner.AddComponent<YuiIOSDocumentPicker>();
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void YuiIOSDocumentPicker_Open(string mode, string callbackObjectName);
#endif
    }
}
