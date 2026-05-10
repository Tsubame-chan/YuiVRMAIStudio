using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UniGLTF;
using UniVRM10;
using UnityEngine;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.Avatar
{
    public sealed class YuiRuntimeVrmImporter : MonoBehaviour
    {
        private const string CustomVrmPathKey = YuiPrefsKeys.CustomVrmPath;
        private const string CustomVrmTransformKeyPrefix = "Yui.Settings.CustomVrmTransform.v4.";
        private const string CustomAvatarName = "Yui Custom VRM Avatar";
        private const float TargetAvatarHeightMeters = 1.58f;
        private const float RuntimeVrmFacingYawDegrees = 180f;

        [SerializeField] private YuiAvatarSwitcher avatarSwitcher;
        [SerializeField] private Transform avatarParent;
        [SerializeField] private bool loadSavedCustomVrmOnStart = true;

        public string LastCustomVrmPath { get; private set; }
        public string LastImportMessage { get; private set; }
        public bool IsImporting { get; private set; }
        public bool HasRestorableSavedCustomVrm
        {
            get
            {
                EnsureLastCustomVrmPathLoaded();
                return loadSavedCustomVrmOnStart
                    && TryGetSavedCustomVrmPath(out var path)
                    && File.Exists(path);
            }
        }

        private void Awake()
        {
            if (avatarSwitcher == null)
            {
                avatarSwitcher = GetComponent<YuiAvatarSwitcher>() ?? YuiSceneObjectFinder.FindFirst<YuiAvatarSwitcher>();
            }

            LastCustomVrmPath = PlayerPrefs.GetString(CustomVrmPathPrefsKey(YuiAvatarSlots.CustomVrm1), PlayerPrefs.GetString(CustomVrmPathKey, string.Empty));
        }

        private void EnsureLastCustomVrmPathLoaded()
        {
            if (string.IsNullOrWhiteSpace(LastCustomVrmPath))
            {
                LastCustomVrmPath = PlayerPrefs.GetString(CustomVrmPathPrefsKey(YuiAvatarSlots.CustomVrm1), PlayerPrefs.GetString(CustomVrmPathKey, string.Empty));
            }
        }

        private async void Start()
        {
            if (!HasRestorableSavedCustomVrm)
            {
                return;
            }

            var slot = GetSavedAvatarSlot();
            var path = GetCustomVrmPath(slot);
            await ImportFromPathAsync(path, true, slot);
        }

        public async Task<bool> ImportFromFilePickerAsync(string slot = null)
        {
            LogImport("Opening VRM file picker.");
            var result = await YuiFilePicker.OpenVrmFileAsync();
            LogImport($"File picker result: opened={result.Opened} path={result.Path} message={result.UserMessage}");
            if (!result.Opened)
            {
                LastImportMessage = !string.IsNullOrWhiteSpace(result.UserMessage)
                    ? result.UserMessage
                    : "VRM selection was canceled.";
                if (!string.IsNullOrWhiteSpace(result.UserMessage))
                {
                    Debug.LogWarning($"Yui custom VRM import: {result.UserMessage}");
                }

                return false;
            }

            return await ImportFromPathAsync(result.Path, true, slot);
        }

        public async Task<bool> ImportFromPathAsync(string path, bool activateOnSuccess = true, string slot = null)
        {
            slot = YuiAvatarSlots.IsCustomVrm(slot) ? YuiAvatarSlots.Normalize(slot) : YuiAvatarSlots.CustomVrm1;
            if (IsImporting)
            {
                LastImportMessage = "Another VRM import is already running.";
                Debug.LogWarning("Yui custom VRM import: import is already running.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                LastImportMessage = "The selected VRM file was not found.";
                Debug.LogWarning($"Yui custom VRM import: file not found: {path}");
                LogImport($"File not found: {path}");
                return false;
            }

            if (!string.Equals(Path.GetExtension(path), ".vrm", StringComparison.OrdinalIgnoreCase))
            {
                LastImportMessage = "Please choose a .vrm file.";
                Debug.LogWarning($"Yui custom VRM import: unsupported file extension: {path}");
                LogImport($"Unsupported extension: {path}");
                return false;
            }

            IsImporting = true;
            LastImportMessage = string.Empty;
            GameObject root = null;
            try
            {
                LogImport($"Begin loading VRM: {path} size={new FileInfo(path).Length} bytes");
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var instance = await Vrm10.LoadPathAsync(
                    path,
                    canLoadVrm0X: true,
                    controlRigGenerationOption: ControlRigGenerationOption.None,
                    showMeshes: true,
                    awaitCaller: new RuntimeOnlyAwaitCaller(),
                    materialGenerator: new BuiltInVrm10MaterialDescriptorGenerator(),
                    vrmMetaInformationCallback: (thumbnail, vrm10Meta, vrm0Meta) =>
                    {
                        var title = vrm10Meta != null ? vrm10Meta.Name : vrm0Meta?.title;
                        LogImport($"VRM metadata loaded: title={title} vrm10={vrm10Meta != null} vrm0={vrm0Meta != null}");
                    },
                    ct: cancellation.Token);

                if (instance == null)
                {
                    LastImportMessage = "UniVRM returned no avatar instance.";
                    Debug.LogWarning("Yui custom VRM import: UniVRM returned no instance.");
                    LogImport("UniVRM returned no instance.");
                    return false;
                }

                root = instance.gameObject;
                root.name = CustomAvatarName;
                var parent = ResolveAvatarParent();
                root.transform.SetParent(parent, false);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.Euler(0f, RuntimeVrmFacingYawDegrees, 0f);
                root.transform.localScale = Vector3.one;
                ApplySavedOrAutoTransform(root, path);

                var runtimeGltf = root.GetComponent<RuntimeGltfInstance>();
                if (runtimeGltf != null)
                {
                    runtimeGltf.ShowMeshes();
                }

                LastCustomVrmPath = path;
                PlayerPrefs.SetString(CustomVrmPathPrefsKey(slot), path);
                if (slot == YuiAvatarSlots.CustomVrm1)
                {
                    PlayerPrefs.SetString(CustomVrmPathKey, path);
                }
                PlayerPrefs.Save();

                if (avatarSwitcher == null)
                {
                    avatarSwitcher = GetComponent<YuiAvatarSwitcher>() ?? YuiSceneObjectFinder.FindFirst<YuiAvatarSwitcher>();
                }

                if (avatarSwitcher != null)
                {
                    avatarSwitcher.SetCustomAvatar(root, slot, activateOnSuccess);
                    LogImport($"Custom avatar assigned. activeSlot={avatarSwitcher.ActiveSlot} activeAvatar={avatarSwitcher.ActiveAvatar?.name}");
                }
                else
                {
                    root.SetActive(true);
                    LogImport("Avatar switcher not found; custom avatar root activated directly.");
                }

                Debug.Log($"Yui custom VRM import: loaded {Path.GetFileName(path)}");
                LastImportMessage = $"Loaded {Path.GetFileName(path)}";
                LogImport(LastImportMessage);
                return true;
            }
            catch (Exception ex)
            {
                if (root != null)
                {
                    Destroy(root);
                }

                LastImportMessage = $"Custom VRM import failed: {ex.Message}";
                Debug.LogError($"Yui custom VRM import failed: {ex.Message}");
                LogImport($"{LastImportMessage}\n{ex}");
                return false;
            }
            finally
            {
                IsImporting = false;
            }
        }

        public string GetCustomVrmPath(string slot)
        {
            slot = YuiAvatarSlots.IsCustomVrm(slot) ? YuiAvatarSlots.Normalize(slot) : YuiAvatarSlots.CustomVrm1;
            var path = PlayerPrefs.GetString(CustomVrmPathPrefsKey(slot), string.Empty);
            if (slot == YuiAvatarSlots.CustomVrm1 && string.IsNullOrWhiteSpace(path))
            {
                path = PlayerPrefs.GetString(CustomVrmPathKey, string.Empty);
            }

            return path;
        }

        public void ClearCustomVrmSlot(string slot)
        {
            slot = YuiAvatarSlots.IsCustomVrm(slot) ? YuiAvatarSlots.Normalize(slot) : YuiAvatarSlots.CustomVrm1;
            var previousPath = GetCustomVrmPath(slot);
            PlayerPrefs.DeleteKey(CustomVrmPathPrefsKey(slot));
            PlayerPrefs.DeleteKey($"{YuiPrefsKeys.CustomVrmNamePrefix}.{YuiAvatarSlots.CustomVrmIndex(slot)}");
            if (slot == YuiAvatarSlots.CustomVrm1)
            {
                PlayerPrefs.DeleteKey(CustomVrmPathKey);
            }

            PlayerPrefs.Save();

            if (avatarSwitcher == null)
            {
                avatarSwitcher = GetComponent<YuiAvatarSwitcher>() ?? YuiSceneObjectFinder.FindFirst<YuiAvatarSwitcher>();
            }

            if (avatarSwitcher != null && string.Equals(avatarSwitcher.ActiveSlot, slot, StringComparison.OrdinalIgnoreCase))
            {
                avatarSwitcher.ClearCustomAvatar();
            }

            if (string.Equals(LastCustomVrmPath, previousPath, StringComparison.OrdinalIgnoreCase))
            {
                LastCustomVrmPath = string.Empty;
            }
        }

        private static bool TryGetSavedCustomVrmPath(out string path)
        {
            var slot = GetSavedAvatarSlot();
            path = string.Empty;
            if (!YuiAvatarSlots.IsCustomVrm(slot))
            {
                return false;
            }

            path = PlayerPrefs.GetString(CustomVrmPathPrefsKey(slot), string.Empty);
            if (slot == YuiAvatarSlots.CustomVrm1 && string.IsNullOrWhiteSpace(path))
            {
                path = PlayerPrefs.GetString(CustomVrmPathKey, string.Empty);
            }

            return !string.IsNullOrWhiteSpace(path);
        }

        private static string GetSavedAvatarSlot()
        {
            var source = string.IsNullOrWhiteSpace(Application.dataPath)
                ? Application.identifier
                : Application.dataPath;
            var key = $"{YuiPrefsKeys.AvatarSlot}.{StableHash(source ?? "default")}";
            return YuiAvatarSlots.Normalize(PlayerPrefs.GetString(key, string.Empty));
        }

        private static string CustomVrmPathPrefsKey(string slot)
        {
            var index = YuiAvatarSlots.CustomVrmIndex(slot);
            return $"{YuiPrefsKeys.CustomVrmPathPrefix}.{index}";
        }

        private Transform ResolveAvatarParent()
        {
            if (avatarParent != null)
            {
                return avatarParent;
            }

            if (avatarSwitcher == null)
            {
                avatarSwitcher = GetComponent<YuiAvatarSwitcher>() ?? YuiSceneObjectFinder.FindFirst<YuiAvatarSwitcher>();
            }

            avatarParent = avatarSwitcher != null ? avatarSwitcher.PreferredAvatarParent : null;
            if (avatarParent != null)
            {
                return avatarParent;
            }

            var container = new GameObject("Yui Runtime Avatars");
            avatarParent = container.transform;
            return avatarParent;
        }

        public void SaveCurrentCustomAvatarTransform()
        {
            if (avatarSwitcher == null || avatarSwitcher.CustomAvatar == null || string.IsNullOrWhiteSpace(LastCustomVrmPath))
            {
                return;
            }

            SaveTransform(LastCustomVrmPath, avatarSwitcher.CustomAvatar.transform);
        }

        private static void ApplySavedOrAutoTransform(GameObject root, string path)
        {
            if (root == null)
            {
                return;
            }

            if (TryLoadTransform(path, out var localPosition, out var localRotation, out var localScale))
            {
                root.transform.localPosition = localPosition;
                root.transform.localRotation = localRotation;
                root.transform.localScale = localScale;
                return;
            }

            NormalizeAvatarTransform(root);
            SaveTransform(path, root.transform);
        }

        private static void NormalizeAvatarTransform(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var bounds = CalculateRendererBounds(root);
            if (!bounds.HasValue || bounds.Value.size.y <= 0.001f)
            {
                return;
            }

            var height = bounds.Value.size.y;
            var scale = Mathf.Clamp(TargetAvatarHeightMeters / height, 0.01f, 100f);
            root.transform.localScale *= scale;

            bounds = CalculateRendererBounds(root);
            if (!bounds.HasValue)
            {
                return;
            }

            var center = bounds.Value.center;
            var min = bounds.Value.min;
            root.transform.position += new Vector3(-center.x, -min.y, -center.z);
        }

        private static bool TryLoadTransform(
            string path,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale)
        {
            var key = TransformPrefsKey(path);
            localPosition = Vector3.zero;
            localRotation = Quaternion.Euler(0f, RuntimeVrmFacingYawDegrees, 0f);
            localScale = Vector3.one;
            if (!PlayerPrefs.HasKey(key + ".ScaleX"))
            {
                return false;
            }

            localPosition = new Vector3(
                PlayerPrefs.GetFloat(key + ".OffsetX", 0f),
                PlayerPrefs.GetFloat(key + ".OffsetY", 0f),
                PlayerPrefs.GetFloat(key + ".OffsetZ", 0f));
            localRotation = Quaternion.Euler(
                PlayerPrefs.GetFloat(key + ".RotationX", 0f),
                PlayerPrefs.GetFloat(key + ".RotationY", RuntimeVrmFacingYawDegrees),
                PlayerPrefs.GetFloat(key + ".RotationZ", 0f));
            localScale = new Vector3(
                PlayerPrefs.GetFloat(key + ".ScaleX", 1f),
                PlayerPrefs.GetFloat(key + ".ScaleY", 1f),
                PlayerPrefs.GetFloat(key + ".ScaleZ", 1f));
            return IsSaneSavedTransform(localPosition, localScale);
        }

        private static bool IsSaneSavedTransform(Vector3 localPosition, Vector3 localScale)
        {
            if (!IsFinite(localPosition.x) || !IsFinite(localPosition.y) || !IsFinite(localPosition.z)
                || !IsFinite(localScale.x) || !IsFinite(localScale.y) || !IsFinite(localScale.z))
            {
                return false;
            }

            if (Mathf.Abs(localPosition.x) > 5f || Mathf.Abs(localPosition.y) > 5f || Mathf.Abs(localPosition.z) > 5f)
            {
                return false;
            }

            return localScale.x > 0.001f && localScale.y > 0.001f && localScale.z > 0.001f
                && localScale.x < 100f && localScale.y < 100f && localScale.z < 100f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void SaveTransform(string path, Transform transform)
        {
            if (transform == null)
            {
                return;
            }

            var key = TransformPrefsKey(path);
            var position = transform.localPosition;
            var rotation = transform.localRotation.eulerAngles;
            var scale = transform.localScale;
            PlayerPrefs.SetFloat(key + ".OffsetX", position.x);
            PlayerPrefs.SetFloat(key + ".OffsetY", position.y);
            PlayerPrefs.SetFloat(key + ".OffsetZ", position.z);
            PlayerPrefs.SetFloat(key + ".RotationX", rotation.x);
            PlayerPrefs.SetFloat(key + ".RotationY", rotation.y);
            PlayerPrefs.SetFloat(key + ".RotationZ", rotation.z);
            PlayerPrefs.SetFloat(key + ".ScaleX", scale.x);
            PlayerPrefs.SetFloat(key + ".ScaleY", scale.y);
            PlayerPrefs.SetFloat(key + ".ScaleZ", scale.z);
            PlayerPrefs.Save();
        }

        private static string TransformPrefsKey(string path)
        {
            var normalized = Path.GetFullPath(path ?? string.Empty)
                .Trim()
                .ToLowerInvariant();
            return CustomVrmTransformKeyPrefix + StableHash(normalized);
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

        private static Bounds? CalculateRendererBounds(GameObject root)
        {
            Bounds? bounds = null;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                if (bounds.HasValue)
                {
                    var current = bounds.Value;
                    current.Encapsulate(renderer.bounds);
                    bounds = current;
                }
                else
                {
                    bounds = renderer.bounds;
                }
            }

            return bounds;
        }

        private static void LogImport(string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            Debug.Log($"Yui custom VRM import: {message}");
            try
            {
                var path = Path.Combine(Application.persistentDataPath, "yui-vrm-import.log");
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch
            {
                // Player.log still receives Debug.Log above.
            }
        }

    }
}






