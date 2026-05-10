using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiStandaloneWindowBootstrap : MonoBehaviour
    {
        public const int DefaultWindowWidth = 576;
        public const int DefaultWindowHeight = 1024;

        [SerializeField] private int windowWidth = DefaultWindowWidth;
        [SerializeField] private int windowHeight = DefaultWindowHeight;

        public void ConfigureWindowSize(int width, int height)
        {
            windowWidth = Mathf.Max(360, width);
            windowHeight = Mathf.Max(640, height);
        }

        private void Start()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            if (GetComponent<YuiWindowResolutionController>() == null)
            {
                Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
            }
#endif
        }
    }
}
