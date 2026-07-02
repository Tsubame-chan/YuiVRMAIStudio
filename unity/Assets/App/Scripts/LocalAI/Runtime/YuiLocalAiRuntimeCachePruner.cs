using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiLocalAiRuntimeCachePruner
    {
        private const string RuntimeCacheRootDirectoryName = "RuntimeCache";

        public static void PruneForActivePack(YuiLocalAiModelPack activePack, string activeCacheDirectory)
        {
            if (activePack == null || string.IsNullOrWhiteSpace(activeCacheDirectory))
            {
                return;
            }

            if (!IsMobileRuntime())
            {
                return;
            }

            try
            {
                var root = Directory.GetParent(activeCacheDirectory)?.FullName;
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    return;
                }

                if (!string.Equals(
                        new DirectoryInfo(root).Name,
                        RuntimeCacheRootDirectoryName,
                        StringComparison.Ordinal))
                {
                    Debug.LogWarning($"Yui local AI cache prune skipped unexpected root: {root}");
                    return;
                }

                var activeFullPath = Path.GetFullPath(activeCacheDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (var directory in Directory.GetDirectories(root))
                {
                    var fullPath = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (string.Equals(fullPath, activeFullPath, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    SafeDeleteDirectory(directory);
                }

                if (!Directory.Exists(activeCacheDirectory))
                {
                    return;
                }

                PruneActiveDirectory(activeCacheDirectory);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Yui local AI cache prune failed: {ex.Message}");
            }
        }

        private static void PruneActiveDirectory(string cacheDirectory)
        {
            var files = Directory.GetFiles(cacheDirectory)
                .Select(path => new FileInfo(path))
                .Where(info => info.Exists)
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .ToList();
            if (files.Count <= 2)
            {
                return;
            }

            var keep = files
                .GroupBy(CacheKind)
                .Select(group => group.First())
                .ToHashSet();

            foreach (var file in files)
            {
                if (keep.Contains(file))
                {
                    continue;
                }

                SafeDeleteFile(file);
            }
        }

        private static string CacheKind(FileInfo file)
        {
            var name = file.Name;
            if (name.Contains("program_cache", StringComparison.OrdinalIgnoreCase))
            {
                return "program";
            }

            if (name.Contains("xnnpack_cache", StringComparison.OrdinalIgnoreCase))
            {
                return "xnnpack";
            }

            return "weight";
        }

        private static void SafeDeleteDirectory(string path)
        {
            try
            {
                Directory.Delete(path, true);
                Debug.Log($"Yui local AI cache pruned directory: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Yui local AI cache directory prune skipped: {path}, error={ex.Message}");
            }
        }

        private static void SafeDeleteFile(FileInfo file)
        {
            try
            {
                var length = file.Length;
                file.Delete();
                Debug.Log($"Yui local AI cache pruned file: {file.FullName}, bytes={length}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Yui local AI cache file prune skipped: {file.FullName}, error={ex.Message}");
            }
        }

        private static bool IsMobileRuntime()
        {
            return Application.platform == RuntimePlatform.IPhonePlayer
                || Application.platform == RuntimePlatform.Android;
        }
    }
}
