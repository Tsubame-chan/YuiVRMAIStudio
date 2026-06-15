using UnityEngine;

namespace YuiPhysicalAI.UI
{
    [RequireComponent(typeof(Camera))]
    public sealed class YuiPortraitViewport : MonoBehaviour
    {
        [SerializeField] private float targetAspect = 9f / 16f;
        [SerializeField] private bool pillarboxLandscapePreview = true;
        [SerializeField] private bool clearOutsideViewport = true;
        [SerializeField] private bool useCameraBackgroundForOutsideViewport = true;
        [SerializeField] private Color outsideViewportColor = new Color(0.16f, 0.18f, 0.19f);

        private Camera targetCamera;
        private Camera clearCamera;
        private int lastWidth;
        private int lastHeight;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            ApplyViewport();
        }

        private void OnDisable()
        {
            if (clearCamera != null)
            {
                clearCamera.enabled = false;
            }
        }

        private void Update()
        {
            if (Screen.width == lastWidth && Screen.height == lastHeight)
            {
                SyncClearCameraColor();
                return;
            }

            ApplyViewport();
        }

        public void ApplyViewport()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            lastWidth = Mathf.Max(1, Screen.width);
            lastHeight = Mathf.Max(1, Screen.height);

            if (!pillarboxLandscapePreview)
            {
                targetCamera.rect = new Rect(0f, 0f, 1f, 1f);
                UpdateClearCamera(false);
                return;
            }

            var currentAspect = (float)lastWidth / lastHeight;
            if (currentAspect <= targetAspect)
            {
                targetCamera.rect = new Rect(0f, 0f, 1f, 1f);
                UpdateClearCamera(false);
                return;
            }

            var width = targetAspect / currentAspect;
            targetCamera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
            UpdateClearCamera(true);
        }

        private void UpdateClearCamera(bool viewportIsLetterboxed)
        {
            if (!clearOutsideViewport || !viewportIsLetterboxed)
            {
                if (clearCamera != null)
                {
                    clearCamera.enabled = false;
                }
                return;
            }

            if (clearCamera == null)
            {
                clearCamera = FindExistingClearCamera();
                if (clearCamera == null)
                {
                    var clearObject = new GameObject("Yui Letterbox Clear Camera");
                    clearObject.transform.SetParent(transform, false);
                    clearCamera = clearObject.AddComponent<Camera>();
                }
            }

            clearCamera.enabled = true;
            clearCamera.rect = new Rect(0f, 0f, 1f, 1f);
            clearCamera.depth = targetCamera.depth - 100f;
            clearCamera.clearFlags = CameraClearFlags.SolidColor;
            SyncClearCameraColor();
            clearCamera.cullingMask = 0;
            clearCamera.orthographic = true;
            clearCamera.allowHDR = targetCamera.allowHDR;
            clearCamera.allowMSAA = targetCamera.allowMSAA;
            clearCamera.targetDisplay = targetCamera.targetDisplay;
            clearCamera.targetTexture = targetCamera.targetTexture;
        }

        private void SyncClearCameraColor()
        {
            if (clearCamera == null || !clearCamera.enabled)
            {
                return;
            }

            clearCamera.backgroundColor = useCameraBackgroundForOutsideViewport && targetCamera != null
                ? targetCamera.backgroundColor
                : outsideViewportColor;
        }

        private Camera FindExistingClearCamera()
        {
            var existing = transform.Find("Yui Letterbox Clear Camera");
            return existing != null ? existing.GetComponent<Camera>() : null;
        }
    }
}
