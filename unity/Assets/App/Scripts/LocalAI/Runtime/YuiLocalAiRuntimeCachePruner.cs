using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiLocalAiRuntimeCachePruner
    {
        private const string RuntimeCacheRootDirectoryName = "RuntimeCache";
        private static readonly Regex ModelRevision = new Regex(
            @"(?<=\.litertlm)_\d+_\d+(?=(?:\.mtp_drafter)?_mldrift_(?:program|weight)_cache\.bin$)",
            RegexOptions.CultureInvariant);

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

                // Registered models may coexist and be selected again. Do not
                // evict another model's compiled weights merely by switching.
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
            // The main model and MTP draft each need their own program AND weight cache.
            // Only coalesce known revisioned filenames. Unknown/audio/vision files are
            // not interchangeable and must not be deleted as generic "weight" entries.
            return ModelRevision.Replace(file.Name, string.Empty);
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
