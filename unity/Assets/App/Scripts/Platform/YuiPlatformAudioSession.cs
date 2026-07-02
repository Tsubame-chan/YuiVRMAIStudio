namespace YuiPhysicalAI.Platform
{
    public static partial class YuiPlatformAudioSession
    {
        public static void PrepareForAssistantPlayback()
        {
            PrepareForAssistantPlaybackPlatform();
        }

        static partial void PrepareForAssistantPlaybackPlatform();
    }
}
