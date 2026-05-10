using System;
using System.Collections.Generic;
using UnityEngine;

namespace YuiPhysicalAI.Core
{
    public static class YuiAnimationCatalog
    {
        private const string ResourcePath = "yui_animation_catalog";

        private static readonly string[] DefaultFaces =
        {
            "Neutral",
            "Joy",
            "Fun",
            "Angry",
            "Sorrow",
            "Surprised",
        };

        private static readonly string[] DefaultAnimations =
        {
            "idle_normal",
            "idle_relaxed",
            "nod_small",
            "nod_big",
            "wave_small",
            "wave_big",
            "thinking",
            "surprised_body",
            "happy_body",
            "troubled_body",
            "proud_pose",
            "tsukkomi_point",
            "look_away",
            "talk_gesture_small",
        };

        private static HashSet<string> faceSet;
        private static HashSet<string> animationSet;

        public static bool IsKnownFace(string face)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(face) && faceSet.Contains(face);
        }

        public static bool IsKnownAnimation(string animation)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(animation) && animationSet.Contains(animation);
        }

        private static void EnsureLoaded()
        {
            if (faceSet != null && animationSet != null)
            {
                return;
            }

            var faces = DefaultFaces;
            var animations = DefaultAnimations;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset != null)
            {
                try
                {
                    var catalog = JsonUtility.FromJson<CatalogData>(asset.text);
                    if (catalog?.faces != null && catalog.faces.Length > 0)
                    {
                        faces = catalog.faces;
                    }
                    if (catalog?.animations != null && catalog.animations.Length > 0)
                    {
                        animations = catalog.animations;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Yui animation catalog could not be parsed; using defaults. {ex.Message}");
                }
            }

            faceSet = new HashSet<string>(faces, StringComparer.OrdinalIgnoreCase);
            animationSet = new HashSet<string>(animations, StringComparer.OrdinalIgnoreCase);
        }

        [Serializable]
        private sealed class CatalogData
        {
            public string[] faces;
            public string[] animations;
        }
    }
}
