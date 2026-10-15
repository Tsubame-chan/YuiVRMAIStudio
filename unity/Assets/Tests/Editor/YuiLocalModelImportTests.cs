using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using YuiPhysicalAI.LocalAI;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.Tests.Editor
{
    public class YuiLocalModelImportTests
    {
        [TestCase("<|channel>analysis\nprivate thought<|channel>final\nHello", "Hello")]
        [TestCase("<|channel>analysis\nunfinished thought", "")]
        [TestCase("<think>private thought</think>Hello", "Hello")]
        public void GgufRepliesKeepOnlyFinalText(string raw,string expected) => Assert.That(YuiLlamaRuntime.StripReasoning(raw),Is.EqualTo(expected));
        [Test] public void ProbeRejectsGenericOutputAndThoughtOnlyEcho() {
            var code=YuiLocalModelProbe.NewCode();Assert.That(YuiLocalModelProbe.Matches("你好！",code),Is.False);
            Assert.That(YuiLocalModelProbe.Matches("<think>"+code+"</think>Unrelated reply",code),Is.False);
            Assert.That(YuiLocalModelProbe.Matches("{\"Text\":\""+code+"\"}",code),Is.True);
            Assert.That(YuiLocalModelProbe.NewCode(),Is.Not.EqualTo(code));
        }
        string root,source;
        [SetUp] public void Setup(){root=Path.Combine(Path.GetTempPath(),"yui-model-import-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);source=Path.Combine(root,"sample.gguf");var bytes=new byte[65536];System.Text.Encoding.ASCII.GetBytes("GGUF").CopyTo(bytes,0);bytes[4]=3;File.WriteAllBytes(source,bytes);}
        [TearDown] public void Cleanup(){Directory.Delete(root,true);}
        YuiLocalModelImporter Store => new YuiLocalModelImporter(root);
        static async Task Expect<T>(Func<Task> action) where T:Exception {try{await action();Assert.Fail("Expected "+typeof(T).Name);}catch(T){}}
        string Catalog => Path.Combine(root,"YuiLocalAI/additional-models.json");
        [Test] public async Task NullCatalogEntryIsRejectedWithoutOverwritingTheCatalog(){Directory.CreateDirectory(Path.GetDirectoryName(Catalog));var damaged="{\"packs\":[null]}";File.WriteAllText(Catalog,damaged);await Expect<InvalidDataException>(async()=>await Import());Assert.AreEqual(damaged,File.ReadAllText(Catalog));Assert.IsTrue(File.Exists(source));}
        [Test] public async Task OwnedPickerCopyCanCrossVolumesWithoutLeavingItsTemporarySource(){var pack=await Store.ImportAsync(source,true,"Cross-volume",false,Array.Empty<YuiLocalAiModelPack>(),(p,s,t)=>Task.FromResult(true),null,CancellationToken.None,_=>long.MaxValue,(s,d)=>throw new IOException("Simulated EXDEV"));Assert.IsFalse(File.Exists(source));Assert.IsTrue(File.Exists(Path.Combine(root,"YuiLocalAI/Models",pack.RuntimeModelRef)));}
        [Test] public async Task CrossVolumeCopyChecksItsExtraSpaceBeforeProbe(){bool probe=false;await Expect<IOException>(async()=>await Store.ImportAsync(source,true,"Cross-volume",false,Array.Empty<YuiLocalAiModelPack>(),(p,s,t)=>{probe=true;return Task.FromResult(true);},null,CancellationToken.None,_=>YuiLocalModelImporter.ReserveBytes+1,(s,d)=>throw new IOException("Simulated EXDEV")));Assert.IsFalse(probe);Assert.IsFalse(File.Exists(Catalog));Assert.IsFalse(File.Exists(source));}
        [Test] public async Task SuccessfulProbeDoesNotLeaveCompilationForItsTemporaryPath(){string cache=null;var pack=await Import((p,s,t)=>{cache=Path.Combine(root,"YuiLocalAI/RuntimeCache",p.RuntimeModelRef);Directory.CreateDirectory(cache);File.WriteAllText(Path.Combine(cache,"temporary-path.bin"),"compiled");return Task.FromResult(true);});Assert.IsFalse(Directory.Exists(cache));Assert.IsTrue(File.Exists(Path.Combine(root,"YuiLocalAI/Models",pack.RuntimeModelRef)));Assert.IsTrue(File.Exists(Catalog));}
        [Test] public async Task FailedProbeRemovesOnlyItsOwnedCompilationCache(){string cache=null;var standard=Path.Combine(root,"YuiLocalAI/RuntimeCache/gemma-4-E2B-it.litertlm");Directory.CreateDirectory(standard);File.WriteAllText(Path.Combine(standard,"keep.bin"),"keep");await Expect<InvalidDataException>(async()=>await Import((p,s,t)=>{cache=Path.Combine(root,"YuiLocalAI/RuntimeCache",p.RuntimeModelRef);Directory.CreateDirectory(cache);File.WriteAllText(Path.Combine(cache,"compiled.bin"),"temporary");return Task.FromResult(false);}));Assert.IsFalse(Directory.Exists(cache));Assert.IsTrue(File.Exists(Path.Combine(standard,"keep.bin")));Assert.IsFalse(File.Exists(Path.Combine(root,"YuiLocalAI/pending-import.json")));}
        [Test] public void RecoveryRemovesAnInterruptedProbesCache(){var name="imported_"+new string('b',64)+".litertlm";var cache=Path.Combine(root,"YuiLocalAI/RuntimeCache",name);Directory.CreateDirectory(cache);File.WriteAllText(Path.Combine(cache,"compiled.bin"),"temporary");File.WriteAllText(Path.Combine(root,"YuiLocalAI/pending-import.json"),name);Store.Recover();Assert.IsFalse(Directory.Exists(cache));Assert.IsTrue(File.Exists(source));}
        Task<YuiLocalAiModelPack> Import(Func<YuiLocalAiModelPack,string,CancellationToken,Task<bool>> probe=null,CancellationToken token=default,Func<string,long> space=null) => Store.ImportAsync(source,false,"Other model",false,Array.Empty<YuiLocalAiModelPack>(),probe??((p,s,t)=>Task.FromResult(true)),null,token,space??(_=>long.MaxValue));
        [Test] public async Task ProbePrecedesPublicationAndOriginalFileSurvives(){var p=await Import((p,path,t)=>{Assert.IsFalse(File.Exists(Catalog));Assert.IsTrue(File.Exists(path));Assert.That(p.Capabilities,Is.EqualTo(new[]{YuiLocalAiCapability.Chat}));return Task.FromResult(true);});Assert.IsTrue(File.Exists(source));Assert.IsTrue(File.Exists(Path.Combine(root,"YuiLocalAI/Models",p.RuntimeModelRef)));Assert.AreEqual(p.Id,JsonConvert.DeserializeObject<YuiLocalAiModelPackManifest>(File.ReadAllText(Catalog)).Packs.Single().Id);Assert.That(YuiLocalModelCatalog.TryValidate(p,Array.Empty<YuiLocalAiModelPack>(),out _),Is.True);}
        [Test] public async Task FailedProbeNeverRegistersOrLeavesStage(){await Expect<InvalidDataException>(async()=>await Import((p,s,t)=>Task.FromResult(false)));Assert.IsFalse(File.Exists(Catalog));Assert.IsEmpty(Directory.GetFiles(Path.Combine(root,"YuiLocalAI/ImportStaging")));Assert.IsTrue(File.Exists(source));}
        [Test] public async Task DuplicateImportKeepsCatalogAndModel(){var p=await Import();var before=File.ReadAllText(Catalog);await Expect<InvalidOperationException>(async()=>await Import());Assert.AreEqual(before,File.ReadAllText(Catalog));Assert.IsTrue(File.Exists(Path.Combine(root,"YuiLocalAI/Models",p.RuntimeModelRef)));}
        [Test] public async Task CancellationAfterProbeRollsBack(){using var c=new CancellationTokenSource();await Expect<OperationCanceledException>(async()=>await Import((p,s,t)=>{c.Cancel();return Task.FromResult(true);},c.Token));Assert.IsFalse(File.Exists(Catalog));Assert.IsEmpty(Directory.GetFiles(Path.Combine(root,"YuiLocalAI/Models")));}
        [Test] public async Task CapacityFailureAndDamagedCatalogKeepSources(){await Expect<IOException>(async()=>await Import(space:_=>1));Directory.CreateDirectory(Path.GetDirectoryName(Catalog));File.WriteAllText(Catalog,"{broken");await Expect<JsonReaderException>(async()=>await Import());Assert.AreEqual("{broken",File.ReadAllText(Catalog));Assert.IsTrue(File.Exists(source));}
        [Test] public async Task OnlyImportedCopiesAndTheirCachesCanBeRemoved(){var p=await Import();Store.Delete(p.Id);Assert.IsEmpty(JsonConvert.DeserializeObject<YuiLocalAiModelPackManifest>(File.ReadAllText(Catalog)).Packs);Assert.IsTrue(File.Exists(source));Assert.Throws<InvalidOperationException>(()=>Store.Delete(YuiLocalModelSelection.StandardId));}
        [Test] public void RecoveryDeletesOnlyTheJournalledUnregisteredOwnedCopy(){var models=Path.Combine(root,"YuiLocalAI/Models");Directory.CreateDirectory(models);var name="imported_"+new string('a',64)+".gguf";File.WriteAllText(Path.Combine(models,name),"orphan");File.WriteAllText(Path.Combine(models,"gemma-4-E2B-it.litertlm"),"keep");File.WriteAllText(Path.Combine(root,"YuiLocalAI/pending-import.json"),name);Store.Recover();Assert.IsFalse(File.Exists(Path.Combine(models,name)));Assert.IsTrue(File.Exists(Path.Combine(models,"gemma-4-E2B-it.litertlm")));}
        [Test] public void InvalidMagicAndSplitModelAreRejectedBeforeProbe(){File.WriteAllText(source,"GGUF is merely a name, not a model");Assert.Throws<InvalidDataException>(()=>YuiLocalModelImporter.DetectFormat(source));Assert.IsNull(YuiPickedFilePolicy.Validate(source,YuiFilePurpose.Model));}
        [Test] public void ThinkingOnlyOutputIsNotAnAssistantReply(){Assert.IsEmpty(YuiLlamaRuntime.StripReasoning("<think>work</think>"));Assert.AreEqual("hello",YuiLlamaRuntime.StripReasoning("<think>work</think> hello"));Assert.AreEqual("literal <think>tag</think>",YuiLlamaRuntime.StripReasoning("literal <think>tag</think>"));}
        [Test] public async Task ImportedModelFailureDoesNotRunAnotherRegisteredModel(){var packs=new[]{new YuiLocalAiModelPack{Id=YuiLocalModelSelection.SelectedId,SourceKind="local-file",Format="litert-lm",EnabledByDefault=true,Platforms=new[]{"all"},Capabilities=new[]{YuiLocalAiCapability.Chat},RuntimeModelRef="selected.litertlm"},new YuiLocalAiModelPack{Id="other",Format="litert-lm",EnabledByDefault=true,Platforms=new[]{"all"},Capabilities=new[]{YuiLocalAiCapability.Chat},RuntimeModelRef="other.litertlm"}};int calls=0;var runtime=new YuiGoogleAiEdgeLocalAiRuntime(new YuiLocalAiModelRegistry(packs),(p,t)=>Task.FromResult("/model/"+p.RuntimeModelRef),(r,t)=>{calls++;return new YuiGoogleAiEdgeBridgeResponse{Ok=false,ErrorCode="runtime_error"};},true);Assert.IsFalse((await runtime.ChatAsync(new YuiLocalAiChatRequest{Message="test"},CancellationToken.None)).Success);Assert.AreEqual(1,calls);}
    }
}
