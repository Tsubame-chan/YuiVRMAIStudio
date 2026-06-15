using System;
using System.Collections.Generic;
using UnityEngine;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Avatar
{
    public sealed class YuiAvatarController : MonoBehaviour
    {
        [Serializable]
        public sealed class FaceBinding
        {
            public string face = "Neutral";
            public string blendShapeName = "";
            [Range(0f, 100f)] public float weight = 100f;
            public List<FaceShape> shapes = new List<FaceShape>();
        }

        [Serializable]
        public sealed class FaceShape
        {
            public string blendShapeName = "";
            [Range(0f, 100f)] public float weight = 100f;
        }

        [Serializable]
        public sealed class AnimationBinding
        {
            public string animation = "idle_normal";
            public string animatorState = "";
            public string triggerName = "";
        }

        [Header("Avatar Targets")]
        [SerializeField] private Animator animator;
        [SerializeField] private SkinnedMeshRenderer faceRenderer;
        [SerializeField] private AudioSource speechAudioSource;
        [SerializeField] private YuiPresenceAnimator presenceAnimator;

        [Header("Face")]
        [SerializeField] private string neutralFace = "Neutral";
        [SerializeField] private float faceReturnDelaySeconds = 4f;
        [SerializeField] private List<FaceBinding> faceBindings = new List<FaceBinding>
        {
            new FaceBinding { face = "Neutral", blendShapeName = "Neutral", weight = 0f },
            new FaceBinding
            {
                face = "Joy",
                shapes = new List<FaceShape>
                {
                    new FaceShape { blendShapeName = "eye_\u559c", weight = 55f },
                    new FaceShape { blendShapeName = "eyebrow_\u559c", weight = 40f },
                    new FaceShape { blendShapeName = "mouth_\u7b11\u3044", weight = 45f },
                },
            },
            new FaceBinding
            {
                face = "Fun",
                shapes = new List<FaceShape>
                {
                    new FaceShape { blendShapeName = "eye_\u697d", weight = 55f },
                    new FaceShape { blendShapeName = "eyebrow_\u697d", weight = 40f },
                    new FaceShape { blendShapeName = "mouth_\u7b11\u3044", weight = 48f },
                },
            },
            new FaceBinding
            {
                face = "Angry",
                shapes = new List<FaceShape>
                {
                    new FaceShape { blendShapeName = "eye_\u6012", weight = 45f },
                    new FaceShape { blendShapeName = "eyebrow_\u6012", weight = 60f },
                    new FaceShape { blendShapeName = "mouth_\u4e0d\u6a5f\u5acc", weight = 45f },
                },
            },
            new FaceBinding
            {
                face = "Sorrow",
                shapes = new List<FaceShape>
                {
                    new FaceShape { blendShapeName = "eye_\u54c0", weight = 45f },
                    new FaceShape { blendShapeName = "eyebrow_\u54c0", weight = 55f },
                    new FaceShape { blendShapeName = "mouth_\u771f\u9762\u76ee", weight = 35f },
                },
            },
            new FaceBinding
            {
                face = "Surprised",
                shapes = new List<FaceShape>
                {
                    new FaceShape { blendShapeName = "eye_\u9a5a\u304d", weight = 55f },
                    new FaceShape { blendShapeName = "eyebrow_\u4e0a", weight = 55f },
                    new FaceShape { blendShapeName = "mouth_\u25cb", weight = 55f },
                },
            },
        };

        [Header("Animation")]
        [SerializeField] private bool applyAnimation = false;
        [SerializeField] private int animatorLayer = 0;
        [SerializeField] private float crossFadeSeconds = 0.15f;
        [SerializeField] private string speakingBool = "IsSpeaking";
        [SerializeField] private List<AnimationBinding> animationBindings = new List<AnimationBinding>
        {
            new AnimationBinding { animation = "idle_normal", animatorState = "Default" },
            new AnimationBinding { animation = "idle_relaxed", animatorState = "Default" },
            new AnimationBinding { animation = "nod_small", animatorState = "Victory" },
            new AnimationBinding { animation = "nod_big", animatorState = "Victory" },
            new AnimationBinding { animation = "wave_small", animatorState = "HandOpen" },
            new AnimationBinding { animation = "wave_big", animatorState = "HandOpen" },
            new AnimationBinding { animation = "thinking", animatorState = "Default" },
            new AnimationBinding { animation = "surprised_body", animatorState = "HandOpen" },
            new AnimationBinding { animation = "happy_body", animatorState = "Victory" },
            new AnimationBinding { animation = "troubled_body", animatorState = "Default" },
            new AnimationBinding { animation = "proud_pose", animatorState = "Victory" },
            new AnimationBinding { animation = "tsukkomi_point", animatorState = "FingerPoint" },
            new AnimationBinding { animation = "look_away", animatorState = "Default" },
            new AnimationBinding { animation = "talk_gesture_small", animatorState = "HandOpen" },
        };

        private readonly Dictionary<string, FaceBinding> faceBindingByName =
            new Dictionary<string, FaceBinding>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AnimationBinding> animationBindingByName =
            new Dictionary<string, AnimationBinding>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> blendShapeIndexByName =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, FaceShape[]> defaultFaceShapesByName =
            new Dictionary<string, FaceShape[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Joy"] = new[]
                {
                    new FaceShape { blendShapeName = "eye_\u559c", weight = 55f },
                    new FaceShape { blendShapeName = "eyebrow_\u559c", weight = 40f },
                    new FaceShape { blendShapeName = "mouth_\u7b11\u3044", weight = 45f },
                    new FaceShape { blendShapeName = "Joy", weight = 55f },
                    new FaceShape { blendShapeName = "Happy", weight = 55f },
                    new FaceShape { blendShapeName = "Fcl_ALL_Joy", weight = 55f },
                },
                ["Fun"] = new[]
                {
                    new FaceShape { blendShapeName = "eye_\u697d", weight = 55f },
                    new FaceShape { blendShapeName = "eyebrow_\u697d", weight = 40f },
                    new FaceShape { blendShapeName = "mouth_\u7b11\u3044", weight = 48f },
                    new FaceShape { blendShapeName = "Fun", weight = 55f },
                    new FaceShape { blendShapeName = "Happy", weight = 50f },
                    new FaceShape { blendShapeName = "Fcl_ALL_Fun", weight = 55f },
                },
                ["Angry"] = new[]
                {
                    new FaceShape { blendShapeName = "eye_\u6012", weight = 45f },
                    new FaceShape { blendShapeName = "eyebrow_\u6012", weight = 60f },
                    new FaceShape { blendShapeName = "mouth_\u4e0d\u6a5f\u5acc", weight = 45f },
                    new FaceShape { blendShapeName = "Angry", weight = 55f },
                    new FaceShape { blendShapeName = "Fcl_ALL_Angry", weight = 55f },
                },
                ["Sorrow"] = new[]
                {
                    new FaceShape { blendShapeName = "eye_\u54c0", weight = 45f },
                    new FaceShape { blendShapeName = "eyebrow_\u54c0", weight = 55f },
                    new FaceShape { blendShapeName = "mouth_\u771f\u9762\u76ee", weight = 35f },
                    new FaceShape { blendShapeName = "Sorrow", weight = 55f },
                    new FaceShape { blendShapeName = "Sad", weight = 55f },
                    new FaceShape { blendShapeName = "Fcl_ALL_Sorrow", weight = 55f },
                },
                ["Surprised"] = new[]
                {
                    new FaceShape { blendShapeName = "eye_\u9a5a\u304d", weight = 55f },
                    new FaceShape { blendShapeName = "eyebrow_\u4e0a", weight = 55f },
                    new FaceShape { blendShapeName = "mouth_\u25cb", weight = 55f },
                    new FaceShape { blendShapeName = "Surprised", weight = 55f },
                    new FaceShape { blendShapeName = "Surprise", weight = 55f },
                    new FaceShape { blendShapeName = "Fcl_ALL_Surprised", weight = 55f },
                },
            };

        private float faceReturnAt = -1f;
        private bool wasSpeaking;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (faceRenderer == null)
            {
                faceRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            }

            if (speechAudioSource == null)
            {
                speechAudioSource = GetComponent<AudioSource>();
            }

            if (presenceAnimator == null)
            {
                presenceAnimator = GetComponent<YuiPresenceAnimator>()
                    ?? GetComponentInChildren<YuiPresenceAnimator>(true)
                    ?? GetComponentInParent<YuiPresenceAnimator>();
            }

            RebuildLookups();
        }

        private void Update()
        {
            UpdateSpeakingState();

            if (faceReturnAt > 0f && Time.time >= faceReturnAt)
            {
                faceReturnAt = -1f;
                ApplyFace(neutralFace, false);
            }
        }

        public void SetSpeechAudioSource(AudioSource audioSource)
        {
            speechAudioSource = audioSource;
            if (presenceAnimator != null)
            {
                presenceAnimator.SetSpeechAudioSource(audioSource);
            }
            UpdateSpeakingState();
        }

        public void SetPresenceAnimator(YuiPresenceAnimator animatorController)
        {
            presenceAnimator = animatorController;
            if (presenceAnimator != null)
            {
                presenceAnimator.SetSpeechAudioSource(speechAudioSource);
            }
        }

        public void ConfigureAvatarTargets(
            Animator targetAnimator,
            SkinnedMeshRenderer targetFaceRenderer,
            YuiPresenceAnimator targetPresenceAnimator)
        {
            animator = targetAnimator;
            faceRenderer = targetFaceRenderer;
            presenceAnimator = targetPresenceAnimator;
            if (presenceAnimator != null)
            {
                presenceAnimator.SetSpeechAudioSource(speechAudioSource);
            }

            RebuildLookups();
            faceReturnAt = -1f;
            ApplyFace(neutralFace, false);
            UpdateSpeakingState();
        }

        public void ApplyResponse(ChatResponse response)
        {
            if (response == null)
            {
                return;
            }

            if (presenceAnimator == null)
            {
                presenceAnimator = GetComponent<YuiPresenceAnimator>()
                    ?? GetComponentInChildren<YuiPresenceAnimator>(true)
                    ?? GetComponentInParent<YuiPresenceAnimator>();
            }

            ApplyFace(response.Face, true);
            if (presenceAnimator != null)
            {
                presenceAnimator.PlayBackendAnimation(response.Animation);
            }
            if (applyAnimation)
            {
                PlayAnimation(response.Animation);
            }
        }

        public void ApplyFace(string face, bool returnToNeutral)
        {
            var binding = FindFaceBinding(face);
            if (binding == null)
            {
                Debug.LogWarning($"YuiAvatarController face binding not found: {face}");
                return;
            }

            SetAllFaceBlendShapes(0f);
            ApplyFaceBinding(binding);
            faceReturnAt = returnToNeutral && !IsNeutral(binding.face)
                ? Time.time + faceReturnDelaySeconds
                : -1f;
        }

        public void PlayAnimation(string animation)
        {
            var binding = FindAnimationBinding(animation);
            if (binding == null || animator == null)
            {
                if (!string.IsNullOrWhiteSpace(animation))
                {
                    Debug.Log($"YuiAvatarController animation pending: {animation}");
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(binding.triggerName))
            {
                animator.SetTrigger(binding.triggerName);
                return;
            }

            if (!string.IsNullOrWhiteSpace(binding.animatorState))
            {
                var animatorState = ResolveAnimatorState(animation, binding.animatorState);
                if (string.IsNullOrWhiteSpace(animatorState)
                    || !animator.HasState(animatorLayer, Animator.StringToHash(animatorState)))
                {
                    return;
                }

                animator.CrossFadeInFixedTime(
                    animatorState,
                    crossFadeSeconds,
                    animatorLayer);
            }
        }

        private string ResolveAnimatorState(string animation, string configuredState)
        {
            if (!string.IsNullOrWhiteSpace(configuredState)
                && animator != null
                && animator.HasState(animatorLayer, Animator.StringToHash(configuredState)))
            {
                return configuredState;
            }

            switch (animation)
            {
                case "nod_small":
                case "nod_big":
                case "happy_body":
                case "proud_pose":
                    return "Victory";
                case "wave_small":
                case "wave_big":
                case "surprised_body":
                case "talk_gesture_small":
                    return "HandOpen";
                case "tsukkomi_point":
                    return "FingerPoint";
                default:
                    return "Default";
            }
        }

        private void RebuildLookups()
        {
            faceBindingByName.Clear();
            foreach (var binding in faceBindings)
            {
                if (binding != null && !string.IsNullOrWhiteSpace(binding.face))
                {
                    faceBindingByName[binding.face] = binding;
                }
            }

            animationBindingByName.Clear();
            foreach (var binding in animationBindings)
            {
                if (binding != null && !string.IsNullOrWhiteSpace(binding.animation))
                {
                    animationBindingByName[binding.animation] = binding;
                }
            }

            blendShapeIndexByName.Clear();
            if (faceRenderer == null || faceRenderer.sharedMesh == null)
            {
                return;
            }

            var mesh = faceRenderer.sharedMesh;
            for (var index = 0; index < mesh.blendShapeCount; index++)
            {
                blendShapeIndexByName[mesh.GetBlendShapeName(index)] = index;
            }
        }

        private FaceBinding FindFaceBinding(string face)
        {
            if (!string.IsNullOrWhiteSpace(face)
                && YuiAnimationCatalog.IsKnownFace(face)
                && faceBindingByName.TryGetValue(face, out var binding))
            {
                return binding;
            }

            if (faceBindingByName.TryGetValue(neutralFace, out var neutralBinding))
            {
                return neutralBinding;
            }

            return null;
        }

        private AnimationBinding FindAnimationBinding(string animation)
        {
            if (!string.IsNullOrWhiteSpace(animation)
                && YuiAnimationCatalog.IsKnownAnimation(animation)
                && animationBindingByName.TryGetValue(animation, out var binding))
            {
                return binding;
            }

            return animationBindingByName.TryGetValue("idle_normal", out var idleBinding)
                ? idleBinding
                : null;
        }

        private void SetAllFaceBlendShapes(float weight)
        {
            if (faceRenderer == null)
            {
                return;
            }

            foreach (var binding in faceBindings)
            {
                foreach (var shape in EnumerateFaceShapes(binding))
                {
                    var index = BlendShapeIndex(shape.blendShapeName);
                    if (index >= 0)
                    {
                        faceRenderer.SetBlendShapeWeight(index, weight);
                    }
                }
            }

            foreach (var shapes in defaultFaceShapesByName.Values)
            {
                foreach (var shape in shapes)
                {
                    var index = BlendShapeIndex(shape.blendShapeName);
                    if (index >= 0)
                    {
                        faceRenderer.SetBlendShapeWeight(index, weight);
                    }
                }
            }
        }

        private void ApplyFaceBinding(FaceBinding binding)
        {
            if (faceRenderer == null || binding == null)
            {
                return;
            }

            var applied = false;
            foreach (var shape in EnumerateFaceShapes(binding))
            {
                var index = BlendShapeIndex(shape.blendShapeName);
                if (index >= 0)
                {
                    faceRenderer.SetBlendShapeWeight(index, shape.weight);
                    applied = true;
                }
            }

            if (applied || !defaultFaceShapesByName.TryGetValue(binding.face, out var defaultShapes))
            {
                return;
            }

            foreach (var shape in defaultShapes)
            {
                var index = BlendShapeIndex(shape.blendShapeName);
                if (index >= 0)
                {
                    faceRenderer.SetBlendShapeWeight(index, shape.weight);
                }
            }
        }

        private IEnumerable<FaceShape> EnumerateFaceShapes(FaceBinding binding)
        {
            if (binding == null)
            {
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(binding.blendShapeName))
            {
                yield return new FaceShape
                {
                    blendShapeName = binding.blendShapeName,
                    weight = binding.weight,
                };
            }

            if (binding.shapes == null)
            {
                yield break;
            }

            foreach (var shape in binding.shapes)
            {
                if (shape != null && !string.IsNullOrWhiteSpace(shape.blendShapeName))
                {
                    yield return shape;
                }
            }
        }

        private int BlendShapeIndex(string blendShapeName)
        {
            if (string.IsNullOrWhiteSpace(blendShapeName))
            {
                return -1;
            }

            if (blendShapeIndexByName.TryGetValue(blendShapeName, out var index))
            {
                return index;
            }

            var normalizedTarget = NormalizeBlendShapeName(blendShapeName);
            foreach (var pair in blendShapeIndexByName)
            {
                var currentName = pair.Key;
                var normalizedCurrent = NormalizeBlendShapeName(currentName);
                if (string.Equals(currentName, blendShapeName, StringComparison.OrdinalIgnoreCase)
                    || currentName.EndsWith("." + blendShapeName, StringComparison.OrdinalIgnoreCase)
                    || normalizedCurrent == normalizedTarget
                    || normalizedCurrent.EndsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }

            return -1;
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

        private void UpdateSpeakingState()
        {
            var isSpeaking = speechAudioSource != null && speechAudioSource.isPlaying;
            if (isSpeaking == wasSpeaking)
            {
                return;
            }

            wasSpeaking = isSpeaking;
            if (animator != null
                && animator.enabled
                && animator.runtimeAnimatorController != null
                && !string.IsNullOrWhiteSpace(speakingBool)
                && HasAnimatorParameter(animator, speakingBool, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(speakingBool, isSpeaking);
            }
        }

        private static bool HasAnimatorParameter(
            Animator targetAnimator,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            AnimatorControllerParameter[] parameters;
            try
            {
                parameters = targetAnimator.parameters;
            }
            catch (ArgumentNullException ex)
            {
                Debug.LogWarning($"Yui avatar: animator parameter check skipped: {ex.Message}");
                return false;
            }

            foreach (var parameter in parameters)
            {
                if (parameter.name == parameterName && parameter.type == parameterType)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsNeutral(string face)
        {
            return string.Equals(face, neutralFace, StringComparison.OrdinalIgnoreCase);
        }
    }
}
