using UnityEngine;

namespace YuiPhysicalAI.UI
{
    [RequireComponent(typeof(Camera))]
    public sealed class YuiPortraitViewport : MonoBehaviour
    {
        [SerializeField] private float targetAspect = 9f / 16f;
        [SerializeField] private bool pillarboxLandscapePreview = true;

        private Camera targetCamera;
        private int lastWidth;
        private int lastHeight;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            ApplyViewport();
        }

        private void Update()
        {
            if (Screen.width == lastWidth && Screen.height == lastHeight)
            {
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
                return;
            }

            var currentAspect = (float)lastWidth / lastHeight;
            if (currentAspect <= targetAspect)
            {
                targetCamera.rect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            var width = targetAspect / currentAspect;
            targetCamera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }
    }
}
