using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiLocalAiPathResolver
    {
        public static string AivisRootPath()
        {
            return ResolvePackagedRoot("Aivis");
        }

        public static string VoicevoxRootPath()
        {
            return ResolvePackagedRoot("Voicevox");
        }

        public static string ResolvePackagedRoot(string packageName)
        {
            foreach (var root in CandidateLocalAiRoots())
            {
                var candidate = Path.Combine(root, packageName);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(NormalizeLocalPath(Application.streamingAssetsPath), "YuiLocalAI", packageName);
        }

        public static string DebugCandidateSummary()
        {
            var parts = new List<string>();
            foreach (var root in CandidateLocalAiRoots())
            {
                parts.Add($"{root}:exists={Directory.Exists(root)}");
            }

            return string.Join(" | ", parts);
        }

        private static IEnumerable<string> CandidateLocalAiRoots()
        {
            var streamingAssets = NormalizeLocalPath(Application.streamingAssetsPath);
            if (!string.IsNullOrWhiteSpace(streamingAssets))
            {
                yield return Path.Combine(streamingAssets, "YuiLocalAI");
            }

            var dataPath = NormalizeLocalPath(Application.dataPath);
            if (!string.IsNullOrWhiteSpace(dataPath))
            {
                yield return Path.Combine(dataPath, "Raw", "YuiLocalAI");
                yield return Path.Combine(dataPath, "Data", "Raw", "YuiLocalAI");

                var parent = Directory.GetParent(dataPath)?.FullName;
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    yield return Path.Combine(parent, "Data", "Raw", "YuiLocalAI");
                    yield return Path.Combine(parent, "Raw", "YuiLocalAI");
                }
            }
        }

        private static string NormalizeLocalPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                return uri.LocalPath;
            }

            return path;
        }
    }
}
