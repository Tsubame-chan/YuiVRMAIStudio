using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace YuiPhysicalAI.Platform
{
    [Serializable]
    public sealed class YuiForegroundAppInfo
    {
        public string ProcessName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Category { get; set; } = "";
        public string WindowTitle { get; set; } = "";

        public bool IsAvailable => !string.IsNullOrWhiteSpace(ProcessName);

        public string StatusLabel()
        {
            if (!IsAvailable)
            {
                return "App: unavailable";
            }

            var label = string.IsNullOrWhiteSpace(Category) ? "App" : Category;
            var name = string.IsNullOrWhiteSpace(DisplayName) ? ProcessName : DisplayName;
            return $"App: {label} ({name})";
        }

        public string StableKey()
        {
            return (Category + "|" + ProcessName + "|" + DisplayName).ToLowerInvariant();
        }
    }

    public sealed class YuiWindowsForegroundAppMonitor : MonoBehaviour
    {
        public YuiForegroundAppInfo GetForegroundApp()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                return new YuiForegroundAppInfo();
            }

            GetWindowThreadProcessId(foregroundWindow, out var processId);
            if (processId == 0)
            {
                return new YuiForegroundAppInfo();
            }

            try
            {
                using var process = Process.GetProcessById((int)processId);
                var processName = process.ProcessName ?? "";
                return new YuiForegroundAppInfo
                {
                    ProcessName = processName,
                    DisplayName = FriendlyProcessName(processName),
                    Category = CategoryForProcess(processName),
                    WindowTitle = ReadWindowTitle(foregroundWindow)
                };
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Yui app awareness failed: {ex.Message}");
                return new YuiForegroundAppInfo();
            }
#else
            return new YuiForegroundAppInfo();
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private static string ReadWindowTitle(IntPtr window)
        {
            var builder = new StringBuilder(160);
            var length = GetWindowText(window, builder, builder.Capacity);
            return length > 0 ? builder.ToString() : "";
        }
#endif

        private static string FriendlyProcessName(string processName)
        {
            var normalized = (processName ?? "").Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "chrome":
                    return "Chrome";
                case "msedge":
                    return "Edge";
                case "firefox":
                    return "Firefox";
                case "discord":
                    return "Discord";
                case "spotify":
                    return "Spotify";
                case "code":
                    return "VS Code";
                case "devenv":
                    return "Visual Studio";
                case "unity":
                    return "Unity";
                case "obs64":
                    return "OBS";
                default:
                    return string.IsNullOrWhiteSpace(processName) ? "" : processName;
            }
        }

        private static string CategoryForProcess(string processName)
        {
            var normalized = (processName ?? "").Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "chrome":
                case "msedge":
                case "firefox":
                case "brave":
                case "opera":
                    return "Browser";
                case "discord":
                    return "Discord";
                case "spotify":
                    return "Spotify";
                case "code":
                case "devenv":
                case "rider64":
                    return "Editor";
                case "unity":
                    return "Unity";
                case "obs64":
                    return "Streaming";
                default:
                    return "Desktop";
            }
        }
    }
}
