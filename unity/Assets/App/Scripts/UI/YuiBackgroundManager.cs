using UnityEngine;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiBackgroundManager : MonoBehaviour
    {
        private const string PresetKey = YuiPrefsKeys.BackgroundPreset;
        private const string BackdropName = "Yui Runtime Backdrop";

        [SerializeField] private Camera targetCamera;
        [SerializeField] private YuiBackgroundPreset preset = YuiBackgroundPreset.Studio;
        [SerializeField] private bool loadSavedPreset = true;
        [SerializeField] private bool useBackdropPlane;
        [SerializeField] private Vector3 backdropPosition = new Vector3(0f, 1.35f, 4.1f);
        [SerializeField] private Vector3 backdropScale = new Vector3(7.0f, 4.2f, 1f);

        private GameObject backdrop;
        private Material backdropMaterial;

        public YuiBackgroundPreset Preset => preset;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (loadSavedPreset)
            {
                preset = (YuiBackgroundPreset)PlayerPrefs.GetInt(PresetKey, (int)preset);
            }

            ApplyPreset(preset, false);
        }

        private void OnDestroy()
        {
            if (backdropMaterial != null)
            {
                Destroy(backdropMaterial);
            }
        }

        public void SetPreset(int presetIndex)
        {
            SetPreset((YuiBackgroundPreset)Mathf.Clamp(
                presetIndex,
                0,
                System.Enum.GetValues(typeof(YuiBackgroundPreset)).Length - 1));
        }

        public void SetPreset(YuiBackgroundPreset nextPreset)
        {
            ApplyPreset(nextPreset, true);
        }

        public void ApplyNextPreset()
        {
            var count = System.Enum.GetValues(typeof(YuiBackgroundPreset)).Length;
            SetPreset((YuiBackgroundPreset)(((int)preset + 1) % count));
        }

        private void ApplyPreset(YuiBackgroundPreset nextPreset, bool save)
        {
            preset = nextPreset;
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            switch (preset)
            {
                case YuiBackgroundPreset.UnityDefault:
                    ApplyUnityDefault();
                    break;
                case YuiBackgroundPreset.WarmRoom:
                    ApplySolidBackdrop(new Color(0.82f, 0.78f, 0.68f), new Color(0.33f, 0.30f, 0.25f), 0.76f);
                    break;
                case YuiBackgroundPreset.NightDesk:
                    ApplySolidBackdrop(new Color(0.045f, 0.055f, 0.075f), new Color(0.12f, 0.16f, 0.23f), 0.64f);
                    break;
                case YuiBackgroundPreset.SoftStage:
                    ApplySolidBackdrop(new Color(0.62f, 0.66f, 0.69f), new Color(0.76f, 0.80f, 0.82f), 0.82f);
                    break;
                default:
                    ApplySolidBackdrop(new Color(0.16f, 0.18f, 0.19f), new Color(0.54f, 0.57f, 0.56f), 0.78f);
                    break;
            }

            if (save)
            {
                PlayerPrefs.SetInt(PresetKey, (int)preset);
                PlayerPrefs.Save();
            }
        }

        private void ApplyUnityDefault()
        {
            if (targetCamera != null)
            {
                targetCamera.clearFlags = CameraClearFlags.Skybox;
            }

            if (backdrop != null)
            {
                backdrop.SetActive(false);
            }
        }

        private void ApplySolidBackdrop(Color cameraColor, Color backdropColor, float ambient)
        {
            if (targetCamera != null)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = cameraColor;
            }

            RenderSettings.ambientLight = Color.white * ambient;
            if (!useBackdropPlane)
            {
                if (backdrop != null)
                {
                    backdrop.SetActive(false);
                }

                return;
            }

            EnsureBackdrop();
            if (backdrop != null)
            {
                backdrop.SetActive(true);
                backdrop.transform.position = backdropPosition;
                backdrop.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                backdrop.transform.localScale = backdropScale;
            }

            if (backdropMaterial != null)
            {
                backdropMaterial.color = backdropColor;
            }
        }

        private void EnsureBackdrop()
        {
            if (backdrop != null)
            {
                return;
            }

            backdrop = GameObject.Find(BackdropName);
            if (backdrop == null)
            {
                backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
                backdrop.name = BackdropName;
                var collider = backdrop.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
            }

            var renderer = backdrop.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            backdropMaterial = new Material(shader);
            renderer.sharedMaterial = backdropMaterial;
        }
    }

    public enum YuiBackgroundPreset
    {
        Studio,
        WarmRoom,
        NightDesk,
        SoftStage,
        UnityDefault,
    }
}
