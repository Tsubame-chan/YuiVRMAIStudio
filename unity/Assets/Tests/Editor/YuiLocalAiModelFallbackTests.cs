using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiLocalAiModelFallbackTests
    {
        [TestCase("core_text_e2b", true, true, "core_text_e2b")]
        [TestCase("core_text", true, true, "core_text")]
        [TestCase("core_text_e2b", true, false, "core_text_e2b")]
        [TestCase("core_text", false, true, "core_text")]
        [TestCase("core_text_e2b", false, true, "core_text")]
        [TestCase("core_text", true, false, "core_text_e2b")]
        public async Task SelectionAndMissingPreferenceUseResolvedInstalledPath(string selected, bool standard, bool quality, string expected)
        {
            var key=YuiLocalModelSelection.PreferenceKey;
            var existed=UnityEngine.PlayerPrefs.HasKey(key);
            var previous=UnityEngine.PlayerPrefs.GetString(key);
            try
            {
                UnityEngine.PlayerPrefs.SetString(key,selected);
                var runtime=new YuiGoogleAiEdgeLocalAiRuntime(YuiLocalAiModelRegistry.CreateDefaultLocalAi(),
                    (pack,token)=> {
                        if(pack.Id==YuiLocalModelSelection.StandardId ? !standard : !quality) throw new FileNotFoundException("Not installed");
                        return Task.FromResult((pack.Id==YuiLocalModelSelection.StandardId?"/App Bundle/Raw/":"/Apple Assets/追加データ/")+pack.RuntimeModelRef);
                    },
                    (request,token)=> {
                        Assert.That(request.ModelPackId,Is.EqualTo(expected));
                        Assert.That(request.ModelPath,Is.EqualTo((expected==YuiLocalModelSelection.StandardId?"/App Bundle/Raw/":"/Apple Assets/追加データ/")+request.RuntimeModelRef));
                        return new YuiGoogleAiEdgeBridgeResponse { Ok=true,ModelId=request.ModelPackId,PayloadJson="{\"text\":\"hello\"}" };
                    },true);
                var response=await runtime.ChatAsync(new YuiLocalAiChatRequest { Input="hello" },CancellationToken.None);
                Assert.That(response.Success,Is.True);
                Assert.That(response.ModelId,Is.EqualTo(expected));
            }
            finally { if(existed)UnityEngine.PlayerPrefs.SetString(key,previous);else UnityEngine.PlayerPrefs.DeleteKey(key); }
        }
        [Test]
        public async Task SwitchingModelsReusesRuntimeButChangesPathAndCache()
        {
            var key=YuiLocalModelSelection.PreferenceKey;
            var existed=UnityEngine.PlayerPrefs.HasKey(key);var previous=UnityEngine.PlayerPrefs.GetString(key);
            var seen=new List<YuiGoogleAiEdgeBridgeRequest>();
            try
            {
                var runtime=new YuiGoogleAiEdgeLocalAiRuntime(YuiLocalAiModelRegistry.CreateDefaultLocalAi(),
                    YuiLocalAiModelPathResolver.EnsureLocalFileAsync,
                    (request,token)=> { seen.Add(request);return new YuiGoogleAiEdgeBridgeResponse { Ok=true,ModelId=request.ModelPackId,PayloadJson="{\"text\":\"ok\"}" }; },true);
                foreach(var id in new[]{YuiLocalModelSelection.StandardId,YuiLocalModelSelection.QualityId,YuiLocalModelSelection.StandardId})
                {
                    if(!YuiLocalModelSelection.Installed(YuiLocalModelSelection.Find(id))) Assert.Ignore("Requires the two locally installed reviewed models; no download is performed.");
                    UnityEngine.PlayerPrefs.SetString(key,id);
                    var response=await runtime.ChatAsync(new YuiLocalAiChatRequest { Input="hello" },CancellationToken.None);
                    Assert.That(response.ModelId,Is.EqualTo(id));
                    Assert.That(File.Exists(seen[seen.Count-1].ModelPath),Is.True);
                }
                Assert.That(seen[0].ModelPath,Is.Not.EqualTo(seen[1].ModelPath));
                Assert.That(seen[0].CacheDirectory,Is.Not.EqualTo(seen[1].CacheDirectory));
                Assert.That(seen[2].ModelPath,Is.EqualTo(seen[0].ModelPath));
                Assert.That(seen[2].CacheDirectory,Is.EqualTo(seen[0].CacheDirectory));
            }
            finally { if(existed)UnityEngine.PlayerPrefs.SetString(key,previous);else UnityEngine.PlayerPrefs.DeleteKey(key); }
        }
        [TestCase(false, true, "small")]
        [TestCase(true, false, "large")]
        [TestCase(true, true, "large")]
        public async Task SelectsInstalledModelWithoutRequiringBoth(bool large, bool small, string expected)
        {
            await Run(large, small, false, expected, null);
        }

        [Test]
        public async Task WorkUsesRegisteredReasoningBudget()
        {
            await Run(true, false, false, "large", null, "work");
        }

        [Test]
        public async Task MissingOptionalModelDoesNotHideInstalledModelFailure()
        {
            await Run(true, false, true, "large", "litert_lm_error");
        }

        [Test]
        public async Task InstalledFallbackCanRecoverFromPrimaryFailure()
        {
            await Run(true, true, true, "small", null);
        }

        [Test]
        public async Task NeitherInstalledReportsMissingFile()
        {
            await Run(false, false, false, "small", "model_file_missing");
        }

        [Test]
        public async Task NativeCancellationDoesNotStartAnotherModel()
        {
            var pack = Pack("cancel", "cancel-" + Guid.NewGuid().ToString("N") + ".litertlm", 1);
            var fallback = Pack("fallback", "fallback-" + Guid.NewGuid().ToString("N") + ".litertlm", 2);
            var calls = 0;
            try
            {
                var runtime = new YuiGoogleAiEdgeLocalAiRuntime(new YuiLocalAiModelRegistry(new[] { pack, fallback }),
                    (model, token) => Task.FromResult("/test/" + model.RuntimeModelRef),
                    (request, token) => { calls++; return new YuiGoogleAiEdgeBridgeResponse { Ok = false, ErrorCode = "cancelled" }; }, true);
                try
                {
                    await runtime.ChatAsync(new YuiLocalAiChatRequest { Message = "hello" }, CancellationToken.None);
                    Assert.Fail("Native cancellation must propagate instead of starting a fallback model.");
                }
                catch (OperationCanceledException) { }
                Assert.AreEqual(1, calls);
            }
            finally
            {
                foreach (var model in new[] { pack, fallback })
                {
                    var cache = YuiLocalAiModelPathResolver.RuntimeCacheDirectory(model);
                    if (Directory.Exists(cache)) Directory.Delete(cache, true);
                }
            }
        }

        private static async Task Run(bool largeInstalled, bool smallInstalled, bool failLarge,
            string expectedModel, string expectedError, string mode = "talk")
        {
            var suffix = Guid.NewGuid().ToString("N");
            // Deliberately unrelated filenames: registration determines behavior.
            var large = Pack("large", "new-vendor-" + suffix + ".litertlm", 1);
            large.SupportsThinking = true;
            large.ThinkingTokenBudget = 128;
            large.WorkThinkingTokenBudget = 768;
            large.TalkOutputTokenBudget = 512;
            large.SupportsSpeculativeDecoding = true;
            var small = Pack("small", "compact-" + suffix + ".litertlm", 2);
            var invoked = new List<string>();
            try
            {
                var runtime = new YuiGoogleAiEdgeLocalAiRuntime(new YuiLocalAiModelRegistry(new[] { small, large }),
                    (pack, token) => {
                        token.ThrowIfCancellationRequested();
                        if (pack == large ? largeInstalled : smallInstalled) return Task.FromResult("/test/" + pack.RuntimeModelRef);
                        throw new FileNotFoundException("Not installed: " + pack.Id);
                    },
                    (request, token) => {
                        invoked.Add(request.ModelPackId);
                        Assert.AreEqual(request.ModelPackId == "large", request.SupportsThinking);
                        Assert.AreEqual(request.ModelPackId == "large" ? (mode == "work" ? 768 : 128) : 768, request.ThinkingTokenBudget);
                        Assert.AreEqual(request.ModelPackId == "large", request.SupportsSpeculativeDecoding);
                        Assert.AreEqual(mode == "work" ? 2304 : request.ModelPackId == "large" ? 512 : 1280, request.MaxOutputTokens);
                        return failLarge && request.ModelPackId == "large"
                            ? new YuiGoogleAiEdgeBridgeResponse { Ok = false, ErrorCode = "litert_lm_error", ErrorMessage = "Cannot allocate memory" }
                            : new YuiGoogleAiEdgeBridgeResponse { Ok = true, ModelId = request.ModelPackId, PayloadJson = "{\"text\":\"5\"}" };
                    }, true);
                var response = await runtime.ChatAsync(new YuiLocalAiChatRequest { Input = "4+4-3", Mode = mode }, CancellationToken.None);
                Assert.AreEqual(expectedError == null, response.Success);
                Assert.AreEqual(expectedModel, response.ModelId);
                Assert.AreEqual(expectedError, response.ErrorCode);
                if (expectedError == "litert_lm_error") Assert.AreEqual("Cannot allocate memory", response.ErrorMessage);
                if (!largeInstalled) CollectionAssert.DoesNotContain(invoked, "large");
                if (!smallInstalled) CollectionAssert.DoesNotContain(invoked, "small");
            }
            finally
            {
                foreach (var pack in new[] { large, small })
                {
                    var cache = YuiLocalAiModelPathResolver.RuntimeCacheDirectory(pack);
                    if (Directory.Exists(cache)) Directory.Delete(cache, true);
                }
            }
        }

        private static YuiLocalAiModelPack Pack(string id, string file, int priority) => new YuiLocalAiModelPack {
            Id = id, ModelId = id, RuntimeModelRef = file, Priority = priority,
            Provider = "google-litert-lm", Format = "litert-lm", EnabledByDefault = true,
            Capabilities = new[] { YuiLocalAiCapability.Chat }
        };
    }
}
