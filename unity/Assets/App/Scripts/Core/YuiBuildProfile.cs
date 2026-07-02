namespace YuiPhysicalAI.Core
{
    public static class YuiBuildProfile
    {
        public const string Public = "public";
        public const string Current = Public;

        public static string DefaultAvatarSlot
        {
            get
            {
                return YuiAvatarSlots.UnityChanDefault;
            }
        }
    }
}
