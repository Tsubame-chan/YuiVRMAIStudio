using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Yui.AvatarBridge.Editor
{
    // Accept known naming conventions, not arbitrary suffixes (e.g. Angry != A).
    public static class YuiAvatarShapeNames
    {
        public static string Normalize(string value)
        {
            var name = Regex.Replace(value ?? "", @"^blendShape\d*[._ ]", "", RegexOptions.IgnoreCase);
            return name.Replace("_", "").Replace(".", "").Replace(" ", "").ToLowerInvariant();
        }

        public static int Find(Mesh mesh, params string[] names)
        {
            if (mesh == null) return -1;
            foreach (var name in names)
            {
                var exact = mesh.GetBlendShapeIndex(name);
                if (exact >= 0) return exact;
                var found = -1;
                for (var i = 0; i < mesh.blendShapeCount; i++)
                {
                    if (Normalize(mesh.GetBlendShapeName(i)) != Normalize(name)) continue;
                    if (found >= 0) return -1; // Ambiguous names are not a safe automatic mapping.
                    found = i;
                }
                if (found >= 0) return found;
            }
            return -1;
        }
    }
}
