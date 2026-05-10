using UnityEngine;

namespace YuiPhysicalAI.Core
{
    public static class YuiSceneObjectFinder
    {
        public static T FindFirst<T>(bool includeInactive = false) where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
#pragma warning disable 618
            return Object.FindObjectOfType<T>(includeInactive);
#pragma warning restore 618
#endif
        }

        public static T[] FindAll<T>(bool includeInactive = false) where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
#pragma warning disable 618
            return Object.FindObjectsOfType<T>(includeInactive);
#pragma warning restore 618
#endif
        }
    }
}
