using System;
using UnityEngine;

namespace YuiPhysicalAI.Avatar
{
    public sealed class YuiAvatarPackageMetadata : MonoBehaviour
    {
        [Serializable]
        public sealed class Viseme
        {
            public string Vowel;
            public string RendererPath;
            public string BlendShape;
        }

        [SerializeField] private string avatarId;
        [SerializeField] private string displayName;
        [SerializeField] private string sourcePackagePath;
        [SerializeField] private Viseme[] visemes = Array.Empty<Viseme>();

        public string AvatarId => avatarId;
        public string DisplayName => displayName;
        public string SourcePackagePath => sourcePackagePath;
        public Viseme[] Visemes => visemes ?? Array.Empty<Viseme>();

        public void Configure(string id, string name, string packagePath, Viseme[] mappings)
        {
            avatarId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            sourcePackagePath = packagePath ?? string.Empty;
            visemes = mappings ?? Array.Empty<Viseme>();
        }

        public bool TryGetViseme(string vowel, out SkinnedMeshRenderer renderer, out string blendShape)
        {
            renderer = null;
            blendShape = string.Empty;
            foreach (var mapping in Visemes)
            {
                if (mapping == null || !string.Equals(mapping.Vowel, vowel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var target = string.IsNullOrWhiteSpace(mapping.RendererPath)
                    ? transform
                    : transform.Find(mapping.RendererPath);
                renderer = target != null ? target.GetComponent<SkinnedMeshRenderer>() : null;
                blendShape = mapping.BlendShape ?? string.Empty;
                return renderer != null && renderer.sharedMesh != null
                    && renderer.sharedMesh.GetBlendShapeIndex(blendShape) >= 0;
            }

            return false;
        }
    }
}
