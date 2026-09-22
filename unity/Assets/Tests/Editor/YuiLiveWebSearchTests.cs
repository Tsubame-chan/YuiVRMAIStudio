using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YuiPhysicalAI.Api;
namespace YuiPhysicalAI.Tests.Editor
{
    public class YuiLiveWebSearchTests
    {
        [UnityTest]
        public IEnumerator DirectWorkSearchReturnsSourcesAndShortSpeech()
        {
            if(Environment.GetEnvironmentVariable("YUI_RUN_LIVE_SEARCH_TEST")!="1") Assert.Ignore("Opt-in paid synthetic API test.");
            var key=Environment.GetEnvironmentVariable("YUI_TEST_OPENAI_KEY");Assert.IsNotEmpty(key);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(100));
            var client=new YuiDirectOpenAiClient(key,Environment.GetEnvironmentVariable("YUI_TEST_OPENAI_MODEL"));
            var task=client.SendChatAsync(new ChatRequest {RequestId=Guid.NewGuid().ToString("N"),Secret=true,Mode="work",Message="OpenAI公式のWeb検索APIドキュメントを検索して、できることを3点と参照URLを教えて。"},timeout.Token);
            while(!task.IsCompleted)yield return null;
            var result=task.GetAwaiter().GetResult();
            StringAssert.Contains("https://",result.Text);
            StringAssert.DoesNotContain("https://",result.SpokenText);
            Assert.Less(result.SpokenText.Length,result.Text.Length);
            Debug.Log("Synthetic Direct Work search result: "+result.Text+"\nSpoken summary: "+result.SpokenText);
        }
    }
}
