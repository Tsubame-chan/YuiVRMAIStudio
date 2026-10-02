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
            store.Remember("a","私は猫が好き",false);Assert.AreEqual(1,store.Read("a").Count);
        }
        [Test] public void LaterCorrectionsArePresentedAfterEarlierStatements()
        {
            var store=new YuiCharacterMemoryStore(directory);store.Save("a","私は鶏肉カレーが好き");store.Save("a","今は豆カレーが好き");
            var context=store.Context("a","好きなカレー");Assert.Less(context.IndexOf("鶏肉"),context.IndexOf("豆"));
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
    }
}
