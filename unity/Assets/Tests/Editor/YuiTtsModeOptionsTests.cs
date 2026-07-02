using NUnit.Framework;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiTtsModeOptionsTests
    {
        [Test]
        public void Labels_ShowVoiceChoicesWithoutBackendOfflineDuplication()
        {
            var labels = YuiTtsModeOptions.Labels(includeHttpTts: true, httpTtsAvailable: true);

            CollectionAssert.AreEqual(
                new[]
                {
                    "VOICEVOX",
                    "AivisSpeech HD",
                    "Irodori TTS",
                    "Silent",
                },
                labels);
        }

        [Test]
        public void Labels_KeepIrodoriVisibleWhenBackendDoesNotAdvertiseIt()
        {
            var labels = YuiTtsModeOptions.Labels(includeHttpTts: true, httpTtsAvailable: false);

            CollectionAssert.AreEqual(
                new[]
                {
                    "VOICEVOX",
                    "AivisSpeech HD",
                    "Irodori TTS (Backend setup required)",
                    "Silent",
                },
                labels);
        }

        [Test]
        public void Labels_DoNotExposeBackendAndOfflineVoicevoxSeparately()
        {
            var labels = YuiTtsModeOptions.Labels(includeHttpTts: true, httpTtsAvailable: true);

            CollectionAssert.AreEqual(
                new[]
                {
                    "VOICEVOX",
                    "AivisSpeech HD",
                    "Irodori TTS",
                    "Silent",
                },
                labels);
            Assert.AreEqual("server", YuiTtsModeOptions.ModeFromIndex(0, true));
            Assert.AreEqual("aivis", YuiTtsModeOptions.ModeFromIndex(1, true));
            Assert.AreEqual("server-http", YuiTtsModeOptions.ModeFromIndex(2, true));
        }

        [Test]
        public void ModeFromIndex_MapsIrodoriOptionToServerHttpOnlyWhenIncluded()
        {
            Assert.AreEqual("server", YuiTtsModeOptions.ModeFromIndex(0, includeHttpTts: true));
            Assert.AreEqual("aivis", YuiTtsModeOptions.ModeFromIndex(1, includeHttpTts: true));
            Assert.AreEqual("server-http", YuiTtsModeOptions.ModeFromIndex(2, includeHttpTts: true));
            Assert.AreEqual("silent", YuiTtsModeOptions.ModeFromIndex(3, includeHttpTts: true));
            Assert.AreEqual("aivis", YuiTtsModeOptions.ModeFromIndex(1, includeHttpTts: false));
        }

        [Test]
        public void IndexFromMode_ReturnsIrodoriIndexWhenIncluded()
        {
            Assert.AreEqual(0, YuiTtsModeOptions.IndexFromMode("local-ai", includeHttpTts: true));
            Assert.AreEqual(2, YuiTtsModeOptions.IndexFromMode("server-http", includeHttpTts: true));
            Assert.AreEqual(1, YuiTtsModeOptions.IndexFromMode("server-http", includeHttpTts: false));
            Assert.AreEqual(3, YuiTtsModeOptions.IndexFromMode("silent", includeHttpTts: true));
        }

        [Test]
        public void IndexFromMode_TreatsLegacyLocalVoicevoxAsHiddenBackendVoicevoxOption()
        {
            Assert.AreEqual(0, YuiTtsModeOptions.IndexFromMode("local", includeHttpTts: true));
            Assert.AreEqual(0, YuiTtsModeOptions.IndexFromMode("local", includeHttpTts: false));
        }

        [Test]
        public void Labels_CanHideLocalAiVoiceForDesktopBuilds()
        {
            var labels = YuiTtsModeOptions.Labels(
                includeLocalAi: false,
                includeHttpTts: true,
                httpTtsAvailable: true);

            CollectionAssert.AreEqual(
                new[]
                {
                    "VOICEVOX",
                    "AivisSpeech HD",
                    "Irodori TTS",
                    "Silent",
                },
                labels);
            Assert.AreEqual("server", YuiTtsModeOptions.ModeFromIndex(0, includeLocalAi: false, includeHttpTts: true));
            Assert.AreEqual(0, YuiTtsModeOptions.IndexFromMode("local-ai", includeLocalAi: false, includeHttpTts: true));
        }

        [Test]
        public void Labels_DoNotExposeNativeAivisAsSeparatePcVoiceChoice()
        {
            var labels = YuiTtsModeOptions.Labels(
                includeLocalAi: false,
                includeNativeAivis: true,
                includeHttpTts: false,
                httpTtsAvailable: false);

            CollectionAssert.AreEqual(
                new[]
                {
                    "VOICEVOX",
                    "AivisSpeech HD",
                    "Silent",
                },
                labels);
            Assert.AreEqual("server", YuiTtsModeOptions.ModeFromIndex(0, includeLocalAi: false, includeNativeAivis: true, includeHttpTts: false));
            Assert.AreEqual("aivis", YuiTtsModeOptions.ModeFromIndex(1, includeLocalAi: false, includeNativeAivis: true, includeHttpTts: false));
            Assert.AreEqual(1, YuiTtsModeOptions.IndexFromMode("aivis-native", includeLocalAi: false, includeNativeAivis: true, includeHttpTts: false));
        }

        [Test]
        public void Labels_CollapseBackendAndNativeVoicevoxIntoOneVoiceChoice()
        {
            var labels = YuiTtsModeOptions.Labels(
                includeLocalAi: false,
                includeNativeAivis: true,
                includeNativeVoicevox: true,
                includeHttpTts: true,
                httpTtsAvailable: true);

            CollectionAssert.AreEqual(
                new[]
                {
                    "VOICEVOX",
                    "AivisSpeech HD",
                    "Irodori TTS",
                    "Silent",
                },
                labels);
            Assert.AreEqual("server", YuiTtsModeOptions.ModeFromIndex(0, false, true, true, true));
            Assert.AreEqual("aivis", YuiTtsModeOptions.ModeFromIndex(1, false, true, true, true));
            Assert.AreEqual("server-http", YuiTtsModeOptions.ModeFromIndex(2, false, true, true, true));
            Assert.AreEqual("silent", YuiTtsModeOptions.ModeFromIndex(3, false, true, true, true));
            Assert.AreEqual(0, YuiTtsModeOptions.IndexFromMode("voicevox-native", false, true, true, true));
        }
    }
}
