using UnityEngine;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Avatar
{
    public sealed class YuiPresenceAnimator : MonoBehaviour
    {
        private const int AnimatorLayer = 0;
        private const string IdleCalmState = "IdleCalm";
        private const string IdleCheerfulState = "IdleCheerful";
        private const string IdleThinkingState = "IdleThinking";
        private const string GestureNodState = "GestureNod";
        private const string GestureWaveState = "GestureWave";
        private const string GestureTalkState = "GestureTalk";
        private const string GestureSurpriseState = "GestureSurprise";

        [Header("Targets")]
        [SerializeField] private Animator animator;
        [SerializeField] private AudioSource speechAudioSource;

        [Header("Presence")]
        [SerializeField] private YuiPresenceIdleStyle idleStyle = YuiPresenceIdleStyle.Calm;
        [SerializeField] private float crossFadeSeconds = 0.2f;
        [SerializeField] private float gestureHoldSeconds = 1.6f;
        [SerializeField] private float speakingGestureIntervalSeconds = 7.5f;
        [SerializeField] private string speakingBool = "IsSpeaking";
        [SerializeField] private bool enableSubtleSpeechGestures = true;

        private float returnToIdleAt = -1f;
        private float nextSpeakingGestureAt = -1f;
        private bool wasSpeaking;
        private string currentIdleState = IdleCalmState;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (speechAudioSource == null)
            {
                speechAudioSource = GetComponent<AudioSource>();
            }

            currentIdleState = IdleStateName(idleStyle);
        }

        private void OnEnable()
        {
            PlayIdle(true);
        }

        private void Update()
        {
            var isSpeaking = speechAudioSource != null && speechAudioSource.isPlaying;
            if (isSpeaking != wasSpeaking)
            {
                wasSpeaking = isSpeaking;
                SetSpeaking(isSpeaking);
                if (isSpeaking)
                {
                    nextSpeakingGestureAt = Time.time + Mathf.Max(2f, speakingGestureIntervalSeconds * 0.5f);
                }
            }

            if (returnToIdleAt > 0f && Time.time >= returnToIdleAt)
            {
                returnToIdleAt = -1f;
                PlayIdle(false);
            }

            if (enableSubtleSpeechGestures
                && isSpeaking
                && nextSpeakingGestureAt > 0f
                && Time.time >= nextSpeakingGestureAt)
            {
                PlayGesture(YuiPresenceGesture.Talk, false);
                nextSpeakingGestureAt = Time.time + Mathf.Max(3f, speakingGestureIntervalSeconds);
            }
        }

        public void Configure(Animator targetAnimator, AudioSource targetSpeechAudioSource)
        {
            animator = targetAnimator;
            speechAudioSource = targetSpeechAudioSource;
            PlayIdle(true);
        }

        public void SetSpeechAudioSource(AudioSource audioSource)
        {
            speechAudioSource = audioSource;
        }

        public void SetIdleStyle(YuiPresenceIdleStyle style)
        {
            idleStyle = style;
            PlayIdle(false);
        }

        public void PlayBackendAnimation(string animation)
        {
            if (string.IsNullOrWhiteSpace(animation))
            {
                return;
            }

            if (!YuiAnimationCatalog.IsKnownAnimation(animation))
            {
                return;
            }

            switch (animation.Trim().ToLowerInvariant())
            {
                case "nod_small":
                case "nod_big":
                    PlayGesture(YuiPresenceGesture.Nod, true);
                    break;
                case "wave_small":
                case "wave_big":
                    PlayGesture(YuiPresenceGesture.Wave, true);
                    break;
                case "thinking":
                case "look_away":
                    SetIdleStyle(YuiPresenceIdleStyle.Thinking);
                    break;
                case "surprised_body":
                    PlayGesture(YuiPresenceGesture.Surprise, true);
                    break;
                case "happy_body":
                case "proud_pose":
                    SetIdleStyle(YuiPresenceIdleStyle.Cheerful);
                    break;
                case "talk_gesture_small":
                case "tsukkomi_point":
                    PlayGesture(YuiPresenceGesture.Talk, true);
                    break;
                case "idle_relaxed":
                case "idle_normal":
                    SetIdleStyle(YuiPresenceIdleStyle.Calm);
                    break;
            }
        }

        public void PlayGesture(YuiPresenceGesture gesture, bool returnToIdle)
        {
            var stateName = GestureStateName(gesture);
            if (string.IsNullOrEmpty(stateName))
            {
                return;
            }

            if (CrossFadeIfAvailable(stateName))
            {
                returnToIdleAt = returnToIdle ? Time.time + Mathf.Max(0.2f, gestureHoldSeconds) : -1f;
            }
        }

        private void PlayIdle(bool immediate)
        {
            var stateName = IdleStateName(idleStyle);
            currentIdleState = stateName;
            if (immediate)
            {
                PlayIfAvailable(stateName);
            }
            else
            {
                CrossFadeIfAvailable(stateName);
            }
        }

        private bool CrossFadeIfAvailable(string stateName)
        {
            if (!HasState(stateName))
            {
                return false;
            }

            animator.CrossFadeInFixedTime(stateName, crossFadeSeconds, AnimatorLayer);
            return true;
        }

        private bool PlayIfAvailable(string stateName)
        {
            if (!HasState(stateName))
            {
                return false;
            }

            animator.Play(stateName, AnimatorLayer);
            return true;
        }

        private bool HasState(string stateName)
        {
            return animator != null
                && !string.IsNullOrEmpty(stateName)
                && animator.HasState(AnimatorLayer, Animator.StringToHash(stateName));
        }

        private void SetSpeaking(bool isSpeaking)
        {
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
            catch (System.ArgumentNullException ex)
            {
                Debug.LogWarning($"Yui presence: animator parameter check skipped: {ex.Message}");
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

        private static string IdleStateName(YuiPresenceIdleStyle style)
        {
            switch (style)
            {
                case YuiPresenceIdleStyle.Cheerful:
                    return IdleCheerfulState;
                case YuiPresenceIdleStyle.Thinking:
                    return IdleThinkingState;
                default:
                    return IdleCalmState;
            }
        }

        private static string GestureStateName(YuiPresenceGesture gesture)
        {
            switch (gesture)
            {
                case YuiPresenceGesture.Nod:
                    return GestureNodState;
                case YuiPresenceGesture.Wave:
                    return GestureWaveState;
                case YuiPresenceGesture.Talk:
                    return GestureTalkState;
                case YuiPresenceGesture.Surprise:
                    return GestureSurpriseState;
                default:
                    return null;
            }
        }
    }
}
