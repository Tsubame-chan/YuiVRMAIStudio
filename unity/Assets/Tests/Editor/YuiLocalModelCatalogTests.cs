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
        public void PartialOrDamagedSettingsUseTheSelectedModelsModeDefaults()
        {
            var pack=Future();pack.Id="settings-test-"+System.Guid.NewGuid().ToString("N");
            var key="yui.local-model.options."+pack.Id+".work";
            try {
                UnityEngine.PlayerPrefs.SetString(key,"{\"TopK\":12}");
                var restored=YuiLocalModelOptions.Load(pack,true);
                Assert.AreEqual(1792,restored.OutputTokens);
                Assert.AreEqual(640,restored.ThinkingTokens);
                Assert.AreEqual(.45f,restored.Temperature);
                Assert.AreEqual(12,restored.TopK);
                UnityEngine.PlayerPrefs.SetString(key,"{\"OutputTokens\":256,\"TopK\":\"broken\"}");
                Assert.AreEqual(1792,YuiLocalModelOptions.Load(pack,true).OutputTokens);
            } finally { YuiLocalModelOptions.Reset(pack,true); }
        }

        [Test]
        public void ResetRestoresAllParametersAndPromptForOnlyTheChosenModelAndMode()
        {
            var pack=Future();pack.Id="reset-test-"+System.Guid.NewGuid().ToString("N");
            try {
                var talk=YuiLocalModelOptions.Defaults(pack,false);talk.OutputTokens=1024;talk.CustomPrompt="custom talk";talk.Save(pack,false);
                var work=YuiLocalModelOptions.Defaults(pack,true);work.CustomPrompt="custom work";work.Save(pack,true);
                YuiLocalModelOptions.Reset(pack,false);
                var restored=YuiLocalModelOptions.Load(pack,false);
                Assert.AreEqual(384,restored.OutputTokens);
                Assert.AreEqual(96,restored.ThinkingTokens);
                Assert.IsEmpty(restored.CustomPrompt);
                Assert.AreEqual("custom work",YuiLocalModelOptions.Load(pack,true).CustomPrompt);
            } finally { YuiLocalModelOptions.Reset(pack,false);YuiLocalModelOptions.Reset(pack,true); }
        }
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
