using UnityEngine;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiBackgroundManager : MonoBehaviour
    {
        private const string PresetKey = YuiPrefsKeys.BackgroundPreset;
        private const string BackdropName = "Yui Runtime Backdrop";

        [SerializeField] private Camera targetCamera;
        [SerializeField] private YuiBackgroundPreset preset = YuiBackgroundPreset.SoftGradient;
        [SerializeField] private bool loadSavedPreset = true;
        [SerializeField] private bool useBackdropPlane;
        [SerializeField] private Vector3 backdropPosition = new Vector3(0f, 1.35f, 4.1f);
        [SerializeField] private Vector3 backdropScale = new Vector3(7.0f, 4.2f, 1f);

        private GameObject backdrop;
        private Material backdropMaterial;
        private Material gradientMaterial;
        private Skybox cameraSkybox;
        private Material originalSkyboxMaterial;
        private bool originalSkyboxEnabled;
        private bool ownsSkybox;
        public static readonly string[] OptionLabels = { "Soft Charcoal", "Soft Sand", "Soft Midnight", "Soft Mist", "Skybox", "Soft Gradient · Default" };

        public YuiBackgroundPreset Preset => preset;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (loadSavedPreset)
            {
                preset = (YuiBackgroundPreset)PlayerPrefs.GetInt(PresetKey, (int)YuiBackgroundPreset.SoftGradient);
            }

            ApplyPreset(preset, false);
        }

        private void OnDestroy()
        {
            RestoreCameraSkybox();
            if (gradientMaterial != null) Destroy(gradientMaterial);
            if (ownsSkybox && cameraSkybox != null) Destroy(cameraSkybox);
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

            RestoreCameraSkybox();

            switch (preset)
            {
                case YuiBackgroundPreset.UnityDefault:
                    ApplyUnityDefault();
                    break;
                default:
                    ApplySoftGradient();
                    break;
            }

            if (save)
            {
                PlayerPrefs.SetInt(PresetKey, (int)preset);
                PlayerPrefs.Save();
            }
        }

        private void RestoreCameraSkybox()
        {
            if (cameraSkybox == null) return;
            cameraSkybox.material = originalSkyboxMaterial;
            cameraSkybox.enabled = originalSkyboxEnabled;
        }

        private void ApplySoftGradient()
        {
            // Backgrounds affect the camera only. Avatar lighting belongs to the scene;
            // adding bright ambient light here washes out MToon imports.
            ApplySolidBackdrop(new Color(.11f,.12f,.16f),Color.gray);
            if (backdrop != null) backdrop.SetActive(false);
            if (targetCamera == null) return;
            if (gradientMaterial == null)
            {
                var shader = Resources.Load<Shader>("YuiBackgrounds/SoftGradient");
                if (shader == null || !shader.isSupported) return; // readable solid fallback
                gradientMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            if (cameraSkybox == null)
            {
                cameraSkybox = targetCamera.GetComponent<Skybox>();
                ownsSkybox = cameraSkybox == null;
                if (ownsSkybox) cameraSkybox = targetCamera.gameObject.AddComponent<Skybox>();
                originalSkyboxMaterial = cameraSkybox.material;
                originalSkyboxEnabled = !ownsSkybox && cameraSkybox.enabled;
            }
            var bottom = new Color(.115f,.125f,.165f);
            var top = new Color(.075f,.084f,.115f);
            var glow = new Color(.18f,.165f,.21f);
            switch (preset)
            {
                case YuiBackgroundPreset.Studio:
                    bottom=new Color(.13f,.14f,.15f); top=new Color(.07f,.08f,.09f); glow=new Color(.19f,.19f,.19f); break;
                case YuiBackgroundPreset.WarmRoom:
                    bottom=new Color(.43f,.39f,.33f); top=new Color(.29f,.26f,.23f); glow=new Color(.24f,.22f,.18f); break;
                case YuiBackgroundPreset.NightDesk:
                    bottom=new Color(.075f,.12f,.18f); top=new Color(.04f,.065f,.12f); glow=new Color(.13f,.19f,.26f); break;
                case YuiBackgroundPreset.SoftStage:
                    bottom=new Color(.36f,.41f,.43f); top=new Color(.24f,.29f,.32f); glow=new Color(.25f,.27f,.28f); break;
            }
            gradientMaterial.SetVector("_BottomColor",bottom);
            gradientMaterial.SetVector("_TopColor",top);
            gradientMaterial.SetVector("_GlowColor",glow);
            cameraSkybox.material = gradientMaterial;
            cameraSkybox.enabled = true;
            targetCamera.clearFlags = CameraClearFlags.Skybox;
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

        private void ApplySolidBackdrop(Color cameraColor, Color backdropColor)
        {
            if (targetCamera != null)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = cameraColor;
            }

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
        SoftGradient,
    }
}
