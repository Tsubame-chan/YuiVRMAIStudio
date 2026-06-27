using NUnit.Framework;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiTtsModeOptionsTests
    {
        [Test]
        public void Labels_NameBackendVoicevoxAndIrodoriWhenHttpTtsIsAvailable()
        {
            var labels = YuiTtsModeOptions.Labels(includeHttpTts: true, httpTtsAvailable: true);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Direct VOICEVOX (this device)",
                    "Backend VOICEVOX",
                    "Irodori TTS",
                    "Silent",
                },
                labels);
        }

        [Test]
        public void Labels_MarksIrodoriUnavailableWhenPreviouslySelectedButBackendDoesNotAdvertiseIt()
        {
            var labels = YuiTtsModeOptions.Labels(includeHttpTts: true, httpTtsAvailable: false);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Direct VOICEVOX (this device)",
                    "Backend VOICEVOX",
                    "Irodori TTS (unavailable)",
                    "Silent",
                },
                labels);
        }

        [Test]
        public void ModeFromIndex_MapsIrodoriOptionToServerHttpOnlyWhenIncluded()
        {
            Assert.AreEqual("server-http", YuiTtsModeOptions.ModeFromIndex(2, includeHttpTts: true));
            Assert.AreEqual("silent", YuiTtsModeOptions.ModeFromIndex(3, includeHttpTts: true));
            Assert.AreEqual("silent", YuiTtsModeOptions.ModeFromIndex(2, includeHttpTts: false));
        }

        [Test]
        public void IndexFromMode_ReturnsIrodoriIndexWhenIncluded()
        {
            Assert.AreEqual(2, YuiTtsModeOptions.IndexFromMode("server-http", includeHttpTts: true));
            Assert.AreEqual(1, YuiTtsModeOptions.IndexFromMode("server-http", includeHttpTts: false));
            Assert.AreEqual(3, YuiTtsModeOptions.IndexFromMode("silent", includeHttpTts: true));
        }
    }
}
