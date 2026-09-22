using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.Avatar;
using Newtonsoft.Json.Linq;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiAvatarBetaTests
    {
        [Test]
        public void AppearanceNameBelongsToCharacterAndFileRatherThanRuntimeSlot()
        {
            var dir = Path.Combine(Path.GetTempPath(), "yui-appearance-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try {
                var source = Path.Combine(dir, "first.vrm"); File.WriteAllText(source, "first");
                var other = Path.Combine(dir, "second.vrm"); File.WriteAllText(other, "second");
                var store = new YuiAvatarLibraryStore(Path.Combine(dir, "library"));
                var first = store.Register(source); var second = store.Register(other);
                store.RenameAppearance(first.id, first.file, "Winter outfit");
                var updated = store.Register(other, first.id);
                Assert.AreEqual(first.id, updated.id);
                Assert.AreEqual(first.name, updated.name);
                var rows = store.Read();
                Assert.AreEqual("Winter outfit", rows.Find(e => e.id == first.id).appearances.Find(a => a.file == first.file).name);
                Assert.AreEqual("second", rows.Find(e => e.id == second.id).appearances[0].name);
            } finally { Directory.Delete(dir, true); }
        }

        [Test] public void RecentDialogueIsBoundedAndIsolatedAcrossCharactersModesAndRestart()
        {
            var dir = Path.Combine(Path.GetTempPath(), "yui-dialogue-" + Guid.NewGuid().ToString("N"));
            try {
                var store = new YuiCharacterDialogueStore(dir);
                for (var i = 0; i < 10; i++) store.Append("a", "talk", "message" + i, new string('a', 900));
                var rows = new YuiCharacterDialogueStore(dir).Read("a", "talk");
                Assert.AreEqual(4, rows.Count); Assert.AreEqual("message6", rows[0].User); Assert.AreEqual(600, rows[0].Assistant.Length);
                Assert.IsEmpty(store.Read("b", "talk")); Assert.IsEmpty(store.Read("a", "work"));
                store.Append("b", "talk", "separate", "reply"); store.Clear("a", "talk");
                Assert.IsEmpty(store.Read("a", "talk")); Assert.AreEqual(1, store.Read("b", "talk").Count);
            } finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
        [Test] public void LocalAndDirectPromptsReceivePriorDialogueAndDirectSecretOmitsIt()
        {
            var extra = new System.Collections.Generic.Dictionary<string, object> { [YuiCharacterDialogueStore.ContextKey] = "Yesterday we chose a blue hat." };
            StringAssert.Contains("blue hat", YuiPhysicalAI.LocalAI.YuiLocalAiPromptBuilder.BuildPrompt(new YuiPhysicalAI.LocalAI.YuiLocalAiChatRequest { Message = "What color?", Extra = extra }));
            var request = new YuiPhysicalAI.Api.ChatRequest { Message = "What color?", Context = new YuiPhysicalAI.Api.RequestContext { Extra = extra } };
            StringAssert.Contains("blue hat", YuiPhysicalAI.Api.YuiDirectOpenAiClient.BuildResponsesPayload(request, "test").ToString());
            request.Secret = true;
            StringAssert.DoesNotContain("blue hat", YuiPhysicalAI.Api.YuiDirectOpenAiClient.BuildResponsesPayload(request, "test").ToString());
        }
        [Test] public void AppearanceUpdateRetainsCharacterIdentityAndSettingsAcrossRestart()
        {
            var dir = Path.Combine(Path.GetTempPath(), "yui-character-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try {
                var source = Path.Combine(dir, "first.vrm"); File.WriteAllText(source, "appearance-v1");
                var library = new YuiAvatarLibraryStore(Path.Combine(dir, "library")); var first = library.Register(source);
                var profiles = new YuiCharacterProfileStore(Path.Combine(dir, "profiles"));
                profiles.Save(first.id, new YuiCharacterProfile { Name = "星", Instruction = "落ち着いた口調", SpeakerId = 22 });
                var secondSource = Path.Combine(dir, "second.vrm"); File.WriteAllText(secondSource, "appearance-v2");
                var updated = library.Register(secondSource, first.id);
                Assert.AreEqual(first.id, updated.id); Assert.AreNotEqual(first.file, updated.file);
                Assert.AreEqual("appearance-v1", File.ReadAllText(library.Resolve(first)));
                var restarted = new YuiAvatarLibraryStore(Path.Combine(dir, "library"));
                Assert.AreEqual(updated.file, restarted.Read()[0].file);
                Assert.AreEqual(2, restarted.Read()[0].appearances.Count);
                Assert.AreEqual("first", restarted.Read()[0].appearances[0].name);
                Assert.AreEqual("second", restarted.Read()[0].appearances[1].name);
                var changedBack = library.Register(library.Resolve(first), first.id);
                Assert.AreEqual(first.file, changedBack.file);
                Assert.AreEqual(2, changedBack.appearances.Count);
                library.Register(secondSource, first.id);
                var profile = new YuiCharacterProfileStore(Path.Combine(dir, "profiles")).Read(updated.id);
                Assert.AreEqual("星", profile.Name); Assert.AreEqual(22, profile.SpeakerId);
                // Reimporting the original as a NEW character must not overwrite the updated character.
                var another = library.Register(source); Assert.AreNotEqual(first.id, another.id);
                Assert.AreEqual(2, library.Read().Count);
                library.RestoreAppearance(first, updated.file);
                Assert.AreEqual(first.file, library.Read().Find(e => e.id == first.id).file);
                var sameAppearance = library.Register(source);
                Assert.AreNotEqual(another.id, sameAppearance.id);
                Assert.AreEqual(another.file, sameAppearance.file);
            } finally { Directory.Delete(dir, true); }
        }
        [Test] public void LegacyWardrobeMigratesAndCrossCharacterClothingKeepsBothIdentities()
        {
            var dir = Path.Combine(Path.GetTempPath(), "yui-wardrobe-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try {
                File.WriteAllText(Path.Combine(dir, "old.vrm"), "old");
                File.WriteAllText(Path.Combine(dir, "index.json"), "{\"entries\":[{\"id\":\"first-person\",\"name\":\"original\",\"file\":\"old.vrm\"}]}");
                var store = new YuiAvatarLibraryStore(dir);
                Assert.AreEqual("original", store.Read()[0].appearances[0].name);
                var source = Path.Combine(dir, "coat.vrm"); File.WriteAllText(source, "coat");
                var second = store.Register(source);
                var changed = store.Register(store.Resolve(second), "first-person");
                Assert.AreEqual("original", changed.name);
                Assert.AreEqual("coat", changed.appearances[1].name);
                Assert.AreEqual(second.file, store.Read().Find(e => e.id == second.id).file);
                Assert.AreEqual(2, store.Read().Count);
                File.WriteAllText(Path.Combine(dir, "index.json"), "{\"entries\":[{\"id\":\"x\",\"file\":\"a.vrm\",\"appearances\":[{\"file\":\"../escape.vrm\"}]}]}");
                Assert.Throws<InvalidDataException>(() => store.Read());
            } finally { Directory.Delete(dir, true); }
        }
        [Test] public void CorruptLibraryIsPreservedAndCancelledCopyDoesNotCommit()
        {
            var dir = Path.Combine(Path.GetTempPath(), "yui-library-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try {
                var source = Path.Combine(dir, "a.vrm"); File.WriteAllText(source, "test");
                var store = new YuiAvatarLibraryStore(dir);
                using var token = new System.Threading.CancellationTokenSource(); token.Cancel();
                Assert.Throws<OperationCanceledException>(() => store.Register(source, null, token.Token));
                Assert.IsFalse(File.Exists(Path.Combine(dir, "index.json")));
                Assert.IsEmpty(Directory.GetFiles(dir, ".import-*"));
                File.WriteAllText(Path.Combine(dir, "index.json"), "{broken");
                Assert.Catch(() => store.Register(source));
                Assert.AreEqual("{broken", File.ReadAllText(Path.Combine(dir, "index.json")));
                File.WriteAllText(Path.Combine(dir, "index.json"), "{}");
                Assert.Throws<InvalidDataException>(() => store.Register(source));
                Assert.AreEqual("{}", File.ReadAllText(Path.Combine(dir, "index.json")));
            } finally { Directory.Delete(dir, true); }
        }
        [Test] public void RuntimePackageRejectsUnexpectedComponentsBeforeInstantiation()
        {
            var prefab = new GameObject("unexpected");
            try { Assert.DoesNotThrow(() => YuiAvatarPackageLoader.ValidateRuntimePrefab(prefab)); prefab.AddComponent<AudioSource>(); Assert.DoesNotThrow(() => YuiAvatarPackageLoader.ValidateRuntimePrefab(prefab)); prefab.AddComponent<Camera>(); Assert.Throws<InvalidDataException>(() => YuiAvatarPackageLoader.ValidateRuntimePrefab(prefab)); }
            finally { UnityEngine.Object.DestroyImmediate(prefab); }
        }
        [Test] public void NativeBlinkDoesNotChangeMouthBlendshape()
        {
            var root = new GameObject("avatar"); var mesh = new Mesh(); mesh.vertices = new [] { Vector3.zero }; mesh.SetIndices(new [] { 0 }, MeshTopology.Points, 0);
            mesh.AddBlendShapeFrame("Blink", 100, new [] { Vector3.zero }, new [] { Vector3.zero }, new [] { Vector3.zero });
            mesh.AddBlendShapeFrame("vrc.v_aa", 100, new [] { Vector3.zero }, new [] { Vector3.zero }, new [] { Vector3.zero });
            try { var renderer = root.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh; renderer.SetBlendShapeWeight(1, 45); var driver = root.AddComponent<YuiAvatarExpressionDriver>(); driver.SetBlink(.8f); Assert.AreEqual(80, renderer.GetBlendShapeWeight(0)); Assert.AreEqual(45, renderer.GetBlendShapeWeight(1)); }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(mesh); }
        }
        [Test] public void MissingPhonePayloadExplainsHowToRecover()
        {
            var manifest = JObject.Parse("{\"payloads\":[{\"platform\":\"windows\"}]}");
            var error = Assert.Throws<InvalidDataException>(() => YuiAvatarPackageLoader.SelectPayload(manifest, "ios"));
            StringAssert.Contains("ios", error.Message); StringAssert.Contains("windows", error.Message); StringAssert.Contains("再書出し", error.Message);
        }
        [Test] public void SpringMotionRespondsToRotationWithoutChangingBoneLengthsAndResetsAfterResume()
        {
            var root = new GameObject("avatar"); var hair = new GameObject("hair"); var tip = new GameObject("tip");
            hair.transform.SetParent(root.transform, false); tip.transform.SetParent(hair.transform, false); tip.transform.localPosition = Vector3.down * .2f;
            try {
                var motion = root.AddComponent<YuiAvatarSpringMotion>(); motion.AddChain(hair.transform, .2f, .3f); Assert.AreEqual(1, motion.JointCount);
                root.transform.rotation = Quaternion.Euler(0,0,30); motion.Step(1f/60f);
                Assert.Greater(Quaternion.Angle(Quaternion.identity, hair.transform.localRotation), .01f);
                for (var i=0;i<600;i++) { root.transform.rotation = Quaternion.Euler(0,0,Mathf.Sin(i*.1f)*30); motion.Step(1f/60f); }
                Assert.That(Vector3.Distance(hair.transform.position, tip.transform.position), Is.EqualTo(.2f).Within(.0001f));
                Assert.LessOrEqual(Quaternion.Angle(Quaternion.identity, hair.transform.localRotation), 45.01f);
                motion.Step(1f); Assert.That(Quaternion.Angle(Quaternion.identity, hair.transform.localRotation), Is.LessThan(.01f));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void SpringMotionHonorsExcludedSubtreesAndDeduplicatesChains()
        {
            var root = new GameObject("root");var child = new GameObject("child");var tip = new GameObject("tip");child.transform.SetParent(root.transform);tip.transform.SetParent(child.transform);child.transform.localPosition=Vector3.down;tip.transform.localPosition=Vector3.down;
            try { var motion=root.AddComponent<YuiAvatarSpringMotion>();motion.AddChain(root.transform,.3f,0,new System.Collections.Generic.HashSet<Transform>{child.transform});Assert.AreEqual(0,motion.JointCount);motion.AddChain(root.transform,.3f,0);motion.AddChain(root.transform,.3f,0);Assert.AreEqual(2,motion.JointCount); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
