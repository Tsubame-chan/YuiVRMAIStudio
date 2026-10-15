using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.LocalAI;
using YuiPhysicalAI.UI;
namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiEnglishNativeRegressionTests
    {
        [Test] public async Task EnglishNameCleaningProducesRealKokoroAudio()
        {
#if UNITY_EDITOR_OSX
            var persistent=Environment.GetEnvironmentVariable("YUI_TEST_KOKORO_PERSISTENT");
            if(string.IsNullOrWhiteSpace(persistent)||!YuiKokoroSpeech.IsInstalled(persistent))Assert.Ignore("Set YUI_TEST_KOKORO_PERSISTENT to isolated test voice data.");
            using var timeout=new CancellationTokenSource(30000);
            var cleaned=YuiSpeechTextUtility.CleanSpeechText("Hello, I am Yui. It is lovely to talk with you!","en");
            var audio=await YuiKokoroSpeech.SynthesizeAsync(cleaned,"af_bella",persistent,Application.dataPath,1,timeout.Token);
            Assert.That(audio.Length,Is.GreaterThan(2400));
            Assert.That(audio.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)),Is.True);
            Assert.That(audio.Any(v=>Math.Abs(v)>.001f),Is.True);
#else
            Assert.Ignore("Real macOS Kokoro native regression.");await Task.CompletedTask;
#endif
        }
    }
}
