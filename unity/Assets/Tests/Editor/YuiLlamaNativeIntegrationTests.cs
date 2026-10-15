using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using YuiPhysicalAI.LocalAI;
namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiLlamaNativeIntegrationTests
    {
        static string Model() {var path=Environment.GetEnvironmentVariable("YUI_TEST_GGUF");if(string.IsNullOrEmpty(path)||!File.Exists(path))Assert.Ignore("Set YUI_TEST_GGUF to an isolated instruction GGUF to run real native inference.");return path;}
        static YuiLocalAiModelPack Pack => new YuiLocalAiModelPack {Id="native_test",Format="gguf",ModelId="native_test",SourceKind="local-file",TalkOutputTokenBudget=256,WorkOutputTokenBudget=512};
        [Test] public async Task ManagedUtf8MessagesReachRealMetalInference(){var path=Model();var p=Pack;var result=await YuiLlamaRuntime.GenerateAsync(p,path,new YuiLocalAiChatRequest{SystemInstruction="Answer briefly. Your name is Yui.",Input="Say hello in English.",CharacterName="ユイ"},new YuiLocalModelOptions{ContextTokens=4096,OutputTokens=64,Temperature=0},CancellationToken.None);Assert.IsTrue(result.Success,result.ErrorMessage);Assert.IsNotEmpty(result.Text);Assert.IsFalse(YuiLocalModelExecutionGate.Busy);}
        [Test] public async Task NativeLoadingCancellationReleasesTheExecutionGate(){var path=Model();using var c=new CancellationTokenSource();c.CancelAfter(50);try{await YuiLlamaRuntime.GenerateAsync(Pack,path,new YuiLocalAiChatRequest{Input="Write a very long story."},new YuiLocalModelOptions{ContextTokens=4096,OutputTokens=512},c.Token);Assert.Fail("Expected cancellation");}catch(OperationCanceledException){}Assert.IsFalse(YuiLocalModelExecutionGate.Busy);}
        [Test] public async Task LowMemoryCancellationAlsoStopsQueuedRequests(){var path=Model();using(var lease=YuiLocalModelExecutionGate.Enter(CancellationToken.None)){var task=YuiLlamaRuntime.GenerateAsync(Pack,path,new YuiLocalAiChatRequest{Input="Hello"},new YuiLocalModelOptions{ContextTokens=4096,OutputTokens=32},CancellationToken.None);YuiLlamaRuntime.CancelActive();try{await task;Assert.Fail("Queued request must not start after cancellation");}catch(OperationCanceledException){}}Assert.IsFalse(YuiLocalModelExecutionGate.Busy);}
        [Test] public async Task SuspendedRequestsCancelAndResumeCanGenerateAgain(){var path=Model();YuiLlamaRuntime.SetSuspended(true);try{try{await YuiLlamaRuntime.GenerateAsync(Pack,path,new YuiLocalAiChatRequest{Input="Hello"},new YuiLocalModelOptions{ContextTokens=4096,OutputTokens=32},CancellationToken.None);Assert.Fail("Suspended request must not start");}catch(OperationCanceledException){}}finally{YuiLlamaRuntime.SetSuspended(false);}var result=await YuiLlamaRuntime.GenerateAsync(Pack,path,new YuiLocalAiChatRequest{Input="Say hello"},new YuiLocalModelOptions{ContextTokens=4096,OutputTokens=32,Temperature=0},CancellationToken.None);Assert.IsTrue(result.Success,result.ErrorMessage);Assert.IsFalse(YuiLocalModelExecutionGate.Busy);}
    }
}
