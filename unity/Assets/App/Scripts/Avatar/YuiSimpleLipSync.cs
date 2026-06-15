using System;
using System.Collections.Generic;
using UniVRM10;
using UnityEngine;
using VRM;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Avatar
{
    public sealed class YuiSimpleLipSync : MonoBehaviour
    {
        [SerializeField] private AudioSource speechAudioSource;
        [SerializeField] private SkinnedMeshRenderer faceRenderer;
        [SerializeField] private string aaBlendShape = "vrc.v_aa";
        [SerializeField] private string ihBlendShape = "vrc.v_ih";
        [SerializeField] private string ouBlendShape = "vrc.v_ou";
        [SerializeField] private string eBlendShape = "vrc.v_e";
        [SerializeField] private string ohBlendShape = "vrc.v_oh";
        [SerializeField, Range(0f, 100f)] private float maxWeight = 82f;
        [SerializeField, Range(1f, 80f)] private float gain = 34f;
        [SerializeField, Range(0f, 100f)] private float fallbackSpeakingWeight = 58f;
        [SerializeField, Range(0.01f, 0.5f)] private float smoothingSeconds = 0.06f;
        [SerializeField] private bool autoFindFaceRenderer = true;

        private readonly Dictionary<string, int> blendShapeIndices =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly float[] samples = new float[256];
        private Vrm10Instance vrm10Instance;
        private VRMBlendShapeProxy vrm0BlendShapeProxy;
        private float currentWeight;
        private float velocity;
        private int aaIndex = -1;
        private int ihIndex = -1;
        private int ouIndex = -1;
        private int eIndex = -1;
        private int ohIndex = -1;
        private bool warnedMissingRenderer;
        private LipSyncTarget target = LipSyncTarget.DirectBlendShape;
        private static readonly string[] AaBlendShapeCandidates = { "vrc.v_aa", "MTH_A", "blendShape1.MTH_A", "Fcl_MTH_A", "Fcl_MTH_Angry", "mouth_a", "mouthA", "A", "あ", "aa" };
        private static readonly string[] IhBlendShapeCandidates = { "vrc.v_ih", "MTH_I", "blendShape1.MTH_I", "Fcl_MTH_I", "mouth_i", "mouthI", "I", "い", "ih" };
        private static readonly string[] OuBlendShapeCandidates = { "vrc.v_ou", "MTH_U", "blendShape1.MTH_U", "Fcl_MTH_U", "mouth_u", "mouthU", "U", "う", "ou" };
        private static readonly string[] EBlendShapeCandidates = { "vrc.v_e", "MTH_E", "blendShape1.MTH_E", "Fcl_MTH_E", "mouth_e", "mouthE", "E", "え" };
        private static readonly string[] OhBlendShapeCandidates = { "vrc.v_oh", "MTH_O", "blendShape1.MTH_O", "Fcl_MTH_O", "mouth_o", "mouthO", "O", "お", "oh" };
        private static readonly string[] MouthFallbackCandidates = { "mouth", "mth", "kuti", "kuchi", "口", "あ" };

        private enum LipSyncTarget
        {
            DirectBlendShape,
            Vrm10Expression,
            Vrm0BlendShapeProxy
        }

        private void Awake()
        {
            if (speechAudioSource == null)
            {
                speechAudioSource = GetComponent<AudioSource>();
            }

            ResolveRenderer();
            RebuildBlendShapeIndices();
        }

        private void OnDisable()
        {
            SetVisemes(0f, 0f, 0f, 0f, 0f);
        }

        private void LateUpdate()
        {
            if (speechAudioSource == null || (target == LipSyncTarget.DirectBlendShape && faceRenderer == null))
            {
                if (target == LipSyncTarget.DirectBlendShape && faceRenderer == null)
                {
                    ResolveRenderer();
                    RebuildBlendShapeIndices();
                    if (faceRenderer == null && !warnedMissingRenderer)
                    {
                        warnedMissingRenderer = true;
                        Debug.LogWarning("YuiSimpleLipSync could not find a facial renderer with supported viseme blendshapes.");
                    }
                }
                return;
            }

            var targetWeight = 0f;
            if (speechAudioSource.isPlaying)
            {
                targetWeight = Mathf.Clamp01(ReadVolume() * gain) * maxWeight;
                if (targetWeight < fallbackSpeakingWeight)
                {
                    targetWeight = fallbackSpeakingWeight;
                }
            }

            currentWeight = Mathf.SmoothDamp(
                currentWeight,
                targetWeight,
                ref velocity,
                smoothingSeconds);

            if (currentWeight < 0.1f)
            {
                SetVisemes(0f, 0f, 0f, 0f, 0f);
                return;
            }

            var phase = Time.time * 11f;
            var aa = currentWeight * (0.72f + Mathf.Sin(phase) * 0.18f);
            var ih = currentWeight * (0.18f + Mathf.Sin(phase * 1.37f + 1.1f) * 0.08f);
            var ou = currentWeight * (0.14f + Mathf.Sin(phase * 0.71f + 2.4f) * 0.08f);
            SetVisemes(aa, ih, ou, currentWeight * 0.08f, currentWeight * 0.1f);
        }

        public void Configure(GameObject avatarRoot, AudioSource audioSource)
        {
            speechAudioSource = audioSource;
            vrm10Instance = null;
            vrm0BlendShapeProxy = null;
            target = LipSyncTarget.DirectBlendShape;
            if (avatarRoot != null)
            {
                vrm10Instance = avatarRoot.GetComponentInChildren<Vrm10Instance>(true);
                if (vrm10Instance != null)
                {
                    target = LipSyncTarget.Vrm10Expression;
                    faceRenderer = null;
                }
                else
                {
                    vrm0BlendShapeProxy = avatarRoot.GetComponentInChildren<VRMBlendShapeProxy>(true);
                    if (vrm0BlendShapeProxy != null)
                    {
                        target = LipSyncTarget.Vrm0BlendShapeProxy;
                        faceRenderer = null;
                    }
                }
            }

            if (avatarRoot != null && target == LipSyncTarget.DirectBlendShape)
            {
                faceRenderer = FindFaceRenderer(avatarRoot);
            }

            RebuildBlendShapeIndices();
            warnedMissingRenderer = false;
            Debug.Log(target == LipSyncTarget.Vrm10Expression
                ? "YuiSimpleLipSync configured for VRM 1.0 Expression API."
                : target == LipSyncTarget.Vrm0BlendShapeProxy
                    ? "YuiSimpleLipSync configured for VRM 0.x BlendShapeProxy."
                    : faceRenderer != null
                        ? $"YuiSimpleLipSync configured: renderer={faceRenderer.name}, aa={aaIndex}, ih={ihIndex}, ou={ouIndex}, e={eIndex}, oh={ohIndex}"
                        : "YuiSimpleLipSync configured without a usable face renderer.");
        }

        public void SetSpeechAudioSource(AudioSource audioSource)
        {
            speechAudioSource = audioSource;
        }

        private float ReadVolume()
        {
            speechAudioSource.GetOutputData(samples, 0);
            var sum = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * samples[i];
            }

            return Mathf.Sqrt(sum / samples.Length);
        }

        private void ResolveRenderer()
        {
            if (faceRenderer != null || !autoFindFaceRenderer)
            {
                return;
            }

            faceRenderer = FindFaceRenderer(gameObject);
        }

        private SkinnedMeshRenderer FindFaceRenderer(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            var best = FindBestFaceRenderer(root.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            if (best != null)
            {
                return best;
            }

            return FindBestFaceRenderer(YuiSceneObjectFinder.FindAll<SkinnedMeshRenderer>(true));
        }

        private SkinnedMeshRenderer FindBestFaceRenderer(SkinnedMeshRenderer[] renderers)
        {
            SkinnedMeshRenderer best = null;
            var bestScore = int.MinValue;
            if (renderers == null)
            {
                return null;
            }

            foreach (var renderer in renderers)
            {
                var mesh = renderer != null ? renderer.sharedMesh : null;
                if (mesh == null || mesh.blendShapeCount == 0)
                {
                    continue;
                }

                var score = LipSyncBlendShapeScore(mesh);
                var lowerName = renderer.name.ToLowerInvariant();
                if (lowerName.Contains("face") || lowerName.Contains("body") || lowerName.Contains("head"))
                {
                    score += 25;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = renderer;
                }
            }

            return bestScore > 0 ? best : null;
        }

        private int LipSyncBlendShapeScore(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0;
            }

            var score = 0;
            if (FindBlendShape(mesh, aaBlendShape, AaBlendShapeCandidates) >= 0) score += 100;
            if (FindBlendShape(mesh, ihBlendShape, IhBlendShapeCandidates) >= 0) score += 30;
            if (FindBlendShape(mesh, ouBlendShape, OuBlendShapeCandidates) >= 0) score += 30;
            if (FindBlendShape(mesh, eBlendShape, EBlendShapeCandidates) >= 0) score += 15;
            if (FindBlendShape(mesh, ohBlendShape, OhBlendShapeCandidates) >= 0) score += 15;
            if (FindBlendShape(mesh, "mouth", MouthFallbackCandidates) >= 0) score += 10;
            return score;
        }

        private void RebuildBlendShapeIndices()
        {
            blendShapeIndices.Clear();
            aaIndex = -1;
            ihIndex = -1;
            ouIndex = -1;
            eIndex = -1;
            ohIndex = -1;

            if (target != LipSyncTarget.DirectBlendShape)
            {
                return;
            }

            var mesh = faceRenderer != null ? faceRenderer.sharedMesh : null;
            if (mesh == null)
            {
                return;
            }

            for (var i = 0; i < mesh.blendShapeCount; i++)
            {
                blendShapeIndices[mesh.GetBlendShapeName(i)] = i;
            }

            aaIndex = FindBlendShape(mesh, aaBlendShape, AaBlendShapeCandidates);
            ihIndex = FindBlendShape(mesh, ihBlendShape, IhBlendShapeCandidates);
            ouIndex = FindBlendShape(mesh, ouBlendShape, OuBlendShapeCandidates);
            eIndex = FindBlendShape(mesh, eBlendShape, EBlendShapeCandidates);
            ohIndex = FindBlendShape(mesh, ohBlendShape, OhBlendShapeCandidates);

            var fallbackIndex = aaIndex >= 0
                ? aaIndex
                : FirstValidIndex(ihIndex, ouIndex, eIndex, ohIndex, FindBlendShape(mesh, "mouth", MouthFallbackCandidates));
            if (aaIndex < 0) aaIndex = fallbackIndex;
            if (ihIndex < 0) ihIndex = fallbackIndex;
            if (ouIndex < 0) ouIndex = fallbackIndex;
            if (eIndex < 0) eIndex = fallbackIndex;
            if (ohIndex < 0) ohIndex = fallbackIndex;
        }

        private static int FirstValidIndex(params int[] indices)
        {
            foreach (var index in indices)
            {
                if (index >= 0)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindBlendShape(Mesh mesh, string blendShapeName, string[] candidates = null)
        {
            if (mesh == null || string.IsNullOrWhiteSpace(blendShapeName))
            {
                return -1;
            }

            if (TryFindBlendShapeByName(mesh, blendShapeName, out var index))
            {
                return index;
            }

            if (candidates == null)
            {
                return -1;
            }

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (TryFindBlendShapeByName(mesh, candidate, out index))
                {
                    return index;
                }
            }

            return -1;
        }

        private bool TryFindBlendShapeByName(Mesh mesh, string blendShapeName, out int index)
        {
            index = -1;
            if (mesh == null || string.IsNullOrWhiteSpace(blendShapeName))
            {
                return false;
            }

            if (mesh == (faceRenderer != null ? faceRenderer.sharedMesh : null)
                && blendShapeIndices.TryGetValue(blendShapeName, out index))
            {
                return true;
            }

            index = mesh.GetBlendShapeIndex(blendShapeName);
            if (index >= 0)
            {
                return true;
            }

            var normalizedTarget = NormalizeBlendShapeName(blendShapeName);
            for (var i = 0; i < mesh.blendShapeCount; i++)
            {
                var currentName = mesh.GetBlendShapeName(i);
                var normalizedCurrent = NormalizeBlendShapeName(currentName);
                if (string.Equals(currentName, blendShapeName, StringComparison.OrdinalIgnoreCase)
                    || currentName.EndsWith("." + blendShapeName, StringComparison.OrdinalIgnoreCase)
                    || normalizedCurrent == normalizedTarget
                    || (normalizedTarget.Length > 1
                        && normalizedCurrent.EndsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase)))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeBlendShapeName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace("blendShape", string.Empty)
                    .Replace("_", string.Empty)
                    .Replace(".", string.Empty)
                    .Replace(" ", string.Empty)
                    .ToLowerInvariant();
        }

        private void SetVisemes(float aa, float ih, float ou, float e, float oh)
        {
            if (target == LipSyncTarget.Vrm10Expression && vrm10Instance != null)
            {
                vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Aa, Mathf.Clamp01(aa / 100f));
                vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Ih, Mathf.Clamp01(ih / 100f));
                vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Ou, Mathf.Clamp01(ou / 100f));
                vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Ee, Mathf.Clamp01(e / 100f));
                vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Oh, Mathf.Clamp01(oh / 100f));
                return;
            }

            if (target == LipSyncTarget.Vrm0BlendShapeProxy && vrm0BlendShapeProxy != null)
            {
                vrm0BlendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.A), Mathf.Clamp01(aa / 100f));
                vrm0BlendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.I), Mathf.Clamp01(ih / 100f));
                vrm0BlendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.U), Mathf.Clamp01(ou / 100f));
                vrm0BlendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.E), Mathf.Clamp01(e / 100f));
                vrm0BlendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.O), Mathf.Clamp01(oh / 100f));
                return;
            }

            SetBlendShape(aaIndex, aa);
            SetBlendShape(ihIndex, ih);
            SetBlendShape(ouIndex, ou);
            SetBlendShape(eIndex, e);
            SetBlendShape(ohIndex, oh);
        }

        private void SetBlendShape(int index, float weight)
        {
            if (faceRenderer != null && index >= 0)
            {
                faceRenderer.SetBlendShapeWeight(index, Mathf.Clamp(weight, 0f, 100f));
            }
        }
    }
}
