using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    // Extension point for future reviewed LiteRT-LM artifacts. Registration does
    // not download or select a model, and cannot replace a bundled definition.
    public static class YuiLocalModelCatalog
    {
        public static string CatalogPath => Path.Combine(Application.persistentDataPath, "YuiLocalAI", "additional-models.json");

        public static IEnumerable<YuiLocalAiModelPack> Merge(IEnumerable<YuiLocalAiModelPack> builtins)
        {
            var result = builtins.ToList();
            if (!File.Exists(CatalogPath)) return result;
            try
            {
                var manifest = JsonConvert.DeserializeObject<YuiLocalAiModelPackManifest>(File.ReadAllText(CatalogPath));
                foreach (var pack in manifest?.Packs ?? new List<YuiLocalAiModelPack>())
                {
                    if (!TryValidate(pack, result, out var reason))
                    {
                        Debug.LogWarning("Additional local model ignored: " + reason);
                        continue;
                    }
                    result.Add(pack);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException)
            { Debug.LogWarning("Additional local model catalog could not be read: " + ex.Message); }
            return result;
        }

        public static bool TryValidate(YuiLocalAiModelPack pack, IEnumerable<YuiLocalAiModelPack> existing, out string reason)
        {
            reason = "Invalid local model registration.";
            if (pack == null || !Regex.IsMatch(pack.Id ?? "", "^[a-zA-Z0-9_-]{1,80}$")) return false;
            if (string.IsNullOrWhiteSpace(pack.DisplayName) || pack.DisplayName.Length > 100) return false;
            if (!string.Equals(pack.Format, "litert-lm", StringComparison.OrdinalIgnoreCase)
                || pack.DeploymentKind != YuiLocalAiDeploymentKind.OnDeviceEmbedded
                || !(pack.Capabilities?.Contains(YuiLocalAiCapability.Chat) ?? false)) return false;
            if (!Regex.IsMatch(pack.RuntimeModelRef ?? "", "^[a-zA-Z0-9_.-]+\\.litertlm$")
                || pack.RuntimeModelRef.Contains("..")) return false;
            if ((existing ?? Array.Empty<YuiLocalAiModelPack>()).Any(p =>
                string.Equals(p.Id, pack.Id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(YuiLocalAiModelPathResolver.ModelFileName(p), pack.RuntimeModelRef, StringComparison.OrdinalIgnoreCase)))
            { reason = "The model ID or file name is already registered."; return false; }
            if (!Regex.IsMatch(pack.Sha256 ?? "", "^[a-fA-F0-9]{64}$")
                || !Uri.TryCreate(pack.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(uri.UserInfo)) return false;
            if (pack.ThinkingTokenBudget < 1 || pack.ThinkingTokenBudget > 768
                || pack.WorkThinkingTokenBudget < 1 || pack.WorkThinkingTokenBudget > 768
                || pack.TalkOutputTokenBudget < 256 || pack.TalkOutputTokenBudget > 2304
                || pack.WorkOutputTokenBudget < 256 || pack.WorkOutputTokenBudget > 2304
                || pack.Platforms == null || pack.Platforms.Length == 0) return false;
            reason = null;
            return true;
        }
    }
}
