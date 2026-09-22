using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Yui.AvatarBridge.Editor
{
    public static class YuiAvatarBasicMaterials
    {
        public static Material Convert(Material source)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Unity Standard shader is unavailable.");
            var material = new Material(shader) { name = (source != null ? source.name : "Missing") + "_YuiBasic" };
            if (source == null) return material;
            if (source.HasProperty("_MainTex")) {
                material.mainTexture = source.GetTexture("_MainTex");
                material.mainTextureScale = source.GetTextureScale("_MainTex");
                material.mainTextureOffset = source.GetTextureOffset("_MainTex");
            }
            if (source.HasProperty("_Color")) material.color = source.GetColor("_Color");
            material.SetFloat("_Glossiness", 0f); material.SetFloat("_Metallic", 0f);
            if (source.renderQueue >= 3000)
            {
                material.SetFloat("_Mode", 2); material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_ALPHABLEND_ON"); material.renderQueue = 3000;
            }
            else if (source.renderQueue >= 2450)
            {
                material.SetFloat("_Mode", 1); material.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : .5f);
                material.EnableKeyword("_ALPHATEST_ON"); material.renderQueue = 2450;
            }
            return material;
        }
        public static void ApplyToClone(GameObject clone, string assetFolder)
        {
            var converted = new Dictionary<Material, Material>(); var count = 0;
            foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null) continue;
                    if (!converted.TryGetValue(original, out var basic)) {
                        basic = Convert(original);
                        AssetDatabase.CreateAsset(basic, $"{assetFolder}/material_{count++:D4}.mat"); converted.Add(original, basic);
                    }
                    materials[i] = basic;
                }
                renderer.sharedMaterials = materials;
            }
        }
    }
}
