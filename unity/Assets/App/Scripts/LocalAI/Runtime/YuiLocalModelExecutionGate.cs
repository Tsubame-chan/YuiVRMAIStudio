using System;
using System.Threading;
namespace YuiPhysicalAI.LocalAI
{
    // LiteRT and GGUF share device memory; never load two engines concurrently.
    public static class YuiLocalModelExecutionGate
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1,1);
        public static bool Busy => Gate.CurrentCount == 0;
        public static IDisposable Enter(CancellationToken token) { Gate.Wait(token); return new Lease(); }
        private sealed class Lease : IDisposable { public void Dispose() { Gate.Release(); } }
    }
}
