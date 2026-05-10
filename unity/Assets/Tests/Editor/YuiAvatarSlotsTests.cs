using NUnit.Framework;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiAvatarSlotsTests
    {
        [TestCase("demo_kikyo", YuiAvatarSlots.DemoKikyo)]
        [TestCase("DEMO_KIKYO", YuiAvatarSlots.DemoKikyo)]
        [TestCase("unitychan_default", YuiAvatarSlots.UnityChanDefault)]
        [TestCase("UNITYCHAN_DEFAULT", YuiAvatarSlots.UnityChanDefault)]
        [TestCase("distribution_default", YuiAvatarSlots.UnityChanDefault)]
        [TestCase("custom_vrm", YuiAvatarSlots.CustomVrm)]
        [TestCase("CUSTOM_VRM", YuiAvatarSlots.CustomVrm)]
        [TestCase("", YuiAvatarSlots.UnityChanDefault)]
        [TestCase("unknown", YuiAvatarSlots.UnityChanDefault)]
        public void Normalize_ReturnsCanonicalSlot(string input, string expected)
        {
            Assert.AreEqual(expected, YuiAvatarSlots.Normalize(input));
        }

        [Test]
        public void Normalize_NullFallsBackToUnityChanDefault()
        {
            Assert.AreEqual(YuiAvatarSlots.UnityChanDefault, YuiAvatarSlots.Normalize(null));
        }
    }
}
