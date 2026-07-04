using UnityEngine;
using YuiPhysicalAI.Backend;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiStandaloneWindowBootstrap : MonoBehaviour
    {
        public const int DefaultWindowWidth = 1440;
        public const int DefaultWindowHeight = 2560;

        [SerializeField] private int windowWidth = DefaultWindowWidth;
        [SerializeField] private int windowHeight = DefaultWindowHeight;
#pragma warning disable 0414
        [SerializeField] private bool autoStartBundledBackend = true;
#pragma warning restore 0414

        public void ConfigureWindowSize(int width, int height)
        {
            windowWidth = Mathf.Max(360, width);
            windowHeight = Mathf.Max(640, height);
        }

        private void Awake()
        {
#if (UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN) && !UNITY_EDITOR
            if (autoStartBundledBackend
                && GetComponent<YuiDesktopBackendSupervisor>() == null
                && YuiSceneObjectFinder.FindFirst<YuiDesktopBackendSupervisor>() == null)
            {
                gameObject.AddComponent<YuiDesktopBackendSupervisor>();
            }
#endif
        }

        private void Start()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            if (GetComponent<YuiWindowResolutionController>() == null
                && YuiPhysicalAI.Core.YuiSceneObjectFinder.FindFirst<YuiWindowResolutionController>() == null)
            {
                Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
            }
#endif
        }
    }
}
