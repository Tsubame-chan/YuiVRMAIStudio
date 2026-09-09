using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Yui.AvatarBridge.Editor
{
    public sealed class YuiAvatarAnalysis
    {
        public GameObject Root;
        public Component Descriptor;
        public readonly List<AnimationClip> AnimationClips = new List<AnimationClip>();
        public readonly YuiAvatarDiagnostics Diagnostics = new YuiAvatarDiagnostics();
        public bool HasErrors => Diagnostics.issues.Any(issue => issue.severity == "error");
    }

    public static class YuiAvatarBridgeAnalyzer
    {
        private static readonly string[] DescriptorTypeNames =
        {
            "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor",
            "VRCSDK2.VRC_AvatarDescriptor",
        };

        private static readonly string[][] VowelCandidates =
        {
            new[] { "vrc.v_aa", "MTH_A", "Fcl_MTH_A", "mouth_a", "mouthA", "あ", "aa" },
            new[] { "vrc.v_ih", "MTH_I", "Fcl_MTH_I", "mouth_i", "mouthI", "い", "ih" },
            new[] { "vrc.v_ou", "MTH_U", "Fcl_MTH_U", "mouth_u", "mouthU", "う", "ou" },
            new[] { "vrc.v_e", "MTH_E", "Fcl_MTH_E", "mouth_e", "mouthE", "え" },
            new[] { "vrc.v_oh", "MTH_O", "Fcl_MTH_O", "mouth_o", "mouthO", "お", "oh" },
        };

        private static readonly string[] Vowels = { "aa", "ih", "ou", "ee", "oh" };
        private static readonly int[] OculusVowelIndices = { 10, 12, 14, 11, 13 };

        public static YuiAvatarAnalysis Analyze(GameObject selectedRoot)
        {
            var analysis = new YuiAvatarAnalysis { Root = selectedRoot };
            if (selectedRoot == null)
            {
                AddIssue(analysis, "error", "avatar.missing", "Hierarchyでアバターのルートを選択してください。");
                return analysis;
            }

            analysis.Descriptor = FindDescriptor(selectedRoot);
            analysis.Diagnostics.hasVrcAvatarDescriptor = analysis.Descriptor != null;
            if (analysis.Descriptor == null)
            {
                AddIssue(analysis, "error", "descriptor.missing", "選択対象にVRC Avatar Descriptorがありません。VCCのアバタールートを選択してください。");
            }

            var animator = selectedRoot.GetComponent<Animator>() ?? selectedRoot.GetComponentInChildren<Animator>(true);
            analysis.Diagnostics.hasHumanoidAnimator = animator != null && animator.avatar != null && animator.avatar.isHuman && animator.avatar.isValid;
            if (!analysis.Diagnostics.hasHumanoidAnimator)
            {
                AddIssue(analysis, "error", "animator.not_humanoid", "Humanoid Animatorが有効ではありません。Yuiで身体モーションを適用できません。");
            }

            var renderers = selectedRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null && renderer.sharedMesh != null)
                .ToArray();
            analysis.Diagnostics.skinnedMeshCount = renderers.Length;
            var materials = renderers
                .SelectMany(renderer => renderer.sharedMaterials ?? Array.Empty<Material>())
                .Where(material => material != null)
                .Distinct()
                .ToArray();
            analysis.Diagnostics.materialCount = materials.Length;
            analysis.Diagnostics.materials = materials.Select(material =>
            {
                var path = AssetDatabase.GetAssetPath(material);
                var shaderFound = material.shader != null
                    && !string.Equals(material.shader.name, "Hidden/InternalErrorShader", StringComparison.OrdinalIgnoreCase);
                return new YuiAvatarMaterialDiagnostic
                {
                    name = material.name,
                    shader = material.shader != null ? material.shader.name : string.Empty,
                    shaderFound = shaderFound,
                    assetGuid = AssetDatabase.AssetPathToGUID(path),
                };
            }).ToArray();
            analysis.Diagnostics.blendShapeCount = renderers.Sum(renderer => renderer.sharedMesh.blendShapeCount);
            if (renderers.Length == 0)
            {
                AddIssue(analysis, "error", "mesh.missing", "有効なSkinnedMeshRendererがありません。アバターを表示できません。");
            }
            var missingShaders = analysis.Diagnostics.materials.Where(material => !material.shaderFound).ToArray();
            if (missingShaders.Length > 0)
            {
                AddIssue(analysis, "error", "shader.missing", $"Shaderが見つからないMaterialが{missingShaders.Length}件あります。必要なShaderをVCCプロジェクトへ追加してください。");
            }

            analysis.Diagnostics.visemes = AnalyzeVisemes(selectedRoot, analysis.Descriptor, renderers);
            var foundVisemes = analysis.Diagnostics.visemes.Count(mapping => mapping.found);
            if (foundVisemes == 0)
            {
                AddIssue(analysis, "warning", "viseme.none", "母音BlendShapeを検出できません。Yuiで発話しても口が動きません。");
            }
            else if (foundVisemes < 5)
            {
                AddIssue(analysis, "warning", "viseme.partial", $"5母音のうち{foundVisemes}個を検出しました。不足母音は近い口形へフォールバックします。");
            }

            CollectAnimationClips(selectedRoot, analysis);
            analysis.Diagnostics.expressionClips = analysis.AnimationClips
                .Select((clip, index) =>
                {
                    var sourcePath = AssetDatabase.GetAssetPath(clip);
                    var category = ClassifyAnimation(sourcePath);
                    return new YuiAvatarExpressionClip
                    {
                        name = clip.name,
                        category = category,
                        emotion = category == "facial_expression" ? GuessEmotion(clip.name) : "unmapped",
                        address = $"animations/{index:D4}_{SafeName(clip.name)}.anim",
                        assetGuid = AssetDatabase.AssetPathToGUID(sourcePath),
                        sourcePath = sourcePath,
                    };
                })
                .ToArray();
            if (analysis.AnimationClips.Count == 0)
            {
                AddIssue(analysis, "warning", "animation.none", "AnimationClip候補が見つかりません。表情やジェスチャーの自動マッピングは行えません。");
            }

            analysis.Diagnostics.physBones = AnalyzePhysBones(selectedRoot);
            if (analysis.Diagnostics.physBones.Length > 0)
            {
                AddIssue(analysis, "info", "physbone.captured", $"PhysBoneを{analysis.Diagnostics.physBones.Length}件検出しました。設定を保存し、Yui runtime用の物理へ変換します。");
            }

            return analysis;
        }

        public static string StableAvatarId(GameObject root)
        {
            var global = root != null ? GlobalObjectId.GetGlobalObjectIdSlow(root).ToString() : "missing";
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(global)).Take(12).Select(value => value.ToString("x2")));
        }

        public static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "avatar";
            var chars = value.Select(character => char.IsLetterOrDigit(character) || character == '-' || character == '_' ? character : '_').ToArray();
            var result = new string(chars).Trim('_');
            while (result.Contains("__")) result = result.Replace("__", "_");
            return string.IsNullOrWhiteSpace(result) ? "avatar" : result;
        }

        private static Component FindDescriptor(GameObject root)
        {
            return root.GetComponentsInChildren<Component>(true).FirstOrDefault(component =>
                component != null && DescriptorTypeNames.Contains(component.GetType().FullName));
        }

        private static YuiAvatarVisemeMapping[] AnalyzeVisemes(GameObject root, Component descriptor, SkinnedMeshRenderer[] renderers)
        {
            var descriptorRenderer = GetMemberValue(descriptor, "VisemeSkinnedMesh", "visemeSkinnedMesh") as SkinnedMeshRenderer;
            var descriptorNames = ToStringArray(GetMemberValue(descriptor, "VisemeBlendShapes", "visemeBlendShapes"));
            var mappings = new List<YuiAvatarVisemeMapping>();
            for (var vowelIndex = 0; vowelIndex < Vowels.Length; vowelIndex++)
            {
                var mappedName = descriptorNames.Length > OculusVowelIndices[vowelIndex]
                    ? descriptorNames[OculusVowelIndices[vowelIndex]]
                    : string.Empty;
                var renderer = descriptorRenderer;
                var source = "VRC Avatar Descriptor";
                if (!HasBlendShape(renderer, mappedName))
                {
                    renderer = renderers.FirstOrDefault(candidate => TryFindBlendShape(candidate.sharedMesh, VowelCandidates[vowelIndex], out mappedName));
                    source = renderer != null ? "name fallback" : "missing";
                }

                mappings.Add(new YuiAvatarVisemeMapping
                {
                    vowel = Vowels[vowelIndex],
                    rendererPath = renderer != null ? RelativePath(root.transform, renderer.transform) : string.Empty,
                    blendShape = mappedName ?? string.Empty,
                    found = renderer != null && HasBlendShape(renderer, mappedName),
                    source = source,
                });
            }

            return mappings.ToArray();
        }

        private static void CollectAnimationClips(GameObject root, YuiAvatarAnalysis analysis)
        {
            var clips = new HashSet<AnimationClip>();
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            {
                AddControllerClips(animator.runtimeAnimatorController, clips);
            }

            foreach (var animation in root.GetComponentsInChildren<Animation>(true))
            {
                foreach (AnimationState state in animation)
                {
                    if (state?.clip != null) clips.Add(state.clip);
                }
            }

            if (analysis.Descriptor != null)
            {
                var serialized = new SerializedObject(analysis.Descriptor);
                var iterator = serialized.GetIterator();
                if (iterator.Next(true))
                {
                    do
                    {
                        if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            if (iterator.objectReferenceValue is RuntimeAnimatorController controller) AddControllerClips(controller, clips);
                            if (iterator.objectReferenceValue is AnimationClip clip) clips.Add(clip);
                        }
                    }
                    while (iterator.Next(true));
                }
            }

            analysis.AnimationClips.AddRange(clips
                .Where(clip => clip != null && !string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(clip)))
                .OrderBy(clip => clip.name, StringComparer.OrdinalIgnoreCase));
        }

        private static void AddControllerClips(RuntimeAnimatorController controller, HashSet<AnimationClip> clips)
        {
            if (controller == null) return;
            foreach (var clip in controller.animationClips)
            {
                if (clip != null) clips.Add(clip);
            }
        }

        private static YuiAvatarPhysBoneMapping[] AnalyzePhysBones(GameObject root)
        {
            return root.GetComponentsInChildren<Component>(true)
                .Where(component => component != null
                    && component.GetType().FullName != null
                    && component.GetType().FullName.EndsWith(".VRCPhysBone", StringComparison.Ordinal))
                .Select(component =>
                {
                    var boneRoot = GetMemberValue(component, "rootTransform", "RootTransform") as Transform ?? component.transform;
                    var colliders = GetMemberValue(component, "colliders", "Colliders") as ICollection;
                    return new YuiAvatarPhysBoneMapping
                    {
                        componentPath = RelativePath(root.transform, component.transform),
                        rootPath = RelativePath(root.transform, boneRoot),
                        affectedTransformCount = boneRoot != null ? boneRoot.GetComponentsInChildren<Transform>(true).Length : 0,
                        colliderCount = colliders?.Count ?? 0,
                        pull = GetFloat(component, "pull", "Pull"),
                        spring = GetFloat(component, "spring", "Spring"),
                        stiffness = GetFloat(component, "stiffness", "Stiffness"),
                        immobile = GetFloat(component, "immobile", "Immobile"),
                        gravity = GetFloat(component, "gravity", "Gravity"),
                        radius = GetFloat(component, "radius", "Radius"),
                    };
                })
                .ToArray();
        }

        private static object GetMemberValue(object target, params string[] names)
        {
            if (target == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                foreach (var name in names)
                {
                    var field = type.GetField(name, flags | BindingFlags.DeclaredOnly);
                    if (field != null) return field.GetValue(target);
                    var property = type.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                    if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(target);
                }
            }
            return null;
        }

        private static float GetFloat(object target, params string[] names)
        {
            var value = GetMemberValue(target, names);
            try { return value != null ? Convert.ToSingle(value) : 0f; }
            catch { return 0f; }
        }

        private static string[] ToStringArray(object value)
        {
            if (value is string[] strings) return strings;
            if (value is IEnumerable enumerable) return enumerable.Cast<object>().Select(item => item?.ToString() ?? string.Empty).ToArray();
            return Array.Empty<string>();
        }

        private static bool HasBlendShape(SkinnedMeshRenderer renderer, string name)
        {
            return renderer != null && renderer.sharedMesh != null && !string.IsNullOrWhiteSpace(name)
                && renderer.sharedMesh.GetBlendShapeIndex(name) >= 0;
        }

        private static bool TryFindBlendShape(Mesh mesh, IEnumerable<string> candidates, out string found)
        {
            found = string.Empty;
            if (mesh == null) return false;
            foreach (var candidate in candidates)
            {
                var normalized = Normalize(candidate);
                for (var index = 0; index < mesh.blendShapeCount; index++)
                {
                    var current = mesh.GetBlendShapeName(index);
                    var currentNormalized = Normalize(current);
                    if (string.Equals(current, candidate, StringComparison.OrdinalIgnoreCase)
                        || currentNormalized == normalized
                        || (normalized.Length > 1 && currentNormalized.EndsWith(normalized, StringComparison.OrdinalIgnoreCase)))
                    {
                        found = current;
                        return true;
                    }
                }
            }
            return false;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value
                .Replace("blendShape", string.Empty).Replace("_", string.Empty)
                .Replace(".", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static string GuessEmotion(string value)
        {
            var lower = (value ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(lower, "happy", "smile", "joy", "laugh", "fun", "喜", "笑", "楽")) return "happy";
            if (ContainsAny(lower, "angry", "anger", "mad", "怒")) return "angry";
            if (ContainsAny(lower, "sad", "cry", "troubled", "悲", "泣", "困")) return "sad";
            if (ContainsAny(lower, "surprise", "surprised", "驚")) return "surprised";
            if (ContainsAny(lower, "relax", "calm", "neutral", "idle", "通常", "標準")) return "relaxed";
            return "unmapped";
        }

        private static string ClassifyAnimation(string assetPath)
        {
            var normalized = (assetPath ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
            if (normalized.Contains("/facialexpression/")) return "facial_expression";
            if (normalized.Contains("/facialoption/")) return "facial_option";
            if (normalized.Contains("/costumeoption/")) return "wardrobe";
            if (normalized.Contains("/gesture/")) return "gesture";
            if (normalized.Contains("/locomotion/")) return "locomotion";
            return "animation";
        }

        private static bool ContainsAny(string value, params string[] terms) => terms.Any(value.Contains);

        private static string RelativePath(Transform root, Transform target)
        {
            if (root == null || target == null) return string.Empty;
            if (root == target) return string.Empty;
            var names = new Stack<string>();
            var current = target;
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return current == root ? string.Join("/", names) : string.Empty;
        }

        private static void AddIssue(YuiAvatarAnalysis analysis, string severity, string code, string message)
        {
            var issues = analysis.Diagnostics.issues.ToList();
            issues.Add(new YuiAvatarDiagnosticIssue { severity = severity, code = code, message = message });
            analysis.Diagnostics.issues = issues.ToArray();
        }
    }
}
