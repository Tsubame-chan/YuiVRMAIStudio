using NUnit.Framework;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiConversationModesTests
    {
        [TestCase(null, YuiConversationModes.Stable)]
        [TestCase("", YuiConversationModes.Stable)]
        [TestCase("voice", YuiConversationModes.RealtimeVoice)]
        [TestCase("realtime_voice", YuiConversationModes.RealtimeVoice)]
        [TestCase("voice_text", YuiConversationModes.RealtimeVoicevox)]
        [TestCase("voicevox", YuiConversationModes.RealtimeVoicevox)]
        [TestCase("realtime_voicevox", YuiConversationModes.RealtimeVoicevox)]
        [TestCase("translate", YuiConversationModes.RealtimeTranslate)]
        [TestCase("realtime_translate", YuiConversationModes.RealtimeTranslate)]
        [TestCase("unknown", YuiConversationModes.Stable)]
        public void Normalize_ReturnsCanonicalConversationMode(string input, string expected)
        {
            Assert.AreEqual(expected, YuiConversationModes.Normalize(input));
        }

        [TestCase(YuiConversationModes.Stable, "voice")]
        [TestCase(YuiConversationModes.RealtimeVoice, "voice")]
        [TestCase(YuiConversationModes.RealtimeVoicevox, "voice_text")]
        [TestCase(YuiConversationModes.RealtimeTranslate, "translate")]
        public void BackendMode_ReturnsRealtimeBackendMode(string mode, string expected)
        {
            Assert.AreEqual(expected, YuiConversationModes.BackendMode(mode));
        }

        [Test]
        public void DropdownLabels_KeepExperimentalRealtimeLabels()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "Stable",
                    "Realtime Voice (Experimental)",
                    "Realtime VOICEVOX (Experimental)",
                    "Realtime Translate (Experimental)"
                },
                YuiConversationModes.DropdownLabels);
        }

        [Test]
        public void WarningText_OnlyAppearsForRealtimeModes()
        {
            Assert.IsEmpty(YuiConversationModes.ExperimentalWarningText(YuiConversationModes.Stable));
            StringAssert.Contains("Realtime Translate ON", YuiConversationModes.ExperimentalWarningText(YuiConversationModes.RealtimeTranslate));
            StringAssert.Contains("実験機能", YuiConversationModes.ExperimentalWarningText(YuiConversationModes.RealtimeTranslate));
        }

        [TestCase(YuiConversationModes.Stable, 0)]
        [TestCase(YuiConversationModes.RealtimeVoice, 1)]
        [TestCase(YuiConversationModes.BackendVoice, 1)]
        [TestCase(YuiConversationModes.RealtimeVoicevox, 2)]
        [TestCase(YuiConversationModes.BackendVoiceText, 2)]
        [TestCase(YuiConversationModes.RealtimeTranslate, 3)]
        [TestCase(YuiConversationModes.BackendTranslate, 3)]
        public void DropdownIndex_ReturnsSettingsDropdownIndex(string mode, int expected)
        {
            Assert.AreEqual(expected, YuiConversationModes.DropdownIndex(mode));
        }

        [TestCase(0, YuiConversationModes.Stable)]
        [TestCase(1, YuiConversationModes.RealtimeVoice)]
        [TestCase(2, YuiConversationModes.RealtimeVoicevox)]
        [TestCase(3, YuiConversationModes.RealtimeTranslate)]
        [TestCase(99, YuiConversationModes.Stable)]
        public void FromDropdownIndex_ReturnsCanonicalMode(int index, string expected)
        {
            Assert.AreEqual(expected, YuiConversationModes.FromDropdownIndex(index));
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
