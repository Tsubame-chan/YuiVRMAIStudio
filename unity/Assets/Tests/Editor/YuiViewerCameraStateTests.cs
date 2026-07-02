using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiViewerCameraStateTests
    {
        [Test]
        public void UpdateIdle_PreservesAdjustedViewerState()
        {
            var state = YuiViewerCameraState.CreateDefault(
                yaw: 0f,
                pitch: 0f,
                distance: 3f,
                fieldOfView: 22f);

            state.ApplyOrbitDelta(new Vector2(100f, -50f), 0.25f);
            state.ApplyZoom(-1f, 0.35f, 8f);
            state.ApplyPan(
                new Vector2(80f, 30f),
                Vector3.right,
                Vector3.up,
                0.0022f);

            var adjusted = state;

            state.UpdateIdle(
                returnToDefault: false,
                deltaTime: 1f,
                returnSpeed: 4f,
                defaultFieldOfView: 22f);

            Assert.AreEqual(adjusted.Yaw, state.Yaw);
            Assert.AreEqual(adjusted.Pitch, state.Pitch);
            Assert.AreEqual(adjusted.Distance, state.Distance);
            Assert.AreEqual(adjusted.PanOffset, state.PanOffset);
        }

        [Test]
        public void UpdateIdle_CanReturnToDefaultWhenRequested()
        {
            var state = YuiViewerCameraState.CreateDefault(
                yaw: 0f,
                pitch: 0f,
                distance: 3f,
                fieldOfView: 22f);

            state.ApplyOrbitDelta(new Vector2(100f, -50f), 0.25f);
            state.ApplyZoom(-1f, 0.35f, 8f);

            state.UpdateIdle(
                returnToDefault: true,
                deltaTime: 1f,
                returnSpeed: 4f,
                defaultFieldOfView: 22f);

            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(state.Yaw, state.DefaultYaw)), 25f);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(state.Pitch, state.DefaultPitch)), 13f);
            Assert.Greater(state.Distance, 2.7f);
        }

        [Test]
        public void SnapToDefault_ClearsAdjustedViewerStateBeforeConsoleReturn()
        {
            var state = YuiViewerCameraState.CreateDefault(
                yaw: 0f,
                pitch: 0f,
                distance: 3f,
                fieldOfView: 22f);

            state.ApplyOrbitDelta(new Vector2(720f, -240f), 0.25f);
            state.ApplyZoom(-1f, 0.35f, 8f);
            state.ApplyPan(
                new Vector2(120f, 80f),
                Vector3.right,
                Vector3.up,
                0.0022f);

            state.SnapToDefault(fieldOfView: 27f);

            Assert.AreEqual(state.DefaultYaw, state.Yaw);
            Assert.AreEqual(state.DefaultPitch, state.Pitch);
            Assert.AreEqual(state.DefaultDistance, state.Distance);
            Assert.AreEqual(Vector3.zero, state.PanOffset);
            Assert.AreEqual(27f, state.FieldOfView);
        }
    }
}
