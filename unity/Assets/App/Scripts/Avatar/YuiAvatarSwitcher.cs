using System;
using System.Collections.Generic;
using System.IO;
using ChatdollKit.Model;
using UnityEngine;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Avatar
{
    [DefaultExecutionOrder(-10000)]
    public sealed class YuiAvatarSwitcher : MonoBehaviour
    {
        // Bundled-avatar sample behaviours that fight our runtime control if left enabled.
        // Match by short type name so we don't have to depend on each asset's namespace.
        private static readonly HashSet<string> SampleBehaviourBlacklist = new HashSet<string>
        {
            "FaceUpdate",
            "IdleChanger",
            "CameraController",
            "ThirdPersonCamera",
            "UnityChanControlScriptWithRgidBody",
            "IKCtrlRightHand",
            "RandomWind",
        };

        [SerializeField] private GameObject demoAvatar;
        [SerializeField] private GameObject distributionAvatar;
        [SerializeField] private GameObject customAvatar;
        [SerializeField] private YuiAvatarController avatarController;
        [SerializeField] private YuiSimpleLipSync lipSync;
        [SerializeField] private YuiConsoleVisibilityController consoleVisibilityController;
        [SerializeField] private ModelController modelController;
        [SerializeField] private AudioSource speechAudioSource;

        private AvatarPoseSnapshot demoInitialPose;
        private AvatarPoseSnapshot distributionInitialPose;

        public string ActiveSlot { get; private set; } = YuiAvatarSlots.UnityChanDefault;
        public GameObject ActiveAvatar { get; private set; }
        public GameObject CustomAvatar => customAvatar;
        public bool HasDemoAvatar => demoAvatar != null;
        public bool HasDistributionAvatar => distributionAvatar != null;
        public bool HasCustomAvatar => customAvatar != null;
        public Transform PreferredAvatarParent => distributionAvatar != null
            ? distributionAvatar.transform.parent
            : demoAvatar != null
                ? demoAvatar.transform.parent
                : transform.parent;

        public void Configure(
            GameObject demo,
            GameObject distribution,
            GameObject custom,
            YuiAvatarController controller,
            YuiSimpleLipSync lipSyncController,
            YuiConsoleVisibilityController consoleController,
            ModelController chatdollModelController,
            AudioSource audioSource)
        {
            demoAvatar = demo;
            distributionAvatar = distribution;
            customAvatar = custom;
            avatarController = controller;
            lipSync = lipSyncController;
            consoleVisibilityController = consoleController;
            modelController = chatdollModelController;
            speechAudioSource = audioSource;
            CaptureInitialPoses();
            SanitizeAvatar(demoAvatar);
            SanitizeAvatar(distributionAvatar);
            SanitizeAvatar(customAvatar);
        }


        private void Awake()
        {
            CaptureInitialPoses();
            SanitizeAvatar(demoAvatar);
            SanitizeAvatar(distributionAvatar);
            SanitizeAvatar(customAvatar);
            HideBundledAvatarsIfWaitingForSavedCustomVrm();
        }

        private void Start()
        {
            RestoreKnownAvatarPose(ActiveAvatar);
            SanitizeAvatar(ActiveAvatar);
        }

        public GameObject SetAvatarSlot(string slot, bool allowFallback = true)
        {
            ActiveSlot = NormalizeSlot(slot);
            var activeAvatar = ResolveAvatar(ActiveSlot);
            if (activeAvatar == null)
            {
                if (!allowFallback)
                {
                    SetActiveIfPresent(demoAvatar, false);
                    SetActiveIfPresent(distributionAvatar, false);
                    SetActiveIfPresent(customAvatar, false);
                    ActiveAvatar = null;
                    return null;
                }

                if (distributionAvatar != null)
                {
                    ActiveSlot = YuiAvatarSlots.UnityChanDefault;
                    activeAvatar = distributionAvatar;
                }
                else
                {
                    ActiveSlot = YuiAvatarSlots.DemoKikyo;
                    activeAvatar = demoAvatar;
                }
            }

            SetActiveIfPresent(demoAvatar, activeAvatar == demoAvatar);
            SetActiveIfPresent(distributionAvatar, activeAvatar == distributionAvatar);
            SetActiveIfPresent(customAvatar, activeAvatar == customAvatar);
            ActiveAvatar = activeAvatar;
            RestoreKnownAvatarPose(activeAvatar);
            SanitizeAvatar(activeAvatar);
            RebindRuntime(activeAvatar);
            return activeAvatar;
        }

        public void SetCustomAvatar(GameObject avatar, bool activate = true)
        {
            SetCustomAvatar(avatar, YuiAvatarSlots.CustomVrm1, activate);
        }

        public void ClearCustomAvatar()
        {
            if (customAvatar != null)
            {
                Destroy(customAvatar);
                customAvatar = null;
            }

            if (YuiAvatarSlots.IsCustomVrm(ActiveSlot))
            {
                SetAvatarSlot(YuiAvatarSlots.UnityChanDefault);
            }
        }

        public void SetCustomAvatar(GameObject avatar, string slot, bool activate = true)
        {
            if (customAvatar != null && customAvatar != avatar)
            {
                Destroy(customAvatar);
            }

            customAvatar = avatar;
            if (customAvatar != null)
            {
                customAvatar.name = "Yui Custom VRM Avatar";
                SanitizeAvatar(customAvatar);
                ConfigureCustomVrmIdlePose(customAvatar);
                if (activate)
                {
                    SetAvatarSlot(YuiAvatarSlots.IsCustomVrm(slot) ? slot : YuiAvatarSlots.CustomVrm1);
                }
                else
                {
                    customAvatar.SetActive(false);
                }
            }
        }

        private void RebindRuntime(GameObject activeAvatar)
        {
            if (activeAvatar == null)
            {
                return;
            }

            var animator = activeAvatar.GetComponentInChildren<Animator>(true);
            var faceRenderer = FindBestFaceRenderer(activeAvatar);
            var presence = YuiAvatarSlots.IsCustomVrm(ActiveSlot)
                ? null
                : activeAvatar.GetComponentInChildren<YuiPresenceAnimator>(true);

            if (avatarController != null)
            {
                avatarController.ConfigureAvatarTargets(animator, faceRenderer, presence);
                avatarController.SetSpeechAudioSource(speechAudioSource);
            }

            if (lipSync != null)
            {
                lipSync.Configure(activeAvatar, speechAudioSource);
            }

            if (consoleVisibilityController != null)
            {
                var shouldFrameCamera = ActiveSlot == YuiAvatarSlots.UnityChanDefault
                    || YuiAvatarSlots.IsCustomVrm(ActiveSlot);
                consoleVisibilityController.SetAvatarRoot(activeAvatar.transform, shouldFrameCamera);
            }

            if (modelController != null)
            {
                modelController.AvatarModel = activeAvatar;
            }
        }


        private GameObject ResolveAvatar(string slot)
        {
            if (slot == YuiAvatarSlots.UnityChanDefault && distributionAvatar != null)
            {
                return distributionAvatar;
            }

            if (YuiAvatarSlots.IsCustomVrm(slot) && customAvatar != null)
            {
                return customAvatar;
            }

            return demoAvatar;
        }

        private void CaptureInitialPoses()
        {
            demoInitialPose = AvatarPoseSnapshot.Capture(demoAvatar);
            distributionInitialPose = AvatarPoseSnapshot.Capture(distributionAvatar);
        }

        private void RestoreKnownAvatarPose(GameObject avatar)
        {
            if (avatar == null)
            {
                return;
            }

            if (avatar == demoAvatar)
            {
                demoInitialPose.Restore(avatar);
            }
            else if (avatar == distributionAvatar)
            {
                distributionInitialPose.Restore(avatar);
            }
        }

        private static void SetActiveIfPresent(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static string NormalizeSlot(string slot)
        {
            return YuiAvatarSlots.Normalize(slot);
        }

        private void HideBundledAvatarsIfWaitingForSavedCustomVrm()
        {
            var savedSlot = GetSavedAvatarSlot();
            if (!YuiAvatarSlots.IsCustomVrm(savedSlot) || !SavedCustomVrmExists(savedSlot))
            {
                return;
            }

            ActiveSlot = savedSlot;
            SetActiveIfPresent(demoAvatar, false);
            SetActiveIfPresent(distributionAvatar, false);
            SetActiveIfPresent(customAvatar, false);
            ActiveAvatar = null;
        }

        private static bool SavedCustomVrmExists(string slot)
        {
            var path = PlayerPrefs.GetString(CustomVrmPathPrefsKey(slot), string.Empty);
            if (YuiAvatarSlots.Normalize(slot) == YuiAvatarSlots.CustomVrm1 && string.IsNullOrWhiteSpace(path))
            {
                path = PlayerPrefs.GetString(YuiPrefsKeys.CustomVrmPath, string.Empty);
            }

            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        private static string GetSavedAvatarSlot()
        {
            var key = $"{YuiPrefsKeys.AvatarSlot}.{GetLocalPrefsScope()}";
            return YuiAvatarSlots.Normalize(PlayerPrefs.GetString(key, string.Empty));
        }

        private static string CustomVrmPathPrefsKey(string slot)
        {
            return $"{YuiPrefsKeys.CustomVrmPathPrefix}.{YuiAvatarSlots.CustomVrmIndex(slot)}";
        }

        private static string GetLocalPrefsScope()
        {
            var source = string.IsNullOrWhiteSpace(Application.dataPath)
                ? Application.identifier
                : Application.dataPath;
            return StableHash(source ?? "default");
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                for (var i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash.ToString("x8");
            }
        }

        private static SkinnedMeshRenderer FindBestFaceRenderer(GameObject root)
        {
            SkinnedMeshRenderer best = null;
            var bestScore = int.MinValue;
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                var score = mesh.blendShapeCount;
                var lowerName = renderer.name.ToLowerInvariant();
                if (lowerName.Contains("face") || lowerName.Contains("body"))
                {
                    score += 100;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = renderer;
                }
            }

            return best;
        }

        private static void SanitizeAvatar(GameObject activeAvatar)
        {
            DisableSampleBehaviours(activeAvatar);
            DisableRootMotion(activeAvatar);
            DisableAvatarPhysics(activeAvatar);
            DisableAvatarColliders(activeAvatar);
        }

        private static void DisableRootMotion(GameObject activeAvatar)
        {
            if (activeAvatar == null)
            {
                return;
            }

            foreach (var animator in activeAvatar.GetComponentsInChildren<Animator>(true))
            {
                if (animator == null)
                {
                    continue;
                }

                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        private static void DisableSampleBehaviours(GameObject activeAvatar)
        {
            if (activeAvatar == null)
            {
                return;
            }

            foreach (var behaviour in activeAvatar.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                if (SampleBehaviourBlacklist.Contains(behaviour.GetType().Name))
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void DisableAvatarPhysics(GameObject activeAvatar)
        {
            if (activeAvatar == null)
            {
                return;
            }

            foreach (var body in activeAvatar.GetComponentsInChildren<Rigidbody>(true))
            {
                body.useGravity = false;
                body.isKinematic = true;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private static void DisableAvatarColliders(GameObject activeAvatar)
        {
            if (activeAvatar == null)
            {
                return;
            }

            foreach (var collider in activeAvatar.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        private static void ConfigureCustomVrmIdlePose(GameObject avatar)
        {
            if (avatar == null)
            {
                return;
            }

            var animator = avatar.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            else
            {
                Debug.LogWarning("Yui custom VRM idle: Animator was not found on imported avatar.");
            }

            var idlePose = avatar.GetComponent<YuiCustomVrmIdlePose>() ?? avatar.AddComponent<YuiCustomVrmIdlePose>();
            idlePose.enabled = true;
        }

        private readonly struct AvatarPoseSnapshot
        {
            private readonly bool hasValue;
            private readonly Vector3 localPosition;
            private readonly Quaternion localRotation;
            private readonly Vector3 localScale;

            private AvatarPoseSnapshot(Transform transform)
            {
                hasValue = transform != null;
                localPosition = transform != null ? transform.localPosition : Vector3.zero;
                localRotation = transform != null ? transform.localRotation : Quaternion.identity;
                localScale = transform != null ? transform.localScale : Vector3.one;
            }

            public static AvatarPoseSnapshot Capture(GameObject avatar)
            {
                return new AvatarPoseSnapshot(avatar != null ? avatar.transform : null);
            }

            public void Restore(GameObject avatar)
            {
                if (!hasValue || avatar == null)
                {
                    return;
                }

                avatar.transform.localPosition = localPosition;
                avatar.transform.localRotation = localRotation;
                avatar.transform.localScale = localScale;
            }
        }
    }
}





