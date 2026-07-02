using NUnit.Framework;
using YuiPhysicalAI.Audio;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiVoicevoxTtsTests
    {
        [TestCase(0, 24)]
        [TestCase(50, 24)]
        [TestCase(155, 37)]
        [TestCase(400, 45)]
        public void SynthesisTimeoutSeconds_ScalesWithTextLength(int textLength, int expectedSeconds)
        {
            Assert.AreEqual(expectedSeconds, YuiChatdollVoicevoxTts.SynthesisTimeoutSeconds(textLength));
        }
    }
}
