using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiIrodoriSpeech
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string Library = "__Internal";
#else
        private const string Library = "YuiIrodoriBridge";
#endif
        [DllImport(Library, CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr YuiIrodori_Synthesize(string root,string text,string caption,string reference,string output);
        [DllImport(Library, CallingConvention=CallingConvention.Cdecl)] private static extern void YuiIrodori_Cancel();
        [DllImport(Library, CallingConvention=CallingConvention.Cdecl)] private static extern void YuiIrodori_Reset();
        [DllImport(Library, CallingConvention=CallingConvention.Cdecl)] private static extern void YuiIrodori_Free(IntPtr error);
        [DllImport(Library, CallingConvention=CallingConvention.Cdecl)] private static extern void YuiIrodori_Release();
        [DllImport(Library, CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr YuiIrodori_Prepare(string root,string reference);
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1,1);
        public static string Root(string persistent) => Path.Combine(persistent,"YuiLocalAI","Irodori");
        public static bool IsInstalled(string persistent) => File.Exists(Path.Combine(Root(persistent),".verified"));
        public static readonly string[] Voices = {"soft_yui","gentle_friend"};
        public static string Caption(string voice) => voice == "gentle_friend"
            ? "若い女性の、親しみやすく優しい声で、落ち着いたテンポで話してください。"
            : "若い女性の、やわらかく可愛い声で、少し甘めに自然な日本語で話してください。";
        // Tiny synthetic reference clips are included; the 1.96 GB model remains optional.
        private static string EnsureReference(string voice, string persistent)
        {
            if (Array.IndexOf(Voices, voice) < 0) throw new ArgumentException("Unknown Irodori voice.", nameof(voice));
            var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>("YuiVoicePacks/IrodoriReferences/" + voice);
            if (asset == null) throw new FileNotFoundException("Irodori reference audio is missing.");
            var directory = Path.Combine(Root(persistent), "yui-references-v41-fp16");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, voice + ".wav");
            var bytes = asset.bytes;
            // Refresh bundled references across application updates without trusting an old same-size file.
            if (!File.Exists(path) || !System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(path), bytes))
                File.WriteAllBytes(path, bytes);
            return path;
        }
        [UnityEngine.RuntimeInitializeOnLoadMethod]
        private static void RegisterMemoryPressure() { UnityEngine.Application.lowMemory -= OnLowMemory; UnityEngine.Application.lowMemory += OnLowMemory; }
        private static async void OnLowMemory()
        {
            try { await ReleaseAsync(); } catch (Exception e) { UnityEngine.Debug.LogWarning("Irodori release: " + e.Message); }
        }
        private static bool loaded;
        public static async Task ReleaseAsync()
        {
            await Gate.WaitAsync();
            try { if (loaded) { await Task.Run(YuiIrodori_Release); loaded = false; } }
            finally { Gate.Release(); }
        }
        public static async Task PrepareAsync(string voice, string persistent, CancellationToken token)
        {
            if (!IsInstalled(persistent)) return;
            var reference = EnsureReference(voice, persistent);
            await Gate.WaitAsync(token);
            try {
                await Task.Run(() => {
                    token.ThrowIfCancellationRequested();
                    YuiIrodori_Reset();
                    using var registration = token.Register(YuiIrodori_Cancel);
                    loaded = true;
                    var error = YuiIrodori_Prepare(Root(persistent), reference);
                    try { token.ThrowIfCancellationRequested(); if (error != IntPtr.Zero) throw new InvalidOperationException(Marshal.PtrToStringAnsi(error)); }
                    finally { if (error != IntPtr.Zero) YuiIrodori_Free(error); }
                }, token);
            } finally { Gate.Release(); }
        }
        public static async Task<byte[]> SynthesizeAsync(string text,string voice,string persistent,CancellationToken token)
        {
            if(!IsInstalled(persistent)) throw new InvalidOperationException("Download the Irodori voice pack first.");
            var reference = EnsureReference(voice, persistent);
            await Gate.WaitAsync(token);
            var output=Path.Combine(Path.GetTempPath(),"yui-irodori-"+Guid.NewGuid().ToString("N")+".wav");
            try {
                return await Task.Run(()=> {
                    token.ThrowIfCancellationRequested();
                    YuiIrodori_Reset();
                    using var registration=token.Register(YuiIrodori_Cancel);
                    loaded = true;
                    var error=YuiIrodori_Synthesize(Root(persistent),text,Caption(voice),reference,output);
                    try {token.ThrowIfCancellationRequested();if(error!=IntPtr.Zero)throw new InvalidOperationException(Marshal.PtrToStringAnsi(error));}
                    finally {if(error!=IntPtr.Zero)YuiIrodori_Free(error);}
                    return File.ReadAllBytes(output);
                },token);
            } finally {if(File.Exists(output))File.Delete(output);Gate.Release();}
        }
    }
}
