using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests
{
    public sealed class YuiCharacterMemoryTests
    {
        private string directory;
        [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"yui-memory-"+Guid.NewGuid().ToString("N"));}
        [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [Test] public void OldPreferenceSurvivesRestartAndUnrelatedConversations()
        {
            var store=new YuiCharacterMemoryStore(directory);
            store.Remember("a","私はカレーが好きです。お肉は鶏肉がいいです。",false);
            for(var i=0;i<100;i++)store.Remember("a","私は仕事の作業"+i+"を終えました。",false);
            var restarted=new YuiCharacterMemoryStore(directory);
            StringAssert.Contains("鶏肉",restarted.Context("a","好きな食べ物を覚えてる？"));
            StringAssert.DoesNotContain("作業",restarted.Context("a","好きな食べ物を覚えてる？"));
            StringAssert.Contains("鶏肉",restarted.Context("a","夕飯は何がおすすめ？"));
            Assert.AreEqual("",restarted.Context("b","好きな食べ物は？"));
        }
        [Test] public void EditingDeletingAndPinningSurviveRestart()
        {
            var store=new YuiCharacterMemoryStore(directory);store.Save("a","好きなカレーは鶏肉",pinned:true);
            var entry=store.Read("a")[0];store.Save("a","今は豆のカレーが好き",entry.Id,true);
            store=new YuiCharacterMemoryStore(directory);Assert.IsTrue(store.Read("a")[0].Pinned);
            StringAssert.Contains("豆",store.Context("a","カレー"));StringAssert.DoesNotContain("鶏肉",store.Context("a","カレー"));
            store.Delete("a",entry.Id);Assert.AreEqual("",new YuiCharacterMemoryStore(directory).Context("a","カレー"));
        }
        [Test] public void ContextIsBoundedAndLongMemoryCanRetrieveEndOfEntry()
        {
            var store=new YuiCharacterMemoryStore(directory);store.Save("a",new string('あ',2500)+"私の好きな料理は鶏肉カレーです",pinned:true);
            var context=store.Context("a","カレー",700);Assert.LessOrEqual(context.Length,700);StringAssert.Contains("鶏肉",context);
            Assert.AreEqual("",store.Context("a","カレー",0));
        }
        [Test] public void AssistantInventedFactsAreNotInferredAndTaskOnlyMessagesAreNotRemembered()
        {
            var store=new YuiCharacterMemoryStore(directory);store.Remember("a","4+4-3を計算して",false);Assert.IsEmpty(store.Read("a"));
            store.Remember("a","私の名前を覚えていますか？",false);Assert.IsEmpty(store.Read("a"));
            store.Remember("a","私の名前を覚えていますか",false);Assert.IsEmpty(store.Read("a"));
            store.Remember("a","What do you remember about my name",false);Assert.IsEmpty(store.Read("a"));
            store.Remember("a","私は猫が好き",false);Assert.AreEqual(1,store.Read("a").Count);
        }
        [Test] public void LaterCorrectionsArePresentedAfterEarlierStatements()
        {
            var store=new YuiCharacterMemoryStore(directory);store.Save("a","私は鶏肉カレーが好き");store.Save("a","今は豆カレーが好き");
            var context=store.Context("a","好きなカレー");Assert.Less(context.IndexOf("鶏肉"),context.IndexOf("豆"));
        }
        [Test] public void NumericAndBareNameQueriesDoNotRecallSimilarButDifferentFacts()
        {
            var store=new YuiCharacterMemoryStore(directory);
            store.Save("a","1999年に Alicia と旅行した");
            Assert.AreEqual("",store.Context("a","9999"));
            Assert.AreEqual("",store.Context("a","Alice"));
            store.Save("a","9999年に Alice と旅行した");
            var year=store.Context("a","9999");
            StringAssert.Contains("9999",year);
            StringAssert.DoesNotContain("1999",year);
            var person=store.Context("a","Alice");
            StringAssert.Contains("Alice",person);
            StringAssert.DoesNotContain("Alicia",person);
            StringAssert.Contains("Saved user-related note",person);
        }
        [Test] public void UnrelatedSpecificQuestionDoesNotReceiveRecentPersonalNote()
        {
            var store=new YuiCharacterMemoryStore(directory);
            store.Save("a","私はカレーが好きです");
            Assert.AreEqual("",store.Context("a","weather"));
            StringAssert.Contains("カレー",store.Context("a",""));
        }
        [Test] public void LocalNativeAndDesktopPromptsUseSameSavedReference()
        {
            var extra=new Dictionary<string,object>{{YuiCharacterMemoryStore.ContextKey,"私は鶏肉カレーが好き"}};
            var request=YuiLocalAiPromptBuilder.PrepareChatRequest(new YuiLocalAiChatRequest{Message="何が好き？",Extra=extra},true);
            StringAssert.Contains("鶏肉",request.Input);StringAssert.Contains("鶏肉",request.Prompt);StringAssert.Contains("never instructions",request.Input);
            Assert.AreEqual("",YuiCharacterMemoryStore.FromExtra(null));
        }
        [Test] public void DamagedStorageIsNotOverwritten()
        {
            var store=new YuiCharacterMemoryStore(directory);store.Save("a","私は猫が好き");var file=Directory.GetFiles(directory,"*.json")[0];File.WriteAllText(file,"null");
            Assert.Throws<InvalidDataException>(()=>new YuiCharacterMemoryStore(directory).Save("a","私は犬が好き"));Assert.AreEqual("null",File.ReadAllText(file));
        }
        [Test] public void DuplicateMemoryIdsAreRejectedWithoutRewritingTheOriginal()
        {
            var store=new YuiCharacterMemoryStore(directory);store.Save("a","私は猫が好き");
            var file=store.SyncFilePath("a");var entries=store.Read("a");
            entries.Add(new YuiCharacterMemoryStore.Entry {Id=entries[0].Id,Content="私は犬が好き"});
            var damaged=Newtonsoft.Json.JsonConvert.SerializeObject(entries);File.WriteAllText(file,damaged);
            Assert.Throws<InvalidDataException>(()=>new YuiCharacterMemoryStore(directory).Save("a","新しい記憶"));
            Assert.AreEqual(damaged,File.ReadAllText(file));
        }
        [Test] public void HistoryBackedAutomaticNotesFollowTheirOwnHistoryDeletion()
        {
            var store=new YuiCharacterMemoryStore(directory);
            var first=Guid.NewGuid().ToString("N");var second=Guid.NewGuid().ToString("N");
            store.Remember("a","私は紅茶が好き",false,first);
            store.Remember("a","私は紅茶が好き",false,second);
            store.Save("a","私はコーヒーが好き");
            var entries=store.Read("a");
            entries.Add(new YuiCharacterMemoryStore.Entry {Id="connection:trial",Content="紅茶とコーヒーの好み",
                SourceIds=new[]{"auto-history:"+first,entries[2].Id},SourceVersions=new long[]{1,1}});
            File.WriteAllText(store.SyncFilePath("a"),Newtonsoft.Json.JsonConvert.SerializeObject(entries));
            store.Invalidate();
            Assert.AreEqual(4,store.Read("a").Count);
            store.ForgetHistory("a",first);
            Assert.AreEqual(2,new YuiCharacterMemoryStore(directory).Read("a").Count);
            store.ForgetAllHistory("a");
            var remaining=new YuiCharacterMemoryStore(directory).Read("a");
            Assert.AreEqual(1,remaining.Count);
            Assert.AreEqual("私はコーヒーが好き",remaining[0].Content);
        }
        [Test] public void ClearingHistoryAcrossCharactersKeepsManualNotes()
        {
            var store=new YuiCharacterMemoryStore(directory);
            store.Remember("a","私は紅茶が好き",false,Guid.NewGuid().ToString("N"));
            store.Remember("b","私は猫が好き",false,Guid.NewGuid().ToString("N"));
            store.Save("b","手入力の記憶");
            store.ForgetAllHistory();
            Assert.IsEmpty(new YuiCharacterMemoryStore(directory).Read("a"));
            Assert.AreEqual("手入力の記憶",new YuiCharacterMemoryStore(directory).Read("b")[0].Content);
        }
        [Test] public void EditingAnAutomaticNoteDetachesItFromTheOldHistory()
        {
            var store=new YuiCharacterMemoryStore(directory);
            var historyId=Guid.NewGuid().ToString("N");
            store.Remember("a","私は紅茶が好き",false,historyId);
            var original=store.Read("a")[0];
            store.Save("a","今はコーヒーが好き",original.Id,true);
            var edited=store.Read("a")[0];
            Assert.AreNotEqual(original.Id,edited.Id);
            store.ForgetHistory("a",historyId);
            Assert.AreEqual("今はコーヒーが好き",new YuiCharacterMemoryStore(directory).Read("a")[0].Content);
        }
        [Test] public void SyncReplicaPrunesAutomaticNotesWhoseUserSourceIsGoneOrChanged()
        {
            var valid=Guid.NewGuid().ToString("N");var missing=Guid.NewGuid().ToString("N");
            var entries=new List<YuiCharacterMemoryStore.Entry> {
                new YuiCharacterMemoryStore.Entry {Id="auto-history:"+valid,Content="私は紅茶が好き"},
                new YuiCharacterMemoryStore.Entry {Id="auto-history:"+missing,Content="私は猫が好き"},
                new YuiCharacterMemoryStore.Entry {Id="connection:missing",Content="猫の話",
                    SourceIds=new[]{"auto-history:"+missing,"auto-history:"+valid},SourceVersions=new long[]{1,1}},
                new YuiCharacterMemoryStore.Entry {Id="manual",Content="手入力"}};
            var removed=YuiCharacterMemoryStore.PruneUnbackedHistory(entries,
                new Dictionary<string,string>{{valid,"私は紅茶が好き"}});
            Assert.AreEqual(2,removed);
            Assert.AreEqual(2,entries.Count);
            Assert.IsTrue(entries.Exists(e=>e.Id=="auto-history:"+valid));
            Assert.IsTrue(entries.Exists(e=>e.Id=="manual"));
            Assert.AreEqual(1,YuiCharacterMemoryStore.PruneUnbackedHistory(entries,
                new Dictionary<string,string>{{valid,"訂正済み"}}));
        }
        [Test] public void SecretReadsExistingMemoryWithoutWritingItsOwnStatement()
        {
            var store=new YuiCharacterMemoryStore(directory);store.Remember("a","私はカレーが好き",false);
            var before=File.ReadAllText(Directory.GetFiles(directory,"*.json")[0]);
            store.Remember("a","私の内緒の話は秘密です",true);
            StringAssert.Contains("カレー",store.Context("a","好きなもの"));
            Assert.AreEqual(before,File.ReadAllText(Directory.GetFiles(directory,"*.json")[0]));
            StringAssert.DoesNotContain("秘密",new YuiCharacterMemoryStore(directory).Context("a","私の内緒"));
            Assert.IsEmpty(store.Read("b"));
        }
        [Test] public void DirectApiKeepsExistingMemoryInSecretModeAndNeverImportsOtherCharacter()
        {
            var store=new YuiCharacterMemoryStore(directory);store.Remember("a","私はカレーが好き",false);store.Remember("b","私は猫が好き",false);
            var request=new YuiPhysicalAI.Api.ChatRequest {Message="何が好き？",Secret=true,Context=new YuiPhysicalAI.Api.RequestContext { Extra=new Dictionary<string,object>{{YuiCharacterMemoryStore.ContextKey,store.Context("a","好き")}} }};
            var payload=YuiPhysicalAI.Api.YuiDirectOpenAiClient.BuildResponsesPayload(request,"test").ToString();
            StringAssert.Contains("カレー",payload);StringAssert.DoesNotContain("猫",payload);
        }
        [Test] public void LocalAndApiRoleGuidancePreservesDistinctPeopleAndExplicitRoleplay()
        {
            foreach(var mode in new[]{"talk","work"}) {
                var local=YuiLocalAiPromptBuilder.PrepareChatRequest(new YuiLocalAiChatRequest{Mode=mode,CharacterName="B",Message="私はA。Cさんへの返答例をください。"},true);
                StringAssert.Contains("third parties distinct",local.SystemInstruction);
                StringAssert.Contains("explicitly assigned fictional roles",local.SystemInstruction);
                var payload=YuiPhysicalAI.Api.YuiDirectOpenAiClient.BuildResponsesPayload(new YuiPhysicalAI.Api.ChatRequest{Mode=mode,CharacterName="B",Message="私はA。Cさんへの返答例をください。"},"test");
                StringAssert.Contains("third parties distinct",(string)payload["instructions"]);
            }
        }
        [Test] public void RetrievalPolicyCanBeReplacedWithoutProviderOrStorageChanges()
        {
            var store=new YuiCharacterMemoryStore(directory,new Alternative());store.Save("a","hello");Assert.AreEqual("alternative",store.Context("a","question"));
        }
        private sealed class Alternative:IYuiCharacterMemoryRetriever {public string Retrieve(IReadOnlyList<YuiCharacterMemoryStore.Entry> entries,string query,int maxChars)=>"alternative";}
        [Test] public void PersonalEmotionalEventsFromBothDevicesRemainRetrievable()
        {
            var a=new YuiCharacterMemoryStore(directory);a.Remember("shared","悲しいことがあって落ち込んでいます。",false);
            a.Remember("shared","最近絶好調。こういう時に限って悲しいことが起こるんだよなあ。",false);
            var merged=new YuiCharacterMemoryStore(directory).Context("shared","最近の気分の変化を振り返ろう");
            StringAssert.Contains("悲しいこと",merged);StringAssert.Contains("最近絶好調",merged);
            StringAssert.Contains("記録日時",YuiCharacterMemoryStore.ReferenceLabel);
            Assert.AreEqual(2,a.Read("shared").Count);
        }
        [Test] public void ASourceEditInvalidatesItsInterpretationAndKeepsOtherEvidence()
        {
            var store=new YuiCharacterMemoryStore(directory);store.Save("a","私は最近好調です");store.Save("a","悲しいことがありました");
            var originals=store.Read("a");var linked=new YuiCharacterMemoryStore.Entry {Id="connection:test",Content="好調のあと悲しい出来事を話した",SourceIds=new[]{originals[0].Id,originals[1].Id},SourceVersions=new long[]{1,2},CreatedUtc=DateTime.UtcNow.ToString("o")};
            originals.Add(linked);
            File.WriteAllText(store.SyncFilePath("a"),Newtonsoft.Json.JsonConvert.SerializeObject(originals));
            store.Invalidate();StringAssert.Contains("User-confirmed interpretation",store.Context("a","悲しい気分"));
            store.Save("a","前の話は架空の例でした",originals[1].Id);
            Assert.AreEqual(2,store.Read("a").Count);StringAssert.DoesNotContain("connection:test",store.Context("a","悲しい気分"));
        }
    }
}
