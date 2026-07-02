using System;
using Newtonsoft.Json;

namespace YuiPhysicalAI.LocalAI
{
    [Serializable]
    public sealed class YuiAivisCoreStatus
    {
        [JsonProperty("ok")] public bool Ok { get; set; }
        [JsonProperty("error_code")] public string ErrorCode { get; set; }
        [JsonProperty("error_message")] public string ErrorMessage { get; set; }
        [JsonProperty("runtime_ready")] public bool RuntimeReady { get; set; }
        [JsonProperty("models_ready")] public bool ModelsReady { get; set; }
        [JsonProperty("root_path")] public string RootPath { get; set; }
        [JsonProperty("catalog_path")] public string CatalogPath { get; set; }
        [JsonProperty("missing_components")] public string[] MissingComponents { get; set; } = Array.Empty<string>();

        public static YuiAivisCoreStatus Error(
            string code,
            string message,
            string rootPath,
            string catalogPath,
            string[] missingComponents)
        {
            return new YuiAivisCoreStatus
            {
                Ok = false,
                ErrorCode = code,
                ErrorMessage = message,
                RuntimeReady = false,
                ModelsReady = false,
                RootPath = rootPath,
                CatalogPath = catalogPath,
                MissingComponents = missingComponents ?? Array.Empty<string>()
            };
        }
    }
}
