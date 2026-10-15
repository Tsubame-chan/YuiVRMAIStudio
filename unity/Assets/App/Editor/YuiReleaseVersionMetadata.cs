using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace YuiPhysicalAI.Editor
{
    public static class YuiReleaseVersionMetadata
    {
        public static void SyncRuntimeMetadata()
        {
            var value = new JObject { ["desktop_tag"] = Read("desktop_tag"), ["ios_version"] = Read("ios_version"), ["ios_build"] = Read("ios_build") };
            const string asset = "Assets/App/Resources/YuiReleaseVersions.json";
            var path = Path.Combine(Application.dataPath, "App/Resources/YuiReleaseVersions.json");
            var text = value.ToString(Newtonsoft.Json.Formatting.Indented) + "\n";
            if (!File.Exists(path) || File.ReadAllText(path) != text)
            {
                File.WriteAllText(path, text);
                UnityEditor.AssetDatabase.ImportAsset(asset, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
            }
        }

        public static string Read(string field)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../backend/app/core/release_versions.json"));
            var value = (string)JObject.Parse(File.ReadAllText(path))[field];
            var pattern = field == "desktop_tag" ? @"^v[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$"
                : field == "ios_build" ? @"^[1-9][0-9]*$" : @"^[0-9]+\.[0-9]+\.[0-9]+$";
            if (string.IsNullOrEmpty(value) || !Regex.IsMatch(value, pattern))
                throw new InvalidOperationException("Invalid release metadata: " + field + " in " + path);
            return value;
        }
    }
}
