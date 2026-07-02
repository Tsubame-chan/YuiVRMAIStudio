using NUnit.Framework;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiRealtimeVadGateTests
    {
        [Test]
        public void Feed_EmitsPrespeechChunks_WhenSpeechStarts()
        {
            var gate = new YuiRealtimeVadGate(YuiRealtimeTuning.ClientVadFor(true));
            var quiet = Chunk(1);
            var speech1 = Chunk(2);
            var speech2 = Chunk(3);

            var quietDecision = gate.Feed(quiet, 0.001f, 1f);
            var candidateDecision = gate.Feed(speech1, 0.02f, 1.1f);
            var startDecision = gate.Feed(speech2, 0.02f, 1.2f);

            Assert.AreEqual(YuiRealtimeVadDecisionKind.None, quietDecision.Kind);
            Assert.AreEqual(YuiRealtimeVadDecisionKind.None, candidateDecision.Kind);
            Assert.AreEqual(YuiRealtimeVadDecisionKind.SpeechStarted, startDecision.Kind);
            Assert.AreEqual(3, startDecision.ChunksToSend.Count);
            Assert.AreSame(quiet, startDecision.ChunksToSend[0].Pcm16);
            Assert.AreSame(speech1, startDecision.ChunksToSend[1].Pcm16);
            Assert.AreSame(speech2, startDecision.ChunksToSend[2].Pcm16);
            Assert.AreEqual(3, gate.SentAudioChunks);
        }

        [Test]
        public void Feed_CommitsAfterConfiguredSilence()
        {
            var gate = new YuiRealtimeVadGate(YuiRealtimeTuning.ClientVadFor(true));

            gate.Feed(Chunk(1), 0.02f, 1f);
            gate.Feed(Chunk(2), 0.02f, 1.1f);
            for (var i = 0; i < 7; i++)
            {
                gate.Feed(Chunk((byte)(10 + i)), 0.02f, 1.2f + (i * 0.1f));
            }

            var decision = gate.Feed(Chunk(20), 0.001f, 2.6f);

            Assert.AreEqual(YuiRealtimeVadDecisionKind.Commit, decision.Kind);
            Assert.AreEqual(10, decision.CommittedChunks);
            Assert.GreaterOrEqual(decision.SilenceSeconds, 0.75f);
        }

        [Test]
        public void Feed_DiscardsShortNoise_WhenTurnIsTooShort()
        {
            var settings = new YuiRealtimeVadSettings(
                speechRms: 0.008f,
                silenceSeconds: 0.2f,
                startChunks: 1,
                minTurnChunks: 3,
                prespeechChunks: 1);
            var gate = new YuiRealtimeVadGate(settings);

            gate.Feed(Chunk(1), 0.02f, 1f);
            var decision = gate.Feed(Chunk(2), 0.001f, 1.3f);

            Assert.AreEqual(YuiRealtimeVadDecisionKind.DiscardShortNoise, decision.Kind);
            Assert.AreEqual(2, decision.CommittedChunks);
            Assert.AreEqual(0, gate.SentAudioChunks);
        }

        private static byte[] Chunk(byte value)
        {
            return new[] { value, value };
        }
    }
}
