using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public static class YuiAvatarFraming
    {
        // The face is the conversational focus. Keep it above the console and
        // leave headroom consistently, independent of avatar/world-space scale.
        public const float FaceViewportY = .75f;
        public static Quaternion CameraRotation => Quaternion.Euler(8f, 0f, 0f);

        public static Vector3 CameraPosition(Vector3 face, float bodyHeight, float verticalFov)
        {
            var distance = Mathf.Max(.1f, bodyHeight) * 1.9f;
            var halfHeight = distance * Mathf.Tan(Mathf.Clamp(verticalFov, 10f, 80f) * Mathf.Deg2Rad * .5f);
            return face - CameraRotation * new Vector3(0f, (FaceViewportY - .5f) * 2f * halfHeight, distance);
        }
    }
}
