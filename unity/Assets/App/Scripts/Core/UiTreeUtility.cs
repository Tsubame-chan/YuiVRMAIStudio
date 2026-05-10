using UnityEngine;

namespace YuiPhysicalAI.Core
{
    public static class UiTreeUtility
    {
        public static Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == childName)
                {
                    return child;
                }

                var found = FindDeepChild(child, childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
