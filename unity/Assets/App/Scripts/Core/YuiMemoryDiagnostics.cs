using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;

namespace YuiPhysicalAI.Core
{
    public static class YuiMemoryDiagnostics
    {
        private const long MaxLogBytes = 512L * 1024L;
        private static bool lowMemoryHandlerRegistered;
        private static string logPath;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern ulong YuiMemoryDiagnostics_GetResidentBytes();
#endif

        public static void RegisterLowMemoryHandler()
        {
            if (lowMemoryHandlerRegistered)
            {
                return;
            }

            lowMemoryHandlerRegistered = true;
            Application.lowMemory += () => LogSnapshot("ios_low_memory_warning");
        }

        public static void LogSnapshot(string label, string detail = null)
        {
            var snapshot = Snapshot(label, detail);
            Debug.Log(snapshot);
            AppendFile(snapshot);
        }

        public static string Snapshot(string label, string detail = null)
        {
            var managed = GC.GetTotalMemory(false);
            var unityAllocated = Profiler.GetTotalAllocatedMemoryLong();
            var unityReserved = Profiler.GetTotalReservedMemoryLong();
            var monoUsed = Profiler.GetMonoUsedSizeLong();
            var monoHeap = Profiler.GetMonoHeapSizeLong();
            var resident = ResidentBytes();

            var detailPart = string.IsNullOrWhiteSpace(detail)
                ? string.Empty
                : $" detail={SanitizeDetail(detail)},";
            return $"Yui memory: label={SanitizeLabel(label)},{detailPart} rss={FormatMiB(resident)}, "
                + $"managed={FormatMiB(managed)}, unity_alloc={FormatMiB(unityAllocated)}, "
                + $"unity_reserved={FormatMiB(unityReserved)}, mono_used={FormatMiB(monoUsed)}, "
                + $"mono_heap={FormatMiB(monoHeap)}";
        }

        private static ulong ResidentBytes()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return YuiMemoryDiagnostics_GetResidentBytes();
#else
            return 0;
#endif
        }

        private static string FormatMiB(ulong bytes)
        {
            return bytes == 0 ? "n/a" : $"{bytes / 1048576.0:0.0}MB";
        }

        private static string FormatMiB(long bytes)
        {
            return bytes <= 0 ? "n/a" : $"{bytes / 1048576.0:0.0}MB";
        }

        private static string SanitizeLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return "unknown";
            }

            return label.Replace(' ', '_').Replace('\n', '_').Replace('\r', '_');
        }

        private static string SanitizeDetail(string detail)
        {
            return detail
                .Replace(' ', '_')
                .Replace(',', ';')
                .Replace('\n', '_')
                .Replace('\r', '_');
        }

        private static void AppendFile(string line)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(logPath))
                {
                    logPath = Path.Combine(Application.persistentDataPath, "yui-memory.log");
                }

                if (File.Exists(logPath) && new FileInfo(logPath).Length > MaxLogBytes)
                {
                    File.WriteAllText(logPath, $"{DateTime.UtcNow:O} Yui memory log rotated\n");
                }

                File.AppendAllText(logPath, $"{DateTime.UtcNow:O} {line}\n");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Yui memory log write failed: {ex.Message}");
            }
        }
    }
}
