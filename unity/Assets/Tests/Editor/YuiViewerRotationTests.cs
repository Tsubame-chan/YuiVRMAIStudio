using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.UI;
using YuiPhysicalAI.Avatar;

namespace YuiPhysicalAI.Tests
{
    public class YuiViewerRotationTests
    {
        GameObject host, avatar, cameraHost;
        YuiConsoleVisibilityController viewer;
        Quaternion authored;
        void Field(string name, object value) => typeof(YuiConsoleVisibilityController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(viewer, value);
        void Step(float dt) => typeof(YuiConsoleVisibilityController).GetMethod("UpdateAvatarRotation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(viewer, new object[] { dt });
        [SetUp] public void SetUp()
        {
            host = new GameObject("viewer"); avatar = new GameObject("avatar"); cameraHost = new GameObject("camera");
            authored = Quaternion.Euler(0, 25, 0); avatar.transform.rotation = authored;
            var camera = cameraHost.AddComponent<Camera>(); camera.transform.position = new Vector3(0, 1, -3);
            viewer = host.AddComponent<YuiConsoleVisibilityController>(); viewer.Configure(null, null, avatar.transform, camera);
            viewer.HideConsole(); Field("rotateAvatarInViewer", true); Field("defaultYaw", 0f); Field("currentYaw", -90f);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(host); Object.DestroyImmediate(avatar); Object.DestroyImmediate(cameraHost); }
        [TestCase(-1, true)]
        [TestCase(0, false)]
        [TestCase(1, true)]
        public void StartupDefaultsToBodyRotationAndPreservesExplicitChoice(int savedValue, bool expected)
        {
            const string key = "Yui.Viewer.RotateAvatar";
            var hadValue = PlayerPrefs.HasKey(key);
            var original = PlayerPrefs.GetInt(key);
            try
            {
                if (savedValue < 0) PlayerPrefs.DeleteKey(key);
                else PlayerPrefs.SetInt(key, savedValue);
                typeof(YuiConsoleVisibilityController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(viewer, null);
                Assert.That(viewer.ViewerRotatesAvatar, Is.EqualTo(expected));
            }
            finally
            {
                if (hadValue) PlayerPrefs.SetInt(key, original);
                else PlayerPrefs.DeleteKey(key);
            }
        }
        [Test] public void CameraOrbitKeepsPivotCenteredDuringFastTurnsAndDoesNotTurnModel()
        {
            Field("rotateAvatarInViewer", false);
            Field("cachedPivot", new Vector3(0,1,0)); Field("hasCachedPivot",true);
            var camera=cameraHost.GetComponent<Camera>();
            var method=typeof(YuiConsoleVisibilityController).GetMethod("UpdateOrbitCamera",BindingFlags.Instance|BindingFlags.NonPublic);
            for(var i=0;i<120;i++) {
                Field("currentYaw",i%2==0?160f:-160f);
                method.Invoke(viewer,new object[]{1f/60});
                var center=camera.WorldToViewportPoint(new Vector3(0,1,0));
                Assert.That(center.x,Is.EqualTo(.5f).Within(.001));Assert.That(center.y,Is.EqualTo(.5f).Within(.001));
                Assert.That(center.z,Is.GreaterThan(1));
            }
            Assert.That(Quaternion.Angle(authored,avatar.transform.rotation),Is.LessThan(.01));
        }
        [Test] public void ViewerTurnsBodyWithoutAQueuedSpeedLimitAndDrivesSecondaryMotion()
        {
            var hair = new GameObject("hair"); hair.transform.SetParent(avatar.transform, false);
            var tip = new GameObject("tip"); tip.transform.SetParent(hair.transform, false); tip.transform.localPosition = new Vector3(.2f, -.2f, .1f);
            var spring = avatar.AddComponent<YuiAvatarSpringMotion>(); spring.AddChain(hair.transform, .2f, 0);
            var cameraPosition = cameraHost.transform.position;
            Step(1f / 60); Assert.That(Quaternion.Angle(authored, avatar.transform.rotation), Is.GreaterThan(15f));
            var peak=0f;
            for (var i = 0; i < 30; i++) { Step(1f / 60); spring.Step(1f / 60); peak=Mathf.Max(peak,Quaternion.Angle(Quaternion.identity,hair.transform.localRotation)); }
            Assert.That(Quaternion.Angle(authored, avatar.transform.rotation), Is.GreaterThan(50));
            Assert.That(peak, Is.GreaterThan(.01));
            Assert.That(tip.transform.localPosition.magnitude, Is.EqualTo(Mathf.Sqrt(.09f)).Within(.0001));
            Assert.That(cameraHost.transform.position, Is.EqualTo(cameraPosition));
        }
        [Test] public void ReturnSwitchAndCameraEditRestoreAuthoredOrientation()
        {
            for (var i = 0; i < 60; i++) Step(1f / 60);
            Field("consoleVisible", true);
            for (var i = 0; i < 60; i++) Step(1f / 60);
            Assert.That(Quaternion.Angle(authored, avatar.transform.rotation), Is.LessThan(.01));
            viewer.HideConsole(); Field("currentYaw", -90f); Step(.03f);
            viewer.BeginCameraEditMode(); Step(.03f);
            Assert.That(Quaternion.Angle(authored, avatar.transform.rotation), Is.LessThan(.01));
            viewer.EndCameraEditMode(); viewer.HideConsole(); Field("currentYaw", -90f); Step(.03f);
            viewer.SetAvatarRoot(null, false);
            Assert.That(Quaternion.Angle(authored, avatar.transform.rotation), Is.LessThan(.01));
        }
    }
}
