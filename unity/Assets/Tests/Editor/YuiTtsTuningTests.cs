using NUnit.Framework;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiTtsTuningTests
    {
        [Test]
        public void SafePitchForMode_ClampsIrodoriToQualitySafeRange()
        {
            Assert.AreEqual(YuiTtsTuning.AivisPitchMin, YuiTtsTuning.SafePitchForMode("server-http", -0.5f));
            Assert.AreEqual(YuiTtsTuning.AivisPitchMax, YuiTtsTuning.SafePitchForMode("server-http", 0.5f));
        }

        [Test]
        public void SafePitchForMode_KeepsVoicevoxRangeWide()
        {
            Assert.AreEqual(YuiTtsTuning.VoicevoxPitchMin, YuiTtsTuning.SafePitchForMode("local", -0.5f));
            Assert.AreEqual(YuiTtsTuning.VoicevoxPitchMax, YuiTtsTuning.SafePitchForMode("server", 0.5f));
        }

        [Test]
        public void NormalizeMode_KeepsBackendAndNativeAivisProfilesSeparate()
        {
            Assert.AreEqual("aivis", YuiTtsTuningPrefs.NormalizeMode("aivis"));
            Assert.AreEqual("aivis-native", YuiTtsTuningPrefs.NormalizeMode("aivis-native"));
        }
    }
}
