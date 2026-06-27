using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public struct YuiViewerCameraState
    {
        public float DefaultYaw { get; private set; }
        public float DefaultPitch { get; private set; }
        public float DefaultDistance { get; private set; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Distance { get; private set; }
        public float FieldOfView { get; private set; }
        public Vector3 PanOffset { get; private set; }

        public static YuiViewerCameraState CreateDefault(
            float yaw,
            float pitch,
            float distance,
            float fieldOfView)
        {
            return new YuiViewerCameraState
            {
                DefaultYaw = yaw,
                DefaultPitch = pitch,
                DefaultDistance = distance,
                Yaw = yaw,
                Pitch = pitch,
                Distance = distance,
                FieldOfView = fieldOfView,
                PanOffset = Vector3.zero
            };
        }

        public static YuiViewerCameraState Create(
            float defaultYaw,
            float defaultPitch,
            float defaultDistance,
            float yaw,
            float pitch,
            float distance,
            float fieldOfView,
            Vector3 panOffset)
        {
            return new YuiViewerCameraState
            {
                DefaultYaw = defaultYaw,
                DefaultPitch = defaultPitch,
                DefaultDistance = defaultDistance,
                Yaw = yaw,
                Pitch = pitch,
                Distance = distance,
                FieldOfView = fieldOfView,
                PanOffset = panOffset
            };
        }

        public void ResetToDefault()
        {
            Yaw = DefaultYaw;
            Pitch = DefaultPitch;
            Distance = DefaultDistance;
            PanOffset = Vector3.zero;
        }

        public void SnapToDefault(float fieldOfView)
        {
            ResetToDefault();
            FieldOfView = fieldOfView;
        }

        public void UpdateIdle(
            bool returnToDefault,
            float deltaTime,
            float returnSpeed,
            float defaultFieldOfView)
        {
            if (returnToDefault)
            {
                var t = Mathf.Clamp01(deltaTime * returnSpeed);
                Yaw = Mathf.LerpAngle(Yaw, DefaultYaw, t);
                Pitch = Mathf.LerpAngle(Pitch, DefaultPitch, t);
                Distance = Mathf.Lerp(Distance, DefaultDistance, t);
                PanOffset = Vector3.Lerp(PanOffset, Vector3.zero, t);
            }

            FieldOfView = Mathf.Lerp(FieldOfView, defaultFieldOfView, Mathf.Clamp01(deltaTime * returnSpeed));
        }

        public void ApplyOrbitDelta(Vector2 screenDelta, float rotateSensitivity)
        {
            Yaw -= screenDelta.x * rotateSensitivity;
            Pitch += screenDelta.y * rotateSensitivity;
        }

        public void ApplyZoom(float distanceDelta, float minDistance, float maxDistance)
        {
            Distance = Mathf.Clamp(Distance + distanceDelta, minDistance, maxDistance);
        }

        public void ApplyPan(
            Vector2 screenDelta,
            Vector3 cameraRight,
            Vector3 cameraUp,
            float panSensitivity)
        {
            var scale = Mathf.Max(0.45f, Distance) * panSensitivity;
            PanOffset += (-cameraRight * screenDelta.x + -cameraUp * screenDelta.y) * scale;
        }
    }
}
