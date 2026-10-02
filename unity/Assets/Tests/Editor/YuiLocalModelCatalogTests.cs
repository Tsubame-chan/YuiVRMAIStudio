using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiLocalModelCatalogTests
    {
        private static YuiLocalAiModelPack Future() => new YuiLocalAiModelPack {
            Id="future_vendor",DisplayName="Future model",Format="litert-lm",RuntimeModelRef="future-it.litertlm",
            DownloadUrl="https://example.com/future-it.litertlm",Sha256=new string('a',64),
            Platforms=new[]{"all"},EnabledByDefault=true,Capabilities=new[]{YuiLocalAiCapability.Chat},
            SupportsThinking=true,ThinkingTokenBudget=96,WorkThinkingTokenBudget=640,
            TalkOutputTokenBudget=384,WorkOutputTokenBudget=1792
        };
        [Test]
        public void RegistrationRejectsTraversalCollisionAndMissingChecksum()
        {
            var pack=Future();
            Assert.That(YuiLocalModelCatalog.TryValidate(pack,new YuiLocalAiModelPack[0],out _),Is.True);
            Assert.That(YuiLocalModelCatalog.TryValidate(pack,new[]{Future()},out _),Is.False);
            pack.RuntimeModelRef="../future-it.litertlm";
            Assert.That(YuiLocalModelCatalog.TryValidate(pack,new YuiLocalAiModelPack[0],out _),Is.False);
            pack.RuntimeModelRef="future-it.litertlm";pack.Sha256=null;
            Assert.That(YuiLocalModelCatalog.TryValidate(pack,new YuiLocalAiModelPack[0],out _),Is.False);
        }
        [Test]
        public void AdvancedSettingsBoundMemoryAndReserveSpaceForFinalAnswer()
        {
            var options=new YuiLocalModelOptions {ContextTokens=4096,OutputTokens=99999,ThinkingTokens=99999,
                Temperature=float.NaN,TopP=float.PositiveInfinity,TimeoutSeconds=99999};
            options.Clamp();
            Assert.That(options.OutputTokens,Is.EqualTo(2048));
            Assert.That(options.ThinkingTokens,Is.EqualTo(1792));
            Assert.That(options.TimeoutSeconds,Is.EqualTo(600));
            Assert.That(float.IsNaN(options.Temperature),Is.False);
            Assert.That(float.IsInfinity(options.TopP),Is.False);
        }
        [TestCase("talk",96,384)]
        [TestCase("work",640,1792)]
        public async Task UnrelatedModelUsesItsRegisteredPathAndModeBudgets(string mode,int thinking,int output)
        {
            var pack=Future();
            var runtime=new YuiGoogleAiEdgeLocalAiRuntime(new YuiLocalAiModelRegistry(new[]{pack}),
                (p,t)=>Task.FromResult("/additional/"+p.RuntimeModelRef),
                (r,t)=> {
                    Assert.That(r.ModelPath,Is.EqualTo("/additional/future-it.litertlm"));
                    Assert.That(r.ThinkingTokenBudget,Is.EqualTo(thinking));
                    Assert.That(r.MaxOutputTokens,Is.EqualTo(output));
                    Assert.That(r.Temperature,Is.EqualTo(mode=="work"?.45f:.65f));
                    return new YuiGoogleAiEdgeBridgeResponse {Ok=true,PayloadJson="{\"text\":\"ok\"}"};
                },true);
            Assert.That((await runtime.ChatAsync(new YuiLocalAiChatRequest {Message="hello",Mode=mode},CancellationToken.None)).Success,Is.True);
        }
        [TestCase("talk")]
        [TestCase("work")]
        public async Task PersonalityModelInstructionsAndDialogueAllReachInference(string mode)
        {
            var pack=Future();pack.Id="prompt-test-"+System.Guid.NewGuid().ToString("N");
            var work=mode=="work";
            var options=YuiLocalModelOptions.Defaults(pack,work);options.CustomPrompt="結論を先に話してください。";options.Save(pack,work);
            try
            {
                var request=YuiLocalAiPromptBuilder.PrepareChatRequest(new YuiLocalAiChatRequest {
                    Mode=mode,Message="続きは？",CharacterName="ユイ",CustomInstruction="フレンドリーに話してください。",ResponseInstruction="説明は短い箇条書きで。",
                    Extra=new System.Collections.Generic.Dictionary<string,object> {
                        [YuiPhysicalAI.Avatar.YuiCharacterDialogueStore.ContextKey]="[{\"User\":\"図書館に行く\",\"Assistant\":\"楽しみだね\"}]"
                    }
                },true);
                var runtime=new YuiGoogleAiEdgeLocalAiRuntime(new YuiLocalAiModelRegistry(new[]{pack}),
                    (p,t)=>Task.FromResult("/additional/"+p.RuntimeModelRef),
                    (r,t)=> {
                        StringAssert.Contains("ユイ",r.SystemInstruction);
                        StringAssert.Contains(options.CustomPrompt,r.SystemInstruction);
                        var payload=Newtonsoft.Json.JsonConvert.DeserializeObject<YuiLocalAiChatRequest>(r.PayloadJson);
                        StringAssert.Contains("フレンドリー",payload.Input);
                        StringAssert.Contains("短い箇条書き",payload.Input);
                        StringAssert.Contains("続きは？",payload.Input);
                        Assert.That(payload.History.Count,Is.EqualTo(2));
                        StringAssert.Contains("図書館",payload.History[0].Content);
                        return new YuiGoogleAiEdgeBridgeResponse {Ok=true,PayloadJson="{\"text\":\"ok\"}"};
                    },true);
                Assert.That((await runtime.ChatAsync(request,CancellationToken.None)).Success,Is.True);
            }
            finally { YuiLocalModelOptions.Reset(pack,work); }
        }
    }
}
