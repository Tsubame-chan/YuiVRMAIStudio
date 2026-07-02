using System.Collections.Generic;
using NUnit.Framework;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiRealtimeLogTests
    {
        [Test]
        public void FormatEvents_ReturnsNoneForEmptyEvents()
        {
            Assert.AreEqual("none", YuiRealtimeLog.FormatEvents(null, false));
            Assert.AreEqual("none", YuiRealtimeLog.FormatEvents(new List<string>(), false));
        }

        [Test]
        public void FormatEvents_KeepsShortEventListsReadable()
        {
            var events = new List<string> { "session.created", "response.done" };

            Assert.AreEqual("session.created,response.done", YuiRealtimeLog.FormatEvents(events, false));
        }

        [Test]
        public void FormatEvents_CompactsLongEventListsUnlessVerbose()
        {
            var events = new List<string>
            {
                "session.created",
                "response.created",
                "response.output_audio.delta",
                "response.output_audio_transcript.delta",
                "response.done"
            };

            Assert.AreEqual("5 events: session.created...response.done", YuiRealtimeLog.FormatEvents(events, false));
            Assert.AreEqual(
                "session.created,response.created,response.output_audio.delta,response.output_audio_transcript.delta,response.done",
                YuiRealtimeLog.FormatEvents(events, true));
        }
    }
}
