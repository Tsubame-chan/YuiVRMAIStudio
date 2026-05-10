using UnityEngine;

namespace YuiPhysicalAI.Avatar
{
    [CreateAssetMenu(
        fileName = "YuiPresenceAnimationProfile",
        menuName = "Yui/Presence Animation Profile")]
    public sealed class YuiPresenceAnimationProfile : ScriptableObject
    {
        [Header("Idles")]
        public AnimationClip calmIdle;
        public AnimationClip cheerfulIdle;
        public AnimationClip thinkingIdle;

        [Header("Gestures")]
        public AnimationClip nodGesture;
        public AnimationClip waveGesture;
        public AnimationClip talkGesture;
        public AnimationClip surpriseGesture;

        public AnimationClip GetIdle(YuiPresenceIdleStyle style)
        {
            switch (style)
            {
                case YuiPresenceIdleStyle.Cheerful:
                    return cheerfulIdle != null ? cheerfulIdle : calmIdle;
                case YuiPresenceIdleStyle.Thinking:
                    return thinkingIdle != null ? thinkingIdle : calmIdle;
                default:
                    return calmIdle;
            }
        }

        public AnimationClip GetGesture(YuiPresenceGesture gesture)
        {
            switch (gesture)
            {
                case YuiPresenceGesture.Nod:
                    return nodGesture;
                case YuiPresenceGesture.Wave:
                    return waveGesture;
                case YuiPresenceGesture.Talk:
                    return talkGesture;
                case YuiPresenceGesture.Surprise:
                    return surpriseGesture;
                default:
                    return null;
            }
        }
    }

    public enum YuiPresenceIdleStyle
    {
        Calm,
        Cheerful,
        Thinking,
    }

    public enum YuiPresenceGesture
    {
        None,
        Nod,
        Wave,
        Talk,
        Surprise,
    }
}
