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
            new[] { "vrc.v_aa", "MTH_A", "Fcl_MTH_A", "mouth_a", "mouthA", "あ", "aa", "A" },
            new[] { "vrc.v_ih", "MTH_I", "Fcl_MTH_I", "mouth_i", "mouthI", "い", "ih", "I" },
            new[] { "vrc.v_ou", "MTH_U", "Fcl_MTH_U", "mouth_u", "mouthU", "う", "ou", "U" },
            new[] { "vrc.v_e", "MTH_E", "Fcl_MTH_E", "mouth_e", "mouthE", "え", "E" },
            new[] { "vrc.v_oh", "MTH_O", "Fcl_MTH_O", "mouth_o", "mouthO", "お", "oh", "O" },
        };

        private static readonly string[] Vowels = { "aa", "ih", "ou", "ee", "oh" };
        private static readonly int[] OculusVowelIndices = { 10, 12, 14, 11, 13 };

        public static YuiAvatarAnalysis Analyze(GameObject selectedRoot, bool portable = false)
        {
            var analysis = new YuiAvatarAnalysis { Root = selectedRoot };
            if (selectedRoot == null)
            {
                AddIssue(analysis, "error", "avatar.missing", "Hierarchyでアバターのルートを選択してください。");
                return analysis;
            }

            foreach (var transform in selectedRoot.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    AddIssue(analysis, "error", "script.missing", $"{transform.name} に参照切れのスクリプトがあります。元プロジェクトで必要なパッケージを復元してください。");
            }
            if (RequiresSourceBake(selectedRoot))
                AddIssue(analysis, portable ? "info" : "error", "source.unbaked", "Modular Avatar等の未処理コンポーネントがあります。このままでは衣装・ボーン・表情の改変が失われます。Yuiの共通ファイル書出しではコピーを自動加工します。旧ZIP書出しでは加工済みコピーが必要です。");
            if (portable && selectedRoot.GetComponentsInChildren<Component>(true).Any(c => c != null &&
                (c.GetType().FullName?.StartsWith("VF.", StringComparison.Ordinal) == true || c.GetType().FullName?.StartsWith("VRCFury.", StringComparison.Ordinal) == true)))
                AddIssue(analysis, "error", "pipeline.unsupported", "VRCFuryの未加工コンポーネントがあります。この加工経路は未対応のため、そのまま削除して書き出すことはできません。");

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
                .Where(renderer => renderer != null && renderer.sharedMesh != null
                    && (!portable || (renderer.enabled && renderer.gameObject.activeInHierarchy)))
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

            AddIssue(analysis, "warning", "material.basic", "Yui用の基本Materialへ変換します。ベース色・テクスチャ・透過を引き継ぎますが、独自Shader、輪郭線、特殊効果は再現しません。元Materialは変更しません。");

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

            if (analysis.AnimationClips.Count > 0)
                AddIssue(analysis, "warning", "animation.runtime_unavailable", "Yuiは元の表情アニメーション・FX・衣装メニューを実行しません。基本表情はYui側の対応BlendShapeに限られます。");
            var constraints = selectedRoot.GetComponentsInChildren<Component>(true).Count(component => component != null && component.GetType().Name.Contains("Constraint"));
            if (constraints > 0)
                AddIssue(analysis, "warning", "constraint.removed", $"Constraintを{constraints}件検出しました。追従する小物・衣装・ギミックの拘束はYuiでは動作しません。");
            analysis.Diagnostics.physBones = AnalyzePhysBones(selectedRoot);
            if (analysis.Diagnostics.physBones.Length > 0)
            {
                AddIssue(analysis, "warning", "physbone.approximation", $"PhysBoneを{analysis.Diagnostics.physBones.Length}件検出しました。対応Yuiでは回転・移動に反応する簡易の揺れとして扱います。衝突・つかむ操作・元PhysBoneの完全再現には非対応です。");
            }

            return analysis;
        }

        public static bool RequiresSourceBake(GameObject root)
        {
            return root != null && root.GetComponentsInChildren<Component>(true).Any(component =>
                component != null && IsBuildTimeComponent(component.GetType().FullName));
        }

        public static bool IsBuildTimeComponent(string typeName)
        {
            return !string.IsNullOrEmpty(typeName) && (
                typeName.StartsWith("nadena.dev.modular_avatar.", StringComparison.Ordinal)
                || typeName.StartsWith("nadena.dev.ndmf.", StringComparison.Ordinal));
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

        public static YuiAvatarVisemeMapping[] AnalyzeVisemes(GameObject root, object descriptor, SkinnedMeshRenderer[] renderers)
        {
            var descriptorRenderer = GetMemberValue(descriptor, "VisemeSkinnedMesh", "visemeSkinnedMesh") as SkinnedMeshRenderer;
            var descriptorNames = ToStringArray(GetMemberValue(descriptor, "VisemeBlendShapes", "visemeBlendShapes"));
            var mode = GetMemberValue(descriptor, "lipSync")?.ToString();
            var mouthOpen = GetMemberValue(descriptor, "MouthOpenBlendShapeName") as string;
            var mappings = new List<YuiAvatarVisemeMapping>();
            for (var vowelIndex = 0; vowelIndex < Vowels.Length; vowelIndex++)
            {
                var mappedName = mode == "JawFlapBlendShape" ? mouthOpen
                    : (mode == null || mode == "VisemeBlendShape") && descriptorNames.Length > OculusVowelIndices[vowelIndex]
                        ? descriptorNames[OculusVowelIndices[vowelIndex]] : string.Empty;
                var renderer = descriptorRenderer;
                var source = mode == "JawFlapBlendShape" ? "VRC mouth open" : "VRC Avatar Descriptor";
                if (!renderers.Contains(renderer) || !HasBlendShape(renderer, mappedName))
                {
                    renderer = null; mappedName = string.Empty; source = "missing";
                    // Bone/parameter-only modes cannot be reproduced by guessing arbitrary mesh names.
                    if (mode != "JawFlapBone" && mode != "VisemeParameterOnly")
                    {
                        var matches = renderers.Select(r => (renderer: r, index: YuiAvatarShapeNames.Find(r.sharedMesh, VowelCandidates[vowelIndex])))
                            .Where(pair => pair.index >= 0).ToArray();
                        if (matches.Length == 1) { renderer = matches[0].renderer; mappedName = renderer.sharedMesh.GetBlendShapeName(matches[0].index); source = "known name"; }
                        else if (matches.Length > 1) source = "ambiguous";
                    }
                }
                mappings.Add(new YuiAvatarVisemeMapping
                {
                    vowel = Vowels[vowelIndex], rendererPath = renderer != null ? RelativePath(root.transform, renderer.transform) : string.Empty,
                    blendShape = mappedName ?? string.Empty, found = renderer != null && HasBlendShape(renderer, mappedName), source = source,
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
                        ignorePaths = (GetMemberValue(component, "ignoreTransforms", "IgnoreTransforms") as IEnumerable)?.Cast<object>()
                            .OfType<Transform>().Select(item => RelativePath(root.transform, item)).ToArray() ?? Array.Empty<string>(),
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

        public static object GetMemberValue(object target, params string[] names)
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

        public static void AddIssue(YuiAvatarAnalysis analysis, string severity, string code, string message)
        {
            var issues = analysis.Diagnostics.issues.ToList();
            issues.Add(new YuiAvatarDiagnosticIssue { severity = severity, code = code, message = message });
            analysis.Diagnostics.issues = issues.ToArray();
        }
    }
}
