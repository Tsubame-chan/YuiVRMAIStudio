using System.Collections.Generic;
using UnityEngine;

namespace YuiPhysicalAI.Core
{
    public static class YuiRealtimeLog
    {
        public static bool VerboseEnabled => PlayerPrefs.GetInt(YuiPrefsKeys.RealtimeVerboseLogging, 0) == 1;

        public static void Verbose(string message)
        {
            if (VerboseEnabled)
            {
                Debug.Log(message);
            }
        }

        public static string FormatEvents(IReadOnlyList<string> events, bool verbose)
        {
            if (events == null || events.Count == 0)
            {
                return "none";
            }

            if (verbose || events.Count <= 4)
            {
                return string.Join(",", events);
            }

            return $"{events.Count} events: {events[0]}...{events[events.Count - 1]}";
        }
    }
}
