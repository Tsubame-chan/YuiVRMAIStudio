using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace YuiPhysicalAI.Editor
{
    public static class YuiReleaseVersionMetadata
    {
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
