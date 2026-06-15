using System.Collections.Generic;

namespace YuiPhysicalAI.Core
{
    public enum YuiRealtimeVadDecisionKind
    {
        None,
        SpeechStarted,
        Commit,
        DiscardShortNoise
    }

    public readonly struct YuiRealtimeVadChunk
    {
        public YuiRealtimeVadChunk(byte[] pcm16, int chunkIndex)
        {
            Pcm16 = pcm16;
            ChunkIndex = chunkIndex;
        }

        public byte[] Pcm16 { get; }
        public int ChunkIndex { get; }
    }

    public sealed class YuiRealtimeVadDecision
    {
        public YuiRealtimeVadDecision(
            YuiRealtimeVadDecisionKind kind,
            IReadOnlyList<YuiRealtimeVadChunk> chunksToSend,
            int committedChunks,
            float silenceSeconds)
        {
            Kind = kind;
            ChunksToSend = chunksToSend ?? EmptyChunks;
            CommittedChunks = committedChunks;
            SilenceSeconds = silenceSeconds;
        }

        private static readonly IReadOnlyList<YuiRealtimeVadChunk> EmptyChunks = new List<YuiRealtimeVadChunk>();

        public YuiRealtimeVadDecisionKind Kind { get; }
        public IReadOnlyList<YuiRealtimeVadChunk> ChunksToSend { get; }
        public int CommittedChunks { get; }
        public float SilenceSeconds { get; }
    }

    public sealed class YuiRealtimeVadGate
    {
        private readonly Queue<byte[]> prespeechPcmBuffer = new Queue<byte[]>();
        private YuiRealtimeVadSettings settings;
        private bool speechActive;
        private int candidateChunks;
        private float lastSpeechAt = -1f;

        public YuiRealtimeVadGate(YuiRealtimeVadSettings settings)
        {
            this.settings = settings;
        }

        public int SentAudioChunks { get; private set; }

        public void Configure(YuiRealtimeVadSettings nextSettings)
        {
            settings = nextSettings;
        }

        public YuiRealtimeVadDecision Feed(byte[] pcm16, float rms, float now)
        {
            if (pcm16 == null || pcm16.Length == 0)
            {
                return None();
            }

            var chunksToSend = new List<YuiRealtimeVadChunk>();
            var isSpeech = rms >= settings.SpeechRms;
            if (!speechActive)
            {
                RememberPrespeechChunk(pcm16);
                if (!isSpeech)
                {
                    candidateChunks = 0;
                    return None();
                }

                candidateChunks++;
                lastSpeechAt = now;
                if (candidateChunks < settings.StartChunks)
                {
                    return None();
                }

                speechActive = true;
                candidateChunks = 0;
                while (prespeechPcmBuffer.Count > 0)
                {
                    chunksToSend.Add(MarkChunkForSending(prespeechPcmBuffer.Dequeue()));
                }
                return new YuiRealtimeVadDecision(YuiRealtimeVadDecisionKind.SpeechStarted, chunksToSend, 0, 0f);
            }

            if (isSpeech)
            {
                lastSpeechAt = now;
            }

            chunksToSend.Add(MarkChunkForSending(pcm16));
            if (lastSpeechAt <= 0f || now - lastSpeechAt < settings.SilenceSeconds)
            {
                return new YuiRealtimeVadDecision(YuiRealtimeVadDecisionKind.None, chunksToSend, 0, 0f);
            }

            var committedChunks = SentAudioChunks;
            var silenceSeconds = now - lastSpeechAt;
            if (SentAudioChunks < settings.MinTurnChunks)
            {
                Reset();
                return new YuiRealtimeVadDecision(
                    YuiRealtimeVadDecisionKind.DiscardShortNoise,
                    chunksToSend,
                    committedChunks,
                    silenceSeconds);
            }

            ResetSpeechState();
            return new YuiRealtimeVadDecision(
                YuiRealtimeVadDecisionKind.Commit,
                chunksToSend,
                committedChunks,
                silenceSeconds);
        }

        public void Reset()
        {
            ResetSpeechState();
            SentAudioChunks = 0;
        }

        private void ResetSpeechState()
        {
            speechActive = false;
            candidateChunks = 0;
            lastSpeechAt = -1f;
            prespeechPcmBuffer.Clear();
        }

        private void RememberPrespeechChunk(byte[] pcm16)
        {
            prespeechPcmBuffer.Enqueue(pcm16);
            while (prespeechPcmBuffer.Count > settings.PrespeechChunks)
            {
                prespeechPcmBuffer.Dequeue();
            }
        }

        private YuiRealtimeVadChunk MarkChunkForSending(byte[] pcm16)
        {
            SentAudioChunks++;
            return new YuiRealtimeVadChunk(pcm16, SentAudioChunks);
        }

        private static YuiRealtimeVadDecision None()
        {
            return new YuiRealtimeVadDecision(YuiRealtimeVadDecisionKind.None, null, 0, 0f);
        }
    }
}
