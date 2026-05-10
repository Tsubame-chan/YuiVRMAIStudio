using System;
using System.Collections.Generic;
using ChatdollKit.Model;
using UnityEngine;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;
using ChatdollAnimation = ChatdollKit.Model.Animation;

namespace YuiPhysicalAI.Avatar
{
    [DefaultExecutionOrder(-10000)]
    public sealed class YuiChatdollKitController : MonoBehaviour
    {
        [Serializable]
        public sealed class AnimationBinding
        {
            public string animation = "idle_normal";
            public string registeredAnimationName = "";
            public string parameterKey = "BaseParam";
            public int parameterValue = 0;
            public float duration = 2.0f;
            public string layeredAnimationName = "";
            public string layeredAnimationLayerName = "";
        }

        [SerializeField] private ModelController modelController;
        [SerializeField] private AudioSource speechAudioSource;
        [SerializeField] private bool applyFace = false;
        [SerializeField] private bool applyAnimation = false;
        [SerializeField] private float faceDurationSeconds = 4.0f;
        [SerializeField] private List<AnimationBinding> animationBindings = new List<AnimationBinding>
        {
            new AnimationBinding { animation = "idle_normal", parameterValue = 6, duration = 6.0f },
            new AnimationBinding { animation = "idle_relaxed", parameterValue = 6, duration = 6.0f },
            new AnimationBinding { animation = "nod_small", parameterValue = 1, duration = 1.5f },
            new AnimationBinding { animation = "nod_big", parameterValue = 2, duration = 1.8f },
            new AnimationBinding { animation = "wave_small", parameterValue = 3, duration = 2.0f },
            new AnimationBinding { animation = "wave_big", parameterValue = 4, duration = 2.4f },
            new AnimationBinding { animation = "thinking", parameterValue = 5, duration = 2.5f },
            new AnimationBinding { animation = "surprised_body", parameterValue = 7, duration = 1.8f },
            new AnimationBinding { animation = "happy_body", parameterValue = 8, duration = 2.0f },
            new AnimationBinding { animation = "troubled_body", parameterValue = 9, duration = 2.0f },
            new AnimationBinding { animation = "proud_pose", parameterValue = 10, duration = 2.2f },
            new AnimationBinding { animation = "tsukkomi_point", parameterValue = 11, duration = 2.0f },
            new AnimationBinding { animation = "look_away", parameterValue = 12, duration = 2.0f },
            new AnimationBinding { animation = "talk_gesture_small", parameterValue = 13, duration = 2.0f },
        };

        private readonly Dictionary<string, AnimationBinding> animationBindingByName =
            new Dictionary<string, AnimationBinding>(StringComparer.OrdinalIgnoreCase);
        private bool hasTriedActivation;

        private void Awake()
        {
            if (modelController == null)
            {
                modelController = GetComponent<ModelController>()
                    ?? GetComponentInChildren<ModelController>(true)
                    ?? GetComponentInParent<ModelController>()
                    ?? YuiSceneObjectFinder.FindFirst<ModelController>();
            }

            if (speechAudioSource == null)
            {
                speechAudioSource = GetComponent<AudioSource>();
            }

            if (!applyFace && !applyAnimation)
            {
                DisableChatdollKitRuntimeComponents();
            }

            SetSpeechAudioSource(speechAudioSource);
            RebuildLookups();
        }

        private void Start()
        {
            if (applyFace || applyAnimation)
            {
                EnsureModelControllerReady();
            }
            SetSpeechAudioSource(speechAudioSource);
        }

        private void DisableChatdollKitRuntimeComponents()
        {
            if (modelController == null)
            {
                return;
            }

            foreach (var behaviour in modelController.gameObject.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                var ns = behaviour.GetType().Namespace ?? string.Empty;
                if (ns.StartsWith("ChatdollKit", StringComparison.Ordinal))
                {
                    behaviour.enabled = false;
                }
            }
        }

        public void SetSpeechAudioSource(AudioSource audioSource)
        {
            speechAudioSource = audioSource;
            if (modelController == null || speechAudioSource == null)
            {
                return;
            }

            var speechController = modelController.SpeechController ?? modelController.GetComponent<SpeechController>();
            if (speechController != null)
            {
                speechController.AudioSource = speechAudioSource;
            }
        }

        public void ApplyResponse(ChatResponse response)
        {
            if (!applyFace && !applyAnimation)
            {
                return;
            }

            if (response == null || !IsReady())
            {
                return;
            }

            if (applyFace && modelController.FaceController != null)
            {
                try
                {
                    modelController.FaceController.SetFace(
                        new List<FaceExpression>
                        {
                            new FaceExpression(response.Face ?? "Neutral", faceDurationSeconds),
                        });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Yui ChatdollKit bridge skipped face: {ex.Message}");
                }
            }

            if (applyAnimation)
            {
                ApplyAnimation(response.Animation);
            }
        }

        private void ApplyAnimation(string animation)
        {
            if (!IsReady())
            {
                return;
            }

            var binding = FindAnimationBinding(animation);
            if (binding == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(binding.registeredAnimationName)
                && modelController.IsAnimationRegistered(binding.registeredAnimationName))
            {
                modelController.Animate(
                    new List<ChatdollAnimation>
                    {
                        modelController.GetRegisteredAnimation(binding.registeredAnimationName),
                    });
                return;
            }

            try
            {
                modelController.Animate(
                    new List<ChatdollAnimation>
                    {
                        new ChatdollAnimation(
                            binding.parameterKey,
                            binding.parameterValue,
                            binding.duration,
                            string.IsNullOrWhiteSpace(binding.layeredAnimationName)
                                ? null
                                : binding.layeredAnimationName,
                            string.IsNullOrWhiteSpace(binding.layeredAnimationLayerName)
                                ? null
                                : binding.layeredAnimationLayerName),
                    });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Yui ChatdollKit bridge skipped animation: {ex.Message}");
            }
        }

        private void RebuildLookups()
        {
            animationBindingByName.Clear();
            foreach (var binding in animationBindings)
            {
                if (binding != null && !string.IsNullOrWhiteSpace(binding.animation))
                {
                    animationBindingByName[binding.animation] = binding;
                }
            }
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

        private bool IsReady()
        {
            EnsureModelControllerReady();
            return modelController != null
                && modelController.enabled
                && modelController.AvatarModel != null;
        }

        private void EnsureModelControllerReady()
        {
            if (hasTriedActivation
                || modelController == null
                || modelController.enabled
                || modelController.AvatarModel == null
                || !modelController.gameObject.activeInHierarchy)
            {
                return;
            }

            hasTriedActivation = true;
            try
            {
                modelController.ActivateAvatar(modelController.AvatarModel);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Yui ChatdollKit bridge could not activate ModelController: {ex.Message}");
            }
        }
    }
}
