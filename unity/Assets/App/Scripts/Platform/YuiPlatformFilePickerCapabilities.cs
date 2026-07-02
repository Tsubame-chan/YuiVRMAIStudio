using System;
using UnityEngine;

namespace YuiPhysicalAI.Platform
{
    public enum YuiPlatformFamily
    {
        Editor,
        Windows,
        MacOS,
        IOS,
        Android,
        WebGL,
        Other
    }

    public readonly struct YuiFilePickerCapabilities
    {
        public YuiFilePickerCapabilities(bool image, bool vrm, string implementation)
        {
            SupportsImage = image;
            SupportsVrm = vrm;
            Implementation = implementation ?? string.Empty;
        }

        public bool SupportsImage { get; }
        public bool SupportsVrm { get; }
        public string Implementation { get; }
    }

    public static class YuiPlatformFilePickerCapabilities
    {
        public static YuiPlatformFamily CurrentPlatformFamily()
        {
#if UNITY_EDITOR_WIN
            return YuiPlatformFamily.Editor;
#elif UNITY_EDITOR_OSX
            return YuiPlatformFamily.Editor;
#elif UNITY_STANDALONE_WIN
            return YuiPlatformFamily.Windows;
#elif UNITY_STANDALONE_OSX
            return YuiPlatformFamily.MacOS;
#elif UNITY_IOS
            return YuiPlatformFamily.IOS;
#elif UNITY_ANDROID
            return YuiPlatformFamily.Android;
#elif UNITY_WEBGL
            return YuiPlatformFamily.WebGL;
#else
            return Application.platform == RuntimePlatform.WindowsPlayer
                ? YuiPlatformFamily.Windows
                : Application.platform == RuntimePlatform.OSXPlayer
                    ? YuiPlatformFamily.MacOS
                    : YuiPlatformFamily.Other;
#endif
        }

        public static YuiFilePickerCapabilities For(YuiPlatformFamily platform)
        {
            switch (platform)
            {
                case YuiPlatformFamily.Editor:
                    return new YuiFilePickerCapabilities(true, true, "UnityEditor.OpenFilePanel");
                case YuiPlatformFamily.Windows:
                    return new YuiFilePickerCapabilities(true, true, "YuiFilePickerHelper.exe");
                case YuiPlatformFamily.MacOS:
                    return new YuiFilePickerCapabilities(true, true, "native file dialog through standalone helper path");
                case YuiPlatformFamily.IOS:
                    return new YuiFilePickerCapabilities(true, true, "UIDocumentPicker copied into app storage");
                case YuiPlatformFamily.Android:
                    return new YuiFilePickerCapabilities(true, true, "Android Storage Access Framework copied into app storage");
                case YuiPlatformFamily.WebGL:
                    return new YuiFilePickerCapabilities(false, false, "browser file input not wired in this Unity build");
                default:
                    return new YuiFilePickerCapabilities(false, false, "unsupported platform");
            }
        }
    }
}
