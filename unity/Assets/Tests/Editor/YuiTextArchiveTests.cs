using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;
using NUnit.Framework;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests
{
    public sealed class YuiTextArchiveTests
    {
        private string directory, path;
        [SetUp] public void Setup() { directory=Path.Combine(Path.GetTempPath(),"yui-history-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);path=Path.Combine(directory,"history.jsonl"); }
        [TearDown] public void Cleanup() { Directory.Delete(directory,true); }
        private static YuiTextArchive.Entry Record(int i,string text=null,string mode="talk") => new YuiTextArchive.Entry {Id="id-"+i,Text=text??"message "+i,Mode=mode};

        [TestCase(true)] [TestCase(false)] public void PrivacyHeadingPreservesConnectivityOnSecondLine(bool hasKey)
        {
            var go=new GameObject("status-test",typeof(RectTransform));go.SetActive(false);
            try
            {
                var panel=go.AddComponent<YuiChatPanel>();
                var label=new GameObject("Status",typeof(RectTransform),typeof(Text));label.transform.SetParent(go.transform,false);
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(YuiChatPanel).GetField("statusText",flags).SetValue(panel,label.GetComponent<Text>());
                typeof(YuiChatPanel).GetField("secretMode",flags).SetValue(panel,true);
                typeof(YuiChatPanel).GetField("conversationMode",flags).SetValue(panel,YuiConversationModes.DirectOpenAi);
                typeof(YuiChatPanel).GetField("openAiApiKey",flags).SetValue(panel,hasKey ? "synthetic-test" : "");
                typeof(YuiChatPanel).GetField("currentStatus",flags).SetValue(panel,"Connected");
                typeof(YuiChatPanel).GetMethod("RenderStatus",flags).Invoke(panel,null);
                var lines=label.GetComponent<Text>().text.Split('\n');
                Assert.AreEqual(2,lines.Length);StringAssert.Contains("Secret Mode",lines[0]);
                StringAssert.Contains(hasKey ? "Connected" : YuiUiLocalization.Text("API key required"),lines[1]);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test] public void LegacyConsoleBufferStaysBoundedWhileArchiveRetainsEverything()
        {
            var go=new GameObject("log-test");go.SetActive(false);
            try
            {
                var view=go.AddComponent<YuiChatLogView>();
                for(var i=0;i<300;i++) view.AppendLog("You","entry-"+i);
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var messages=(System.Collections.ICollection)typeof(YuiChatLogView).GetField("messages",flags).GetValue(view);
                Assert.AreEqual(100,messages.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test] public void ChildDialogsAppearAboveSettingsWithoutASeparateWindow()
        {
            var canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));
            var modal=new GameObject("Modal",typeof(RectTransform));modal.transform.SetParent(canvas.transform,false);
            try
            {
                typeof(YuiChatPanel).GetMethod("ForegroundModal",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{modal});
                Assert.IsTrue(modal.GetComponent<Canvas>().overrideSorting);
                Assert.Greater(modal.GetComponent<Canvas>().sortingOrder,5010);
                Assert.IsNotNull(modal.GetComponent<GraphicRaycaster>());
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }
        [Test] public void ReaderChunksPreserveLargeResponsesAndUnicode()
        {
            var original=string.Concat(Enumerable.Repeat("日本語 🌸\n"+new string('x',4090),15));
            var chunks=YuiReadOnlyTextView.Split(original).ToArray();
            Assert.AreEqual(original,string.Concat(chunks));Assert.Greater(chunks.Length,10);
            foreach(var chunk in chunks) { Assert.LessOrEqual(chunk.Length,4096);Assert.IsFalse(char.IsHighSurrogate(chunk[chunk.Length-1])); }
        }
        [Test] public void FullTextSurvivesRestartAndMoreThanTheConsoleWindow()
        {
            var archive=new YuiTextArchive(path);
            var full=string.Concat(Enumerable.Repeat("日本語の長い回答 🌸\nhttps://example.com/a?x=1&b=2\n",400));
            for(var i=0;i<250;i++) archive.Append(Record(i,i==0?full:null));
            var reopened=new YuiTextArchive(path);
            var ids=Enumerable.Range(0,25).SelectMany(i=>reopened.ReadPage(i*10,10).Items).ToArray();
            Assert.AreEqual(250,ids.Length);Assert.AreEqual(250,ids.Select(x=>x.Id).Distinct().Count());
            Assert.AreEqual(full,ids.Last().Text);
            Assert.IsTrue(reopened.ReadPage(0,10).HasOlder);Assert.IsFalse(reopened.ReadPage(240,10).HasOlder);
        }
        [Test] public void SnapshotKeepsPaginationStableWhenNewMessagesArrive()
        {
            var archive=new YuiTextArchive(path);for(var i=0;i<12;i++)archive.Append(Record(i));
            var snapshot=archive.Length;
            CollectionAssert.AreEqual(new[]{"id-11","id-10","id-9"},archive.ReadPage(0,3,null,snapshot).Items.Select(x=>x.Id));
            archive.Append(Record(12));archive.Append(Record(13));
            CollectionAssert.AreEqual(new[]{"id-8","id-7","id-6"},archive.ReadPage(3,3,null,snapshot).Items.Select(x=>x.Id));
            Assert.AreEqual("id-13",archive.ReadPage(0,1).Items.Single().Id);
        }
        [Test] public void InterruptedAppendDoesNotHideValidRecordsOrDestroyOriginalBytes()
        {
            var archive=new YuiTextArchive(path);archive.Append(Record(1));File.AppendAllText(path,"{\"Id\":\"broken");archive.Append(Record(2));
            var page=archive.ReadPage(0,10);Assert.AreEqual(1,page.DamagedLines);
            CollectionAssert.AreEqual(new[]{"id-2","id-1"},page.Items.Select(x=>x.Id));
            StringAssert.Contains("{\"Id\":\"broken\n",File.ReadAllText(path));
        }
        [Test] public void ExplicitDeletionRemovesTextAndPreservesOtherModesAndMalformedRecords()
        {
            var archive=new YuiTextArchive(path);archive.Append(Record(1,"delete this unique text"));archive.Append(Record(2,"keep","work"));File.AppendAllText(path,"broken\n");
            archive.Remove("id-1");
            StringAssert.DoesNotContain("delete this unique text",File.ReadAllText(path));StringAssert.Contains("broken",File.ReadAllText(path));
            Assert.AreEqual("keep",new YuiTextArchive(path).ReadPage(0,20,"work").Items.Single().Text);
            Assert.IsEmpty(archive.ReadPage(0,20,"talk").Items);
        }
        [Test] public void LegacySavedAnswersMigrateOnceAndNewAnswersShareOneCollection()
        {
            File.WriteAllText(Path.Combine(directory,"old.md"),"old answer 日本語");File.WriteAllText(Path.Combine(directory,"old.md.json"),"{\"source\":1}");
            var store=new YuiSavedResultStore(directory);var id=store.Save("new answer","metadata");
            Assert.AreEqual(2,store.Page(0,10).Items.Count);Assert.AreEqual(2,new YuiSavedResultStore(directory).Page(0,10).Items.Count);
            Assert.AreEqual("metadata",store.Metadata(id));Assert.AreEqual("{\"source\":1}",store.Metadata("old.md"));
            Assert.AreEqual(1,Directory.GetFiles(directory,"*.md").Length);
            store.Remove("old.md");Assert.IsFalse(File.Exists(Path.Combine(directory,"old.md")));Assert.IsFalse(File.Exists(Path.Combine(directory,"old.md.json")));
            Assert.AreEqual("new answer",new YuiSavedResultStore(directory).Page(0,10).Items.Single().Text);
        }
        [Test] public void DuplicateMigrationIdentifiersAreReadOnlyOnce()
        {
            var archive=new YuiTextArchive(path);archive.Append(Record(1));archive.Append(Record(1));archive.Append(Record(2));
            CollectionAssert.AreEqual(new[]{"id-2","id-1"},archive.ReadPage(0,10).Items.Select(x=>x.Id));
            archive.Remove("id-1");Assert.AreEqual(1,archive.ReadPage(0,10).Items.Count);
        }
    }
}
