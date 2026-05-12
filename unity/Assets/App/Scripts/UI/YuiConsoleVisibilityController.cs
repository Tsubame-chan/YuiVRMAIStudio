using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiConsoleVisibilityController : MonoBehaviour
    {
        private const string BackdropName = "Yui Runtime Backdrop";
        private const int CameraPresetSlots = 4;

        // Viewer-mode invariants:
        // - consoleVisible=true means the chat UI is interactable and the camera
        //   should lerp back to the saved default framing.
        // - consoleVisible=false means orbit/pan/zoom owns the camera; default
        //   camera fields are treated as the return target and must stay stable.
        // - cachedPivot is only an optimization for the hidden/orbit state. Clear
        //   it whenever the active avatar changes or visibility toggles.
        // - Speech playback is independent from console visibility; hiding the UI
        //   must not stop AudioSource playback or lip sync.

        [SerializeField] private GameObject consoleRoot;
        [SerializeField] private Button hideButton;
        [SerializeField] private List<GameObject> auxiliaryUiRoots = new List<GameObject>();
        [SerializeField] private Transform avatarRoot;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float rotateSensitivity = 0.28f;
        [SerializeField] private float returnSpeed = 4.0f;
        [SerializeField] private float shownFieldOfView = 27f;
        [SerializeField] private float hiddenFieldOfView = 22f;
        [SerializeField] private float minOrbitDistance = 0.35f;
        [SerializeField] private float maxOrbitDistance = 8.0f;
        [SerializeField] private float hiddenDefaultDistanceMultiplier = 1.35f;
        [SerializeField] private float pinchDistanceSensitivity = 0.01f;
        [SerializeField] private float mouseWheelDistanceSensitivity = 0.35f;
        [SerializeField] private float panSensitivity = 0.0022f;
        [SerializeField] private float orbitPivotHeightOffset = 0.1f;

        private enum PointerMode
        {
            None = 0,
            Orbit = 1,
            Pan = 2,
            Zoom = 3,
        }

        private bool consoleVisible = true;
        private bool cameraEditMode;
        private bool pointerDown;
        private bool dragged;
        private bool hasCameraDefault;
        private PointerMode pointerMode;
        private Vector2 lastPointerPosition;
        private Vector3 cachedPivot;
        private bool hasCachedPivot;
        private float cachedPivotRefreshAt;
        private const float PivotCacheLifetimeSeconds = 0.5f;
        private static readonly List<Renderer> RendererBuffer = new List<Renderer>(64);
        private Vector3 defaultCameraPosition;
        private Quaternion defaultCameraRotation;
        private float defaultCameraFieldOfView;
        private bool hasSceneCameraDefault;
        private Vector3 sceneCameraPosition;
        private Quaternion sceneCameraRotation;
        private float sceneCameraFieldOfView;
        private float defaultYaw;
        private float defaultPitch;
        private float defaultOrbitDistance;
        private float currentYaw;
        private float currentPitch;
        private float currentOrbitDistance;
        private float currentHiddenFieldOfView;
        private Vector3 currentPanOffset;
        private float lastToggleTime;
        private readonly Dictionary<GameObject, bool> auxiliaryActiveStates = new Dictionary<GameObject, bool>();
        private bool backdropWasActive;
        private CanvasGroup consoleCanvasGroup;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            currentHiddenFieldOfView = hiddenFieldOfView;
            CaptureCameraDefaults();

            if (hideButton != null)
            {
                hideButton.onClick.AddListener(HideConsole);
            }
        }

        public void RegisterAuxiliaryUiRoot(GameObject root)
        {
            if (root == null || auxiliaryUiRoots.Contains(root))
            {
                return;
            }

            auxiliaryUiRoots.Add(root);
            root.SetActive(consoleVisible && root.activeSelf);
        }

        private void OnDestroy()
        {
            if (hideButton != null)
            {
                hideButton.onClick.RemoveListener(HideConsole);
            }
        }

        private void Update()
        {
            if (consoleVisible)
            {
                RestoreDefaultCamera();
                return;
            }

            HandleHiddenInput();

            if (!pointerDown && !cameraEditMode)
            {
                currentYaw = Mathf.LerpAngle(currentYaw, defaultYaw, Time.deltaTime * returnSpeed);
                currentPitch = Mathf.LerpAngle(currentPitch, defaultPitch, Time.deltaTime * returnSpeed);
                currentOrbitDistance = Mathf.Lerp(currentOrbitDistance, defaultOrbitDistance, Time.deltaTime * returnSpeed);
                currentPanOffset = Vector3.Lerp(currentPanOffset, Vector3.zero, Time.deltaTime * returnSpeed);
                currentHiddenFieldOfView = Mathf.Lerp(
                    currentHiddenFieldOfView,
                    hiddenFieldOfView,
                    Time.deltaTime * returnSpeed);
            }

            UpdateOrbitCamera();
        }

        public void Configure(
            GameObject console,
            Button button,
            Transform avatar,
            Camera camera)
        {
            if (hideButton != null)
            {
                hideButton.onClick.RemoveListener(HideConsole);
            }

            consoleRoot = console;
            hideButton = button;
            avatarRoot = avatar;
            targetCamera = camera;
            currentHiddenFieldOfView = hiddenFieldOfView;
            CaptureCameraDefaults();

            if (hideButton != null)
            {
                hideButton.onClick.AddListener(HideConsole);
            }
        }

        public void SetAvatarRoot(Transform avatar)
        {
            SetAvatarRoot(avatar, true);
        }

        public void SetAvatarRoot(Transform avatar, bool frameDefaultCamera)
        {
            avatarRoot = avatar;
            InvalidatePivotCache();
            if (frameDefaultCamera)
            {
                FrameAvatarAsDefault();
            }
            else
            {
                CaptureCameraDefaults();
                ResetOrbitToDefault();
            }
        }

        public void SetAvatarRootUsingSceneDefault(Transform avatar)
        {
            avatarRoot = avatar;
            InvalidatePivotCache();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null && hasSceneCameraDefault)
            {
                targetCamera.transform.position = sceneCameraPosition;
                targetCamera.transform.rotation = sceneCameraRotation;
                targetCamera.fieldOfView = sceneCameraFieldOfView;
            }

            CaptureCameraDefaults();
            ResetOrbitToDefault();
        }

        public void FrameAvatarAsDefault()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null || avatarRoot == null)
            {
                CaptureCameraDefaults();
                ResetOrbitToDefault();
                return;
            }

            if (!TryGetAvatarBounds(out var bounds))
            {
                var target = avatarRoot.position + Vector3.up * 1.1f;
                targetCamera.transform.position = target + new Vector3(0f, 0f, -3.0f);
                targetCamera.transform.rotation = Quaternion.LookRotation(target - targetCamera.transform.position, Vector3.up);
                targetCamera.fieldOfView = shownFieldOfView > 0f ? shownFieldOfView : 25f;
                CaptureCameraDefaults();
                ResetOrbitToDefault();
                return;
            }

            var height = Mathf.Max(0.1f, bounds.size.y);
            var targetPoint = bounds.center + new Vector3(0f, height * 0.16f, 0f);
            var distance = Mathf.Clamp(height * 1.9f, 2.45f, 4.75f);
            var cameraLift = Mathf.Clamp(height * 0.06f, 0.08f, 0.16f);
            targetCamera.transform.position = targetPoint + new Vector3(0f, cameraLift, -distance);
            targetCamera.transform.rotation = Quaternion.LookRotation(targetPoint - targetCamera.transform.position, Vector3.up);
            targetCamera.fieldOfView = shownFieldOfView > 0f ? shownFieldOfView : 25f;
            CaptureCameraDefaults();
            ResetOrbitToDefault();
        }

        public void ApplyCameraPreset(int presetIndex)
        {
            if (presetIndex <= 0)
            {
                FrameAvatarAsDefault();
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null || !TryLoadCameraPreset(presetIndex, out var position, out var rotation, out var fieldOfView))
            {
                return;
            }

            targetCamera.transform.position = position;
            targetCamera.transform.rotation = rotation;
            targetCamera.fieldOfView = fieldOfView;
            CaptureCameraDefaults();
            ResetOrbitToDefault();
        }

        public void SaveCameraPreset(int presetIndex)
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null || presetIndex <= 0 || presetIndex > CameraPresetSlots)
            {
                return;
            }

            var key = CameraPresetKey(presetIndex);
            var position = targetCamera.transform.position;
            var rotation = targetCamera.transform.rotation.eulerAngles;
            PlayerPrefs.SetFloat(key + ".PositionX", position.x);
            PlayerPrefs.SetFloat(key + ".PositionY", position.y);
            PlayerPrefs.SetFloat(key + ".PositionZ", position.z);
            PlayerPrefs.SetFloat(key + ".RotationX", rotation.x);
            PlayerPrefs.SetFloat(key + ".RotationY", rotation.y);
            PlayerPrefs.SetFloat(key + ".RotationZ", rotation.z);
            PlayerPrefs.SetFloat(key + ".FieldOfView", targetCamera.fieldOfView);
            PlayerPrefs.SetInt(key + ".Saved", 1);
            PlayerPrefs.Save();
            CaptureCameraDefaults();
            ResetOrbitToDefault();
        }

        public void DeleteCameraPreset(int presetIndex)
        {
            if (presetIndex <= 0 || presetIndex > CameraPresetSlots)
            {
                return;
            }

            var key = CameraPresetKey(presetIndex);
            PlayerPrefs.DeleteKey(key + ".PositionX");
            PlayerPrefs.DeleteKey(key + ".PositionY");
            PlayerPrefs.DeleteKey(key + ".PositionZ");
            PlayerPrefs.DeleteKey(key + ".RotationX");
            PlayerPrefs.DeleteKey(key + ".RotationY");
            PlayerPrefs.DeleteKey(key + ".RotationZ");
            PlayerPrefs.DeleteKey(key + ".FieldOfView");
            PlayerPrefs.DeleteKey(key + ".Saved");
            PlayerPrefs.Save();
        }

        private void InvalidatePivotCache()
        {
            hasCachedPivot = false;
            cachedPivotRefreshAt = 0f;
        }

        public void ConfigureCameraView(float shownFov, float hiddenFov, float hiddenDistanceMultiplier)
        {
            shownFieldOfView = Mathf.Max(10f, shownFov);
            hiddenFieldOfView = Mathf.Max(10f, hiddenFov);
            hiddenDefaultDistanceMultiplier = Mathf.Max(1f, hiddenDistanceMultiplier);
            currentHiddenFieldOfView = hiddenFieldOfView;
            CaptureCameraDefaults();
        }

        public void HideConsole()
        {
            SetConsoleVisible(false);
        }

        public void BeginCameraEditMode()
        {
            cameraEditMode = true;
            SetConsoleVisible(false);
        }

        public void EndCameraEditMode()
        {
            cameraEditMode = false;
            ShowConsole();
        }

        private void ShowConsole()
        {
            SetConsoleVisible(true);
        }

        private void SetConsoleVisible(bool visible)
        {
            consoleVisible = visible;
            if (visible)
            {
                cameraEditMode = false;
            }

            lastToggleTime = Time.realtimeSinceStartup;
            pointerDown = false;
            dragged = false;
            pointerMode = PointerMode.None;
            if (consoleRoot != null)
            {
                SetConsoleRootVisible(visible);
            }

            SetAuxiliaryUiVisible(visible);
            SetBackdropVisibleForConsole(visible);

            if (!visible)
            {
                ResetOrbitToDefault();
                currentHiddenFieldOfView = hiddenFieldOfView;
            }
        }

        private void SetConsoleRootVisible(bool visible)
        {
            if (consoleRoot == null)
            {
                return;
            }

            if (!consoleRoot.activeSelf)
            {
                consoleRoot.SetActive(true);
            }

            if (consoleCanvasGroup == null)
            {
                consoleCanvasGroup = consoleRoot.GetComponent<CanvasGroup>();
                if (consoleCanvasGroup == null)
                {
                    consoleCanvasGroup = consoleRoot.AddComponent<CanvasGroup>();
                }
            }

            consoleCanvasGroup.alpha = visible ? 1f : 0f;
            consoleCanvasGroup.interactable = visible;
            consoleCanvasGroup.blocksRaycasts = visible;
        }

        private void SetAuxiliaryUiVisible(bool visible)
        {
            if (!visible)
            {
                auxiliaryActiveStates.Clear();
            }

            for (var index = auxiliaryUiRoots.Count - 1; index >= 0; index--)
            {
                var root = auxiliaryUiRoots[index];
                if (root == null)
                {
                    auxiliaryUiRoots.RemoveAt(index);
                    continue;
                }

                if (!visible)
                {
                    auxiliaryActiveStates[root] = root.activeSelf;
                    root.SetActive(false);
                }
                else
                {
                    var wasActive = !auxiliaryActiveStates.TryGetValue(root, out var active) || active;
                    root.SetActive(wasActive);
                }
            }
        }

        private void SetBackdropVisibleForConsole(bool visible)
        {
            var backdrop = GameObject.Find(BackdropName);
            if (backdrop == null)
            {
                return;
            }

            if (!visible)
            {
                backdropWasActive = backdrop.activeSelf;
                backdrop.SetActive(false);
                return;
            }

            if (backdropWasActive)
            {
                backdrop.SetActive(true);
            }
        }

        private void HandleHiddenInput()
        {
            if (Time.realtimeSinceStartup - lastToggleTime < 0.2f)
            {
                return;
            }

            if (Input.mouseScrollDelta.y != 0f)
            {
                ApplyZoom(-Input.mouseScrollDelta.y * mouseWheelDistanceSensitivity);
            }

            if (Input.touchCount >= 2)
            {
                var firstTouch = Input.GetTouch(0);
                var secondTouch = Input.GetTouch(1);
                var previousFirst = firstTouch.position - firstTouch.deltaPosition;
                var previousSecond = secondTouch.position - secondTouch.deltaPosition;
                var previousDistance = Vector2.Distance(previousFirst, previousSecond);
                var currentDistance = Vector2.Distance(firstTouch.position, secondTouch.position);
                var previousCenter = (previousFirst + previousSecond) * 0.5f;
                var currentCenter = (firstTouch.position + secondTouch.position) * 0.5f;
                var pinchDelta = currentDistance - previousDistance;
                var panDelta = currentCenter - previousCenter;

                pointerDown = true;
                dragged = true;
                pointerMode = PointerMode.Pan;
                ApplyZoom(-pinchDelta * pinchDistanceSensitivity);
                ApplyPan(panDelta);
                return;
            }

            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    BeginPointer(touch.position);
                }
                else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    MovePointer(touch.position);
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    EndPointer();
                }

                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                BeginPointer(Input.mousePosition, PointerMode.Pan);
            }
            else if (Input.GetMouseButton(1))
            {
                MovePointer(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(1))
            {
                EndPointer();
            }

            if (pointerDown && !Input.GetMouseButton(0) && !Input.GetMouseButton(1))
            {
                EndPointer();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                var mode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                    ? PointerMode.Pan
                    : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                        ? PointerMode.Zoom
                        : PointerMode.Orbit;
                BeginPointer(Input.mousePosition, mode);
            }
            else if (Input.GetMouseButton(0))
            {
                MovePointer(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                EndPointer();
            }
        }

        private void BeginPointer(Vector2 position)
        {
            BeginPointer(position, PointerMode.Orbit);
        }

        private void BeginPointer(Vector2 position, PointerMode mode)
        {
            pointerDown = true;
            dragged = false;
            pointerMode = mode;
            lastPointerPosition = position;
        }

        private void MovePointer(Vector2 position)
        {
            if (!pointerDown)
            {
                return;
            }

            var delta = position - lastPointerPosition;
            if (delta.sqrMagnitude > 9f)
            {
                dragged = true;
            }

            switch (pointerMode)
            {
                case PointerMode.Pan:
                    ApplyPan(delta);
                    break;
                case PointerMode.Zoom:
                    ApplyZoom(delta.y * mouseWheelDistanceSensitivity * 0.05f);
                    break;
                default:
                    currentYaw -= delta.x * rotateSensitivity;
                    currentPitch += delta.y * rotateSensitivity;
                    break;
            }

            lastPointerPosition = position;
        }

        private void EndPointer()
        {
            if (!pointerDown)
            {
                return;
            }

            pointerDown = false;
            if (pointerMode == PointerMode.Orbit && !dragged && !cameraEditMode)
            {
                ShowConsole();
            }

            pointerMode = PointerMode.None;
        }

        private void ApplyZoom(float distanceDelta)
        {
            currentOrbitDistance = Mathf.Clamp(
                currentOrbitDistance + distanceDelta,
                minOrbitDistance,
                GetMaxOrbitDistance());
        }

        private void ApplyPan(Vector2 screenDelta)
        {
            if (targetCamera == null)
            {
                return;
            }

            var scale = Mathf.Max(0.45f, currentOrbitDistance) * panSensitivity;
            var right = targetCamera.transform.right;
            var up = targetCamera.transform.up;
            currentPanOffset += (-right * screenDelta.x + -up * screenDelta.y) * scale;
        }

        private void CaptureCameraDefaults()
        {
            if (targetCamera == null)
            {
                return;
            }

            defaultCameraPosition = targetCamera.transform.position;
            defaultCameraRotation = targetCamera.transform.rotation;
            defaultCameraFieldOfView = targetCamera.fieldOfView;
            if (!hasSceneCameraDefault)
            {
                sceneCameraPosition = defaultCameraPosition;
                sceneCameraRotation = defaultCameraRotation;
                sceneCameraFieldOfView = defaultCameraFieldOfView;
                hasSceneCameraDefault = true;
            }
            hasCameraDefault = true;
            ResetOrbitToDefault();
        }

        private void ResetOrbitToDefault()
        {
            if (!hasCameraDefault)
            {
                return;
            }

            var pivot = GetOrbitPivot();
            var toPivot = pivot - defaultCameraPosition;
            var lookRotation = toPivot.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toPivot.normalized, Vector3.up)
                : defaultCameraRotation;
            var lookEuler = lookRotation.eulerAngles;

            defaultYaw = lookEuler.y;
            defaultPitch = NormalizeAngle(lookEuler.x);
            var cameraDistance = Vector3.Distance(defaultCameraPosition, pivot);
            var pulledBackDistance = cameraDistance * Mathf.Max(1.25f, hiddenDefaultDistanceMultiplier);
            defaultOrbitDistance = Mathf.Clamp(
                pulledBackDistance,
                minOrbitDistance,
                GetMaxOrbitDistance());

            currentYaw = defaultYaw;
            currentPitch = defaultPitch;
            currentOrbitDistance = defaultOrbitDistance;
            currentPanOffset = Vector3.zero;
        }

        private void RestoreDefaultCamera()
        {
            if (targetCamera == null || !hasCameraDefault)
            {
                return;
            }

            targetCamera.transform.position = Vector3.Lerp(
                targetCamera.transform.position,
                defaultCameraPosition,
                Time.deltaTime * returnSpeed);
            targetCamera.transform.rotation = Quaternion.Slerp(
                targetCamera.transform.rotation,
                defaultCameraRotation,
                Time.deltaTime * returnSpeed);
            var targetFieldOfView = defaultCameraFieldOfView > 0f
                ? defaultCameraFieldOfView
                : shownFieldOfView > 0f ? shownFieldOfView : targetCamera.fieldOfView;
            targetCamera.fieldOfView = Mathf.Lerp(
                targetCamera.fieldOfView,
                targetFieldOfView,
                Time.deltaTime * 5f);
        }

        private void UpdateOrbitCamera()
        {
            if (targetCamera == null)
            {
                return;
            }

            var pivot = GetOrbitPivot() + currentPanOffset;
            var orbitRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            var targetPosition = pivot - orbitRotation * Vector3.forward * currentOrbitDistance;
            targetCamera.transform.position = Vector3.Lerp(
                targetCamera.transform.position,
                targetPosition,
                Time.deltaTime * 16f);
            targetCamera.transform.rotation = Quaternion.Slerp(
                targetCamera.transform.rotation,
                orbitRotation,
                Time.deltaTime * 16f);
            targetCamera.fieldOfView = Mathf.Lerp(
                targetCamera.fieldOfView,
                currentHiddenFieldOfView,
                Time.deltaTime * 5f);
        }

        private Vector3 GetOrbitPivot()
        {
            if (avatarRoot == null)
            {
                return Vector3.zero;
            }

            // Recomputing the avatar's combined renderer bounds every frame is expensive
            // (it walks every child Renderer and asks Unity for world-space bounds). Cache
            // the result and refresh on a short interval; orbit feels identical because
            // the avatar barely moves during viewer mode.
            var now = Time.unscaledTime;
            if (hasCachedPivot && now < cachedPivotRefreshAt)
            {
                return cachedPivot;
            }

            avatarRoot.GetComponentsInChildren(true, RendererBuffer);
            if (RendererBuffer.Count == 0)
            {
                cachedPivot = avatarRoot.position + Vector3.up * orbitPivotHeightOffset;
            }
            else
            {
                var bounds = RendererBuffer[0].bounds;
                for (var i = 1; i < RendererBuffer.Count; i++)
                {
                    bounds.Encapsulate(RendererBuffer[i].bounds);
                }
                cachedPivot = bounds.center + Vector3.up * orbitPivotHeightOffset;
            }

            RendererBuffer.Clear();
            hasCachedPivot = true;
            cachedPivotRefreshAt = now + PivotCacheLifetimeSeconds;
            return cachedPivot;
        }

        private bool TryGetAvatarBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            if (avatarRoot == null)
            {
                return false;
            }

            avatarRoot.GetComponentsInChildren(true, RendererBuffer);
            if (RendererBuffer.Count == 0)
            {
                RendererBuffer.Clear();
                return false;
            }

            bounds = RendererBuffer[0].bounds;
            for (var i = 1; i < RendererBuffer.Count; i++)
            {
                bounds.Encapsulate(RendererBuffer[i].bounds);
            }

            RendererBuffer.Clear();
            return true;
        }

        private static bool TryLoadCameraPreset(
            int presetIndex,
            out Vector3 position,
            out Quaternion rotation,
            out float fieldOfView)
        {
            var key = CameraPresetKey(presetIndex);
            position = Vector3.zero;
            rotation = Quaternion.identity;
            fieldOfView = 25f;
            if (PlayerPrefs.GetInt(key + ".Saved", 0) != 1)
            {
                return false;
            }

            position = new Vector3(
                PlayerPrefs.GetFloat(key + ".PositionX", 0f),
                PlayerPrefs.GetFloat(key + ".PositionY", 0f),
                PlayerPrefs.GetFloat(key + ".PositionZ", 0f));
            rotation = Quaternion.Euler(
                PlayerPrefs.GetFloat(key + ".RotationX", 0f),
                PlayerPrefs.GetFloat(key + ".RotationY", 0f),
                PlayerPrefs.GetFloat(key + ".RotationZ", 0f));
            fieldOfView = Mathf.Clamp(PlayerPrefs.GetFloat(key + ".FieldOfView", 25f), 10f, 80f);
            return true;
        }

        private static string CameraPresetKey(int presetIndex)
        {
            return $"{YuiPrefsKeys.CameraPresetPrefix}.{Mathf.Clamp(presetIndex, 1, CameraPresetSlots)}";
        }

        private float GetMaxOrbitDistance()
        {
            return Mathf.Max(maxOrbitDistance, 8.0f);
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f)
            {
                angle -= 360f;
            }

            while (angle < -180f)
            {
                angle += 360f;
            }

            return angle;
        }
    }
}
