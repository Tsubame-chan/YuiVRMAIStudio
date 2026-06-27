using NUnit.Framework;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiTtsTuningTests
    {
        [Test]
        public void SafePitchForMode_ClampsIrodoriToQualitySafeRange()
        {
            Assert.AreEqual(-0.5f, YuiTtsTuning.SafePitchForMode("server-http", -0.5f));
            Assert.AreEqual(0.5f, YuiTtsTuning.SafePitchForMode("server-http", 0.5f));
        }

        [Test]
        public void SafePitchForMode_KeepsVoicevoxRangeWide()
        {
            Assert.AreEqual(-0.5f, YuiTtsTuning.SafePitchForMode("local", -0.5f));
            Assert.AreEqual(0.5f, YuiTtsTuning.SafePitchForMode("server", 0.5f));
        }
    }
}
