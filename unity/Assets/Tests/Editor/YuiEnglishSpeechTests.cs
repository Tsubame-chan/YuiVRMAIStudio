using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using YuiPhysicalAI.LocalAI;
using YuiPhysicalAI.Audio;
namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiEnglishSpeechTests
    {
        [TestCase("en", "voicevox-native", true)]
        [TestCase("en-US", "server", true)]
        [TestCase("ja", "voicevox-native", false)]
        [TestCase("en", "silent", false)]
        [TestCase("en", "backend-profile", false)]
        [TestCase("en", "server-http", false)]
        public void LanguagePolicyPreservesExplicitVoiceEngines(string language, string mode, bool expected)
            => Assert.AreEqual(expected, YuiSpeechLanguage.UsesKokoro(language, mode));
        [Test] public void EnglishVoiceAppearsWithoutJapaneseModelAndKeepsModeIdentity()
        {
            var options = YuiPhysicalAI.UI.YuiVoiceEnvironmentOptions.Build(null, false, false, false, false, false, "server", true, false);
            Assert.That(options.First(p => p.Key == "server").Value, Does.Contain("Kokoro"));
            Assert.That(options.First(p => p.Key == "voicevox-native").Value, Does.Contain("Download needed"));
        }
        [Test] public void LocalChatEnglishDoesNotKeepJapaneseReplyInstruction()
        {
            var request = new YuiLocalAiChatRequest { LanguageCode = "en", Message = "Hello!" };
            Assert.That(YuiLocalAiPromptBuilder.BuildSystemInstruction(request), Does.Contain("Reply in natural English"));
            Assert.That(YuiLocalAiPromptBuilder.BuildCompactSystemInstruction(request), Does.Not.Contain("Reply in natural Japanese"));
            Assert.That(YuiLocalAiPromptBuilder.BuildSystemInstruction(new YuiLocalAiChatRequest()), Does.Contain("日本語"));
        }
        [TestCase("ja", "ja-JP")][TestCase("en", "en-US")]
        public void RecognitionUsesSelectedLocale(string input, string expected) => Assert.AreEqual(expected, YuiSpeechLanguage.Locale(input));
        [TestCase("2026", "two thousand twenty six")][TestCase("007", "zero zero seven")]
        [TestCase("42", "forty two")][TestCase("0", "zero")]
        public void NumbersAreSpoken(string input, string expected) => Assert.AreEqual(expected, YuiKokoroEnglishPhonemizer.NumberWords(input));
        [TestCase(4f)][TestCase(-4f)]
        public void PitchChangesFrequencyWithoutChangingDuration(float semitones)
        {
            const int rate = 24000;
            var input = Enumerable.Range(0, rate * 2).Select(i => (float)(0.2 * Math.Sin(2 * Math.PI * 440 * i / rate))).ToArray();
            var result = YuiSpeechPitch.Shift(input, semitones);
            Assert.AreEqual(input.Length, result.Length);
            Assert.IsTrue(result.All(x => !float.IsNaN(x) && !float.IsInfinity(x) && Math.Abs(x) <= 1));
            int crossings = 0;
            for (int i = rate / 2; i < rate * 3 / 2; i++) if (result[i] <= 0 && result[i + 1] > 0) crossings++;
            Assert.That(crossings, Is.EqualTo(440 * Math.Pow(2, semitones / 12)).Within(4));
        }
        [Test] public void NeutralPitchDoesNotTouchWaveform()
        { var data = new[] { 0.1f, -0.2f }; Assert.AreSame(data, YuiSpeechPitch.Shift(data, 0)); }
        [Test] public void LexiconAndChunkingUseTheActualOptionalPack()
        {
            var root = Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).Parent.FullName, "builds/kokoro-validation-data-20261005");
            if (!File.Exists(Path.Combine(root, "us_gold.json"))) Assert.Ignore("Optional Kokoro test data not installed in this workspace.");
            var g2p = new YuiKokoroEnglishPhonemizer(root);
            Assert.That(g2p.Phonemes("Hello world!"), Does.Contain("həlˈO"));
            Assert.That(g2p.Phonemes("Xqzzz"), Is.Not.Empty);
            Assert.That(g2p.Phonemes("I have 42 cats."), Does.Contain("fˈɔɹɾi"));
            Assert.That(g2p.Phonemes("2.5%"), Does.Contain("pˈYnt"));
            Assert.That(g2p.Phonemes("Yui"), Is.EqualTo("jˈui"));
            var chunks = g2p.Tokenize(string.Join(" ", Enumerable.Repeat("Hello, I am Yui. It is lovely to talk with you!", 15))).ToArray();
            Assert.That(chunks.Length, Is.GreaterThan(1));
            Assert.IsTrue(chunks.All(c => c.Length <= 242 && c[0] == 0 && c[c.Length - 1] == 0));
            Assert.Throws<ArgumentException>(() => g2p.Phonemes("こんにちは"));
        }
    }
}
