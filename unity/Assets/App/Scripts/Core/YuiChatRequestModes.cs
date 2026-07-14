using System;

namespace YuiPhysicalAI.Core
{
    public static class YuiChatRequestModes
    {
        public const string Talk = "standard";
        public const string Work = "work";

        public static string Normalize(string value)
        {
            return string.Equals(value, Work, StringComparison.OrdinalIgnoreCase)
                ? Work
                : Talk;
        }

        public static bool IsWork(string value)
        {
            return string.Equals(Normalize(value), Work, StringComparison.Ordinal);
        }
    }
}
