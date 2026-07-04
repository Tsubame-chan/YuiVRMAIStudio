using System;
using System.IO;

namespace YuiPhysicalAI.Backend
{
    public static class YuiDesktopBackendPaths
    {
        public const string DefaultBackendUrl = "http://127.0.0.1:8000";
        public const string BackendFolderName = "YuiBackend";
        private const string MacStartScriptRelativePath = "scripts/start_local_services_detached_macos.sh";
        private const string WindowsStartScriptRelativePath = "scripts/start_local_services.ps1";

        public static bool IsLocalBackendUrl(string backendUrl)
        {
            if (string.IsNullOrWhiteSpace(backendUrl))
            {
                return true;
            }

            if (!Uri.TryCreate(backendUrl.Trim(), UriKind.Absolute, out var uri))
            {
                return false;
            }

            var host = uri.Host.ToLowerInvariant();
            return host == "127.0.0.1" || host == "localhost" || host == "::1";
        }

        public static string ResolveMacBackendRoot(string applicationDataPath, string persistentDataPath = null)
        {
            if (!string.IsNullOrWhiteSpace(persistentDataPath))
            {
                var persistentCandidate = Path.Combine(Path.GetFullPath(persistentDataPath), BackendFolderName);
                if (File.Exists(StartScriptPath(persistentCandidate)))
                {
                    return persistentCandidate;
                }
            }

            if (string.IsNullOrWhiteSpace(applicationDataPath))
            {
                return !string.IsNullOrWhiteSpace(persistentDataPath)
                    ? Path.Combine(Path.GetFullPath(persistentDataPath), BackendFolderName)
                    : string.Empty;
            }

            var dataPath = Path.GetFullPath(applicationDataPath);
            var directory = new DirectoryInfo(dataPath);
            if (string.Equals(directory.Name, "Contents", StringComparison.OrdinalIgnoreCase)
                && directory.Parent != null
                && directory.Parent.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
                && directory.Parent.Parent != null)
            {
                var sibling = Path.Combine(directory.Parent.Parent.FullName, BackendFolderName);
                if (File.Exists(StartScriptPath(sibling)) || string.IsNullOrWhiteSpace(persistentDataPath))
                {
                    return sibling;
                }
            }

            if (directory.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
                && directory.Parent != null)
            {
                var sibling = Path.Combine(directory.Parent.FullName, BackendFolderName);
                if (File.Exists(StartScriptPath(sibling)) || string.IsNullOrWhiteSpace(persistentDataPath))
                {
                    return sibling;
                }
            }

            return !string.IsNullOrWhiteSpace(persistentDataPath)
                ? Path.Combine(Path.GetFullPath(persistentDataPath), BackendFolderName)
                : Path.Combine(directory.FullName, BackendFolderName);
        }

        public static string ResolveWindowsBackendRoot(string applicationDataPath, string persistentDataPath = null)
        {
            if (!string.IsNullOrWhiteSpace(persistentDataPath))
            {
                var persistentCandidate = Path.Combine(Path.GetFullPath(persistentDataPath), BackendFolderName);
                if (File.Exists(StartScriptPath(persistentCandidate, "windows")))
                {
                    return persistentCandidate;
                }
            }

            if (string.IsNullOrWhiteSpace(applicationDataPath))
            {
                return !string.IsNullOrWhiteSpace(persistentDataPath)
                    ? Path.Combine(Path.GetFullPath(persistentDataPath), BackendFolderName)
                    : string.Empty;
            }

            var dataPath = Path.GetFullPath(applicationDataPath);
            var directory = new DirectoryInfo(dataPath);
            if (directory.Name.EndsWith("_Data", StringComparison.OrdinalIgnoreCase)
                && directory.Parent != null)
            {
                var sibling = Path.Combine(directory.Parent.FullName, BackendFolderName);
                if (File.Exists(StartScriptPath(sibling, "windows")) || string.IsNullOrWhiteSpace(persistentDataPath))
                {
                    return sibling;
                }
            }

            return !string.IsNullOrWhiteSpace(persistentDataPath)
                ? Path.Combine(Path.GetFullPath(persistentDataPath), BackendFolderName)
                : Path.Combine(directory.FullName, BackendFolderName);
        }

        public static string ResolveBackendRoot(string applicationDataPath, string persistentDataPath = null)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return ResolveWindowsBackendRoot(applicationDataPath, persistentDataPath);
#else
            return ResolveMacBackendRoot(applicationDataPath, persistentDataPath);
#endif
        }

        public static string StartScriptPath(string backendRoot)
        {
            return StartScriptPath(backendRoot, CurrentDesktopPlatformKey());
        }

        public static string StartScriptPath(string backendRoot, string platform)
        {
            var relativePath = IsWindowsPlatform(platform)
                ? WindowsStartScriptRelativePath
                : MacStartScriptRelativePath;
            return Path.Combine(backendRoot ?? string.Empty, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static string OwnershipFilePath(string backendRoot)
        {
            return Path.Combine(backendRoot ?? string.Empty, "runtime", "unity-owned-pids.txt");
        }

        private static string CurrentDesktopPlatformKey()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return "windows";
#else
            return "macos";
#endif
        }

        private static bool IsWindowsPlatform(string platform)
        {
            if (string.IsNullOrWhiteSpace(platform))
            {
                return false;
            }

            var value = platform.Trim().Replace('-', '_').ToLowerInvariant();
            return value.StartsWith("win", StringComparison.Ordinal)
                || value.StartsWith("standalone_win", StringComparison.Ordinal)
                || value.StartsWith("windows", StringComparison.Ordinal);
        }
    }
}
