#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace YuiPhysicalAI.Platform
{
    public static partial class YuiPlatformAudioSession
    {
        static partial void PrepareForAssistantPlaybackPlatform()
        {
            iOSNativeMicrophonePlugin_ForcePlaybackSpeakerOutput();
        }

        [DllImport("__Internal")]
        private static extern void iOSNativeMicrophonePlugin_ForcePlaybackSpeakerOutput();
    }
}
#endif
