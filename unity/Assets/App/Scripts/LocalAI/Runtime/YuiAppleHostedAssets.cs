using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiAppleHostedAssets
    {
        private static YuiLocalAiModelPack DownloadModel => YuiLocalAiModelRegistry.FromStreamingAssetsOrDefault()
            .EnabledFor(YuiLocalAiCapability.Chat, "ios")
            .FirstOrDefault(pack => !string.IsNullOrWhiteSpace(pack.AppleAssetPackId));

        public static string ModelPath => "YuiLocalAI/Models/" + YuiLocalAiModelPathResolver.ModelFileName(DownloadModel);

        public static bool HasLocalConversationModel()
        {
            return YuiLocalAiModelRegistry.FromStreamingAssetsOrDefault()
                .EnabledFor(YuiLocalAiCapability.Chat, "ios")
                .Any(pack => File.Exists(YuiLocalAiModelPathResolver.PersistentModelPath(pack))
                    || File.Exists(YuiLocalAiModelPathResolver.StreamingAssetsModelPath(pack)));
        }
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int YuiAppleAssets_Enabled();
        [DllImport("__Internal")] private static extern IntPtr YuiAppleAssets_Path(string relative);
        [DllImport("__Internal")] private static extern IntPtr YuiAppleAssets_Status();
        [DllImport("__Internal")] private static extern void YuiAppleAssets_Free(IntPtr pointer);
        [DllImport("__Internal")] private static extern void YuiAppleAssets_Prepare(string assetPackId);
        [DllImport("__Internal")] private static extern void YuiAppleAssets_Cancel();
        private static string Read(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringAnsi(pointer); }
            finally { YuiAppleAssets_Free(pointer); }
        }
#endif
        public static bool Enabled
        {
            get {
#if UNITY_IOS && !UNITY_EDITOR
                return YuiAppleAssets_Enabled() == 1;
#else
                return false;
#endif
            }
        }
        public static string ResolvePath(string relative)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return Enabled ? Read(YuiAppleAssets_Path(relative)) : null;
#else
            return null;
#endif
        }
        public static void Prepare()
        {
            Prepare(DownloadModel);
        }
        public static void Cancel()
        {
#if UNITY_IOS && !UNITY_EDITOR
            YuiAppleAssets_Cancel();
#endif
        }
        public static void Prepare(YuiLocalAiModelPack pack)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (pack == null || string.IsNullOrWhiteSpace(pack.AppleAssetPackId)) throw new InvalidOperationException("This model is bundled with the app and has no Apple download. Reinstall the app if bundled data is missing.");
            YuiAppleAssets_Prepare(pack.AppleAssetPackId);
#endif
        }
        public sealed class DownloadStatus
        {
            public string state;
            public float progress;
            public string message;
        }
        public static DownloadStatus Status()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return JsonConvert.DeserializeObject<DownloadStatus>(Read(YuiAppleAssets_Status()));
#else
            return new DownloadStatus { state = "unavailable" };
#endif
        }
    }
}
