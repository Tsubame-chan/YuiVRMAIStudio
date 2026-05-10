namespace YuiPhysicalAI.Core
{
    /// <summary>
    /// Canonical avatar slot identifiers persisted in PlayerPrefs and used by
    /// settings UI, runtime switching, and distribution builds.
    /// </summary>
    public static class YuiAvatarSlots
    {
        public const string DemoKikyo = "demo_kikyo";
        public const string UnityChanDefault = "unitychan_default";
        public const string CustomVrm = "custom_vrm";
        public const string CustomVrm1 = "custom_vrm_1";
        public const string CustomVrm2 = "custom_vrm_2";
        public const string CustomVrm3 = "custom_vrm_3";
        public const string CustomVrm4 = "custom_vrm_4";

        // Historical value kept only for one-time migration from early alpha builds.
        public const string LegacyDistributionDefault = "distribution_default";

        public static string Normalize(string value)
        {
            if (string.Equals(value, UnityChanDefault, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, LegacyDistributionDefault, System.StringComparison.OrdinalIgnoreCase))
            {
                return UnityChanDefault;
            }

            if (string.Equals(value, CustomVrm, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, CustomVrm1, System.StringComparison.OrdinalIgnoreCase))
            {
                return CustomVrm1;
            }

            if (string.Equals(value, CustomVrm2, System.StringComparison.OrdinalIgnoreCase))
            {
                return CustomVrm2;
            }

            if (string.Equals(value, CustomVrm3, System.StringComparison.OrdinalIgnoreCase))
            {
                return CustomVrm3;
            }

            if (string.Equals(value, CustomVrm4, System.StringComparison.OrdinalIgnoreCase))
            {
                return CustomVrm4;
            }

            return UnityChanDefault;
        }

        public static bool IsCustomVrm(string value)
        {
            var normalized = Normalize(value);
            return string.Equals(normalized, CustomVrm1, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, CustomVrm2, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, CustomVrm3, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, CustomVrm4, System.StringComparison.OrdinalIgnoreCase);
        }

        public static int CustomVrmIndex(string value)
        {
            switch (Normalize(value))
            {
                case CustomVrm2:
                    return 2;
                case CustomVrm3:
                    return 3;
                case CustomVrm4:
                    return 4;
                default:
                    return 1;
            }
        }

        public static string CustomVrmSlot(int index)
        {
            switch (index)
            {
                case 2:
                    return CustomVrm2;
                case 3:
                    return CustomVrm3;
                case 4:
                    return CustomVrm4;
                default:
                    return CustomVrm1;
            }
        }
    }
}
