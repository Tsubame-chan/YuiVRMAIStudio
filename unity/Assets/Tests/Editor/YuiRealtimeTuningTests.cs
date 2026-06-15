using NUnit.Framework;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiRealtimeTuningTests
    {
        [Test]
        public void ClientVadFor_ReturnsVoiceConversationThresholds()
        {
            var settings = YuiRealtimeTuning.ClientVadFor(YuiConversationModes.BackendVoice);

            Assert.AreEqual(0.008f, settings.SpeechRms);
            Assert.AreEqual(0.9f, settings.SilenceSeconds);
            Assert.AreEqual(5, settings.StartChunks);
            Assert.AreEqual(5, settings.MinTurnChunks);
            Assert.AreEqual(5, settings.PrespeechChunks);
        }

        [Test]
        public void ClientVadFor_ReturnsTranslateThresholds()
        {
            var settings = YuiRealtimeTuning.ClientVadFor(YuiConversationModes.BackendTranslate);

            Assert.AreEqual(0.008f, settings.SpeechRms);
            Assert.AreEqual(0.75f, settings.SilenceSeconds);
            Assert.AreEqual(2, settings.StartChunks);
            Assert.AreEqual(10, settings.MinTurnChunks);
            Assert.AreEqual(10, settings.PrespeechChunks);
        }

        [Test]
        public void PlaybackGain_ExposesRealtimeAudioNormalizationSettings()
        {
            Assert.AreEqual(0.003f, YuiRealtimeTuning.AudioMinPlayablePeak);
            Assert.AreEqual(0.62f, YuiRealtimeTuning.AudioTargetPeak);
            Assert.AreEqual(3f, YuiRealtimeTuning.AudioMaxAutoGain);
        }
    }
}
