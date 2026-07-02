using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiAivisRuntimeAssets
    {
        public const string OnnxRuntimeComponent = "onnxruntime";
        public const string StyleBertVits2RuntimeComponent = "style_bert_vits2_runtime";
        public const string JapaneseBertOnnxComponent = "japanese_bert_onnx";
        public const string JapaneseBertTokenizerComponent = "japanese_bert_tokenizer";
        public const string JapaneseTextFrontendComponent = "japanese_text_frontend";

        private static readonly RuntimeAssetRequirement[] Requirements =
        {
            new RuntimeAssetRequirement(OnnxRuntimeComponent, "Runtime/ONNXRuntime/manifest.json", manifestMustBeReady: true),
            new RuntimeAssetRequirement(StyleBertVits2RuntimeComponent, "Runtime/StyleBertVits2/manifest.json", manifestMustBeReady: true),
            new RuntimeAssetRequirement(JapaneseBertOnnxComponent, "Runtime/JapaneseBert/model_fp16.onnx"),
            new RuntimeAssetRequirement(JapaneseBertTokenizerComponent, "Runtime/JapaneseBert/tokenizer.json"),
            new RuntimeAssetRequirement(JapaneseTextFrontendComponent, "Runtime/JapaneseTextFrontend/manifest.json", manifestMustBeReady: true)
        };

        public static YuiAivisRuntimeAssetReport Evaluate(string rootPath, bool nativeRuntimeLinked, string platformName = null)
        {
            rootPath = rootPath ?? string.Empty;
            var missing = new List<string>();

            if (!nativeRuntimeLinked)
            {
                missing.Add(StyleBertVits2RuntimeComponent);
            }

            foreach (var requirement in Requirements)
            {
                if (requirement.ComponentName == StyleBertVits2RuntimeComponent && !nativeRuntimeLinked)
                {
                    continue;
                }

                var path = Path.Combine(rootPath, requirement.RelativePath);
                if (!File.Exists(path) || (requirement.ManifestMustBeReady && !ManifestIsReady(path, platformName)))
                {
                    missing.Add(requirement.ComponentName);
                }
            }

            var missingComponents = missing
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return new YuiAivisRuntimeAssetReport
            {
                RuntimeReady = nativeRuntimeLinked && missingComponents.Length == 0,
                MissingComponents = missingComponents
            };
        }

        public static string[] RequiredComponentNames()
        {
            return Requirements
                .Select(requirement => requirement.ComponentName)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private readonly struct RuntimeAssetRequirement
        {
            public RuntimeAssetRequirement(
                string componentName,
                string relativePath,
                bool manifestMustBeReady = false)
            {
                ComponentName = componentName;
                RelativePath = relativePath;
                ManifestMustBeReady = manifestMustBeReady;
            }

            public string ComponentName { get; }
            public string RelativePath { get; }
            public bool ManifestMustBeReady { get; }
        }

        private static bool ManifestIsReady(string path, string platformName)
        {
            try
            {
                var manifest = JObject.Parse(File.ReadAllText(path));
                if (string.Equals(
                    manifest.Value<string>("status"),
                    "ready",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.IsNullOrWhiteSpace(platformName))
                {
                    return false;
                }

                var readyPlatforms = manifest["ready_platforms"] as JArray;
                return readyPlatforms != null && readyPlatforms
                    .Values<string>()
                    .Any(value => string.Equals(value, platformName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
    }

    public sealed class YuiAivisRuntimeAssetReport
    {
        public bool RuntimeReady { get; set; }
        public string[] MissingComponents { get; set; } = Array.Empty<string>();
    }
}
