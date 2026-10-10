using NUnit.Framework;
using YuiPhysicalAI.Audio;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiMicrophoneStopPositionTests
    {
        [TestCase(0, false, 29.99f, 0)]
        [TestCase(0, true, 30f, 0)]
        [TestCase(0, false, 30f, 1440000)]
        public void ThirtySecondRecordingDoesNotSubmitAnEmptyBuffer(int position, bool recording, float elapsed, int expected)
        {
            Assert.AreEqual(expected, YuiUnityMicrophoneRecorder.ResolveStoppedSamplePosition(position, recording, 1440000, 30f, elapsed));
        }

        [TestCase(0, false, 3f, 0)]
        [TestCase(0, true, 3f, 0)]
        [TestCase(0, false, 59.99f, 0)]
        [TestCase(0, false, 60f, 2880000)]
        [TestCase(144000, false, 3f, 144000)]
        [TestCase(144000, true, 3f, 144000)]
        public void InvalidShortRecordingDoesNotBecomeFullMinute(int position,
            bool wasRecording, float elapsed, int expected)
        {
            Assert.AreEqual(expected, YuiUnityMicrophoneRecorder.ResolveStoppedSamplePosition(
                position, wasRecording, 2880000, 60f, elapsed));
        }
    }
}
