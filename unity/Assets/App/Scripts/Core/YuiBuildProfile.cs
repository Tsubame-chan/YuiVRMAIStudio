namespace YuiPhysicalAI.Core
{
    public static class YuiBuildProfile
    {
        public const string Public = "public";
        public const string Personal = "personal";

#if YUI_PROFILE_PERSONAL
        public const string Current = Personal;
#else
        public const string Current = Public;
#endif

        public static string DefaultAvatarSlot
        {
            get
            {
#if YUI_PROFILE_PERSONAL
                return YuiAvatarSlots.DemoKikyo;
#else
                return YuiAvatarSlots.UnityChanDefault;
#endif
            }
        }
    }
}
