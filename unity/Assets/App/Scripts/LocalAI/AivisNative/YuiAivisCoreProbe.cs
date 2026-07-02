using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiAivisCoreProbe
    {
        private const string CatalogFileName = "aivis_voices.json";

        public static YuiAivisCoreStatus Evaluate(string rootPath, bool nativeRuntimeLinked, string platformName = null)
        {
            rootPath = rootPath ?? string.Empty;
            var catalogPath = Path.Combine(rootPath, CatalogFileName);
            var missing = new List<string>();

            if (!Directory.Exists(rootPath))
            {
                missing.Add("Aivis");
            }

            var catalogExists = File.Exists(catalogPath);
            if (!catalogExists)
            {
                missing.Add(CatalogFileName);
            }

            var modelsReady = false;
            if (catalogExists)
            {
                modelsReady = AreVoiceModelsReady(rootPath, missing);
            }

            var runtimeAssets = YuiAivisRuntimeAssets.Evaluate(rootPath, nativeRuntimeLinked, platformName);
            missing.AddRange(runtimeAssets.MissingComponents);

            var missingComponents = missing
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var runtimeReady = modelsReady && runtimeAssets.RuntimeReady && missingComponents.Length == 0;
            return new YuiAivisCoreStatus
            {
                Ok = catalogExists,
                ErrorCode = runtimeReady ? string.Empty : "runtime_unavailable",
                ErrorMessage = runtimeReady
                    ? string.Empty
                    : "Aivis models are present only when models_ready=true; runtime_ready also requires the embedded Style-Bert-VITS2 runtime, Japanese BERT, and text frontend assets.",
                RuntimeReady = runtimeReady,
                ModelsReady = modelsReady,
                RootPath = rootPath,
                CatalogPath = catalogPath,
                MissingComponents = missingComponents
            };
        }

        private static bool AreVoiceModelsReady(string rootPath, ICollection<string> missing)
        {
            var catalog = YuiAivisNativeVoiceCatalog.Load(rootPath);
            if (catalog.Voices == null || catalog.Voices.Length == 0)
            {
                missing.Add("voices");
                return false;
            }

            var allReady = true;
            foreach (var voice in catalog.Voices)
            {
                if (voice == null)
                {
                    allReady = false;
                    missing.Add("voices");
                    continue;
                }

                allReady &= RequireRelativeFile(rootPath, voice.ModelPath, missing);
                allReady &= RequireRelativeFile(rootPath, voice.HyperParametersPath, missing);
                allReady &= RequireRelativeFile(rootPath, voice.StyleVectorsPath, missing);
            }

            return allReady;
        }

        private static bool RequireRelativeFile(string rootPath, string relativePath, ICollection<string> missing)
        {
            if (!TrySafeRelativePath(relativePath, out var safeRelativePath))
            {
                missing.Add(string.IsNullOrWhiteSpace(relativePath) ? "voice_file" : "invalid_voice_path");
                return false;
            }

            if (File.Exists(Path.Combine(rootPath, safeRelativePath)))
            {
                return true;
            }

            missing.Add(safeRelativePath.Replace('\\', '/'));
            return false;
        }

        private static bool TrySafeRelativePath(string relativePath, out string safeRelativePath)
        {
            safeRelativePath = string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath)
                || Path.IsPathRooted(relativePath)
                || relativePath.IndexOf('\0') >= 0)
            {
                return false;
            }

            var normalized = relativePath.Replace('\\', '/');
            var segments = normalized.Split('/');
            foreach (var segment in segments)
            {
                if (segment == "..")
                {
                    return false;
                }
            }

            safeRelativePath = normalized;
            return true;
        }

    }
}
