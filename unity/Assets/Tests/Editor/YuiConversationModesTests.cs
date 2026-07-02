using NUnit.Framework;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiConversationModesTests
    {
        [TestCase(null, YuiConversationModes.Stable)]
        [TestCase("", YuiConversationModes.Stable)]
        [TestCase("backend", YuiConversationModes.BackendAi)]
        [TestCase("backend-ai", YuiConversationModes.BackendAi)]
        [TestCase("local-backend", YuiConversationModes.BackendAi)]
        [TestCase("voice", YuiConversationModes.RealtimeVoice)]
        [TestCase("realtime_voice", YuiConversationModes.RealtimeVoice)]
        [TestCase("voice_text", YuiConversationModes.RealtimeVoicevox)]
        [TestCase("voicevox", YuiConversationModes.RealtimeVoicevox)]
        [TestCase("realtime_voicevox", YuiConversationModes.RealtimeVoicevox)]
        [TestCase("aivis_realtime", YuiConversationModes.RealtimeAivis)]
        [TestCase("realtime_aivis", YuiConversationModes.RealtimeAivis)]
        [TestCase("translate", YuiConversationModes.RealtimeTranslate)]
        [TestCase("realtime_translate", YuiConversationModes.RealtimeTranslate)]
        [TestCase("local", YuiConversationModes.LocalAi)]
        [TestCase("on-device", YuiConversationModes.LocalAi)]
        [TestCase("direct_openai", YuiConversationModes.DirectOpenAi)]
        [TestCase("openai_direct", YuiConversationModes.DirectOpenAi)]
        [TestCase("unknown", YuiConversationModes.Stable)]
        public void Normalize_ReturnsCanonicalConversationMode(string input, string expected)
        {
            Assert.AreEqual(expected, YuiConversationModes.Normalize(input));
        }

        [TestCase(YuiConversationModes.Stable, "voice")]
        [TestCase(YuiConversationModes.RealtimeVoice, "voice")]
        [TestCase(YuiConversationModes.RealtimeVoicevox, "voice_text")]
        [TestCase(YuiConversationModes.RealtimeAivis, "voice_text")]
        [TestCase(YuiConversationModes.RealtimeTranslate, "translate")]
        public void BackendMode_ReturnsRealtimeBackendMode(string mode, string expected)
        {
            Assert.AreEqual(expected, YuiConversationModes.BackendMode(mode));
        }

        [Test]
        public void DropdownLabels_KeepOnlyModelRoutingChoices()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "Auto Select (Backend > Local)",
                    "Local Gemma SLM (On-device)",
                    "Backend Talk (Standard)",
                    "Realtime Talk (OpenAI Voice)",
                    "Realtime Talk (VOICEVOX)",
                    "Realtime Talk (AivisSpeech HD)",
                    "Realtime Translation (Backend)",
                    "Direct OpenAI API (No Backend)"
                },
                YuiConversationModes.DropdownLabels);
        }

        [Test]
        public void WarningText_AppearsForLocalAndRealtimeModes()
        {
            Assert.IsEmpty(YuiConversationModes.ExperimentalWarningText(YuiConversationModes.Stable));
            StringAssert.Contains("バックエンド", YuiConversationModes.ExperimentalWarningText(YuiConversationModes.BackendAi));
            StringAssert.Contains("APIキー", YuiConversationModes.ExperimentalWarningText(YuiConversationModes.DirectOpenAi));
            StringAssert.Contains("端末内モデル", YuiConversationModes.ExperimentalWarningText(YuiConversationModes.LocalAi));
            StringAssert.Contains("Realtime Talk Aivis ON", YuiConversationModes.ExperimentalWarningText(YuiConversationModes.RealtimeAivis));
            StringAssert.Contains("Realtime Translate ON", YuiConversationModes.ExperimentalWarningText(YuiConversationModes.RealtimeTranslate));
            StringAssert.Contains("実験機能", YuiConversationModes.ExperimentalWarningText(YuiConversationModes.RealtimeTranslate));
        }

        [TestCase(YuiConversationModes.Stable, 0)]
        [TestCase(YuiConversationModes.LocalAi, 1)]
        [TestCase(YuiConversationModes.BackendAi, 2)]
        [TestCase(YuiConversationModes.RealtimeVoice, 3)]
        [TestCase(YuiConversationModes.BackendVoice, 3)]
        [TestCase(YuiConversationModes.RealtimeVoicevox, 4)]
        [TestCase(YuiConversationModes.BackendVoiceText, 4)]
        [TestCase(YuiConversationModes.RealtimeAivis, 5)]
        [TestCase(YuiConversationModes.RealtimeTranslate, 6)]
        [TestCase(YuiConversationModes.BackendTranslate, 6)]
        [TestCase(YuiConversationModes.DirectOpenAi, 7)]
        public void DropdownIndex_ReturnsSettingsDropdownIndex(string mode, int expected)
        {
            Assert.AreEqual(expected, YuiConversationModes.DropdownIndex(mode));
        }

        [TestCase(0, YuiConversationModes.Stable)]
        [TestCase(1, YuiConversationModes.LocalAi)]
        [TestCase(2, YuiConversationModes.BackendAi)]
        [TestCase(3, YuiConversationModes.RealtimeVoice)]
        [TestCase(4, YuiConversationModes.RealtimeVoicevox)]
        [TestCase(5, YuiConversationModes.RealtimeAivis)]
        [TestCase(6, YuiConversationModes.RealtimeTranslate)]
        [TestCase(7, YuiConversationModes.DirectOpenAi)]
        [TestCase(8, YuiConversationModes.Stable)]
        [TestCase(99, YuiConversationModes.Stable)]
        public void FromDropdownIndex_ReturnsCanonicalMode(int index, string expected)
        {
            Assert.AreEqual(expected, YuiConversationModes.FromDropdownIndex(index));
        }

        [Test]
        public void RealtimeAivis_UsesTextRealtimeWithUnityTtsPlayback()
        {
            Assert.IsTrue(YuiConversationModes.IsRealtime(YuiConversationModes.RealtimeAivis));
            Assert.IsTrue(YuiConversationModes.IsRealtimeTextTts(YuiConversationModes.RealtimeAivis));
            Assert.IsTrue(YuiConversationModes.IsRealtimeTextTts(YuiConversationModes.RealtimeVoicevox));
            Assert.IsFalse(YuiConversationModes.IsRealtimeTextTts(YuiConversationModes.RealtimeVoice));
        }

        [Test]
        public void InstructionsForMode_BuildsVoicevoxTextOnlyInstructionWithCharacterName()
        {
            var instruction = YuiConversationModes.InstructionsForMode(YuiConversationModes.BackendVoiceText, "Mika");

            StringAssert.StartsWith("Mikaとして", instruction);
            StringAssert.Contains("テキストだけ", instruction);
            StringAssert.Contains("80字前後", instruction);
        }

        [Test]
        public void InstructionsForMode_BuildsTranslationInstructionWithoutCharacterName()
        {
            var instruction = YuiConversationModes.InstructionsForMode(YuiConversationModes.BackendTranslate, "Mika");

            StringAssert.Contains("between Japanese and English", instruction);
            StringAssert.Contains("If the user speaks English", instruction);
            Assert.IsFalse(instruction.Contains("Mika"));
        }
    }
}
