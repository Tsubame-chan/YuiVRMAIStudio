using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiCameraAspectTests
    {
        [Test]
        public void AvatarThumbnail_DoesNotFreezeMainCameraAspect()
        {
            var mains=UnityEngine.Object.FindObjectsOfType<Camera>().Where(c=>c.CompareTag("MainCamera")).ToArray();
            foreach(var c in mains)c.tag="Untagged";
            var go=new GameObject("Thumbnail aspect regression",typeof(Camera));go.tag="MainCamera";
            var camera=go.GetComponent<Camera>();camera.ResetAspect();
            var entry=new YuiAvatarLibrary.Entry{id="aspect-test-"+Guid.NewGuid().ToString("N")};
            var path=Path.Combine(YuiAvatarLibrary.DirectoryPath,entry.id+".png");
            Directory.CreateDirectory(YuiAvatarLibrary.DirectoryPath);
            try {
                Assert.That(Camera.main,Is.SameAs(camera));
                var before=camera.aspect;
                YuiAvatarLibrary.CaptureThumbnail(entry);
                Assert.That(File.Exists(path),Is.True,"Thumbnail must still be produced");
                Assert.That(camera.aspect,Is.EqualTo(before).Within(.001f));
                camera.rect=new Rect(0,0,.5f,1);
                Assert.That(camera.aspect,Is.EqualTo((float)camera.pixelWidth/camera.pixelHeight).Within(.003f),"After capture, aspect must still follow a changing viewport");
            }finally {File.Delete(path);UnityEngine.Object.DestroyImmediate(go);foreach(var c in mains)if(c!=null)c.tag="MainCamera";}
        }
        [Test]
        public void PortraitViewport_ReleasesAnObsoleteAspectOverride()
        {
            var go=new GameObject("Viewport aspect regression",typeof(Camera));
            try {
                var camera=go.GetComponent<Camera>();var viewport=go.AddComponent<YuiPortraitViewport>();
                camera.aspect=.4f;viewport.ApplyViewport();
                Assert.That(camera.aspect,Is.EqualTo((float)camera.pixelWidth/camera.pixelHeight).Within(.003f));
            }finally {UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
