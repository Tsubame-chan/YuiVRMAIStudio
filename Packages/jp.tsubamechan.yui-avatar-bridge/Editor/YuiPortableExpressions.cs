using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniVRM10;

namespace Yui.AvatarBridge.Editor
{
    public static class YuiPortableExpressions
    {
        public static MorphTargetBinding[] Blink(GameObject root, object descriptor)
        {
            var settings = YuiAvatarBridgeAnalyzer.GetMemberValue(descriptor, "customEyeLookSettings");
            var type = YuiAvatarBridgeAnalyzer.GetMemberValue(settings, "eyelidType")?.ToString();
            var enabled = YuiAvatarBridgeAnalyzer.GetMemberValue(descriptor, "enableEyeLook");
            if (type == "Blendshapes" && !(enabled is bool value && !value))
            {
                var renderer = YuiAvatarBridgeAnalyzer.GetMemberValue(settings, "eyelidsSkinnedMesh") as SkinnedMeshRenderer;
                var indices = YuiAvatarBridgeAnalyzer.GetMemberValue(settings, "eyelidsBlendshapes") as int[];
                if (IsVisibleChild(root, renderer) && indices != null && indices.Length > 0 && indices[0] >= 0 && indices[0] < renderer.sharedMesh.blendShapeCount)
                    return new [] { Binding(root, renderer, indices[0]) };
            }
            if (type == "Bones") return new MorphTargetBinding[0];
            var bindings = new List<MorphTargetBinding>();
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!IsVisibleChild(root, renderer)) continue;
                var combined = YuiAvatarShapeNames.Find(renderer.sharedMesh, "Blink", "Fcl_EYE_Close", "eye_close", "eyes_closed", "まばたき");
                if (combined >= 0) { bindings.Add(Binding(root, renderer, combined)); continue; }
                var left = YuiAvatarShapeNames.Find(renderer.sharedMesh, "Blink_L", "BlinkLeft", "eyeBlinkLeft", "Fcl_EYE_Close_L");
                var right = YuiAvatarShapeNames.Find(renderer.sharedMesh, "Blink_R", "BlinkRight", "eyeBlinkRight", "Fcl_EYE_Close_R");
                if (left >= 0) bindings.Add(Binding(root, renderer, left));
                if (right >= 0 && right != left) bindings.Add(Binding(root, renderer, right));
            }
            return bindings.ToArray();
        }

        public static MorphTargetBinding[] KnownShape(GameObject root, params string[] names)
        {
            return root.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(r => IsVisibleChild(root, r))
                .Select(r => (renderer: r, index: YuiAvatarShapeNames.Find(r.sharedMesh, names)))
                .Where(pair => pair.index >= 0).Select(pair => Binding(root, pair.renderer, pair.index)).ToArray();
        }

        private static bool IsVisibleChild(GameObject root, SkinnedMeshRenderer renderer) => renderer != null && renderer.sharedMesh != null
            && renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.transform.IsChildOf(root.transform);
        private static MorphTargetBinding Binding(GameObject root, SkinnedMeshRenderer renderer, int index) =>
            new MorphTargetBinding(AnimationUtility.CalculateTransformPath(renderer.transform, root.transform), index, 1f);
    }
}
