using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YuiPhysicalAI.LocalAI;
namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiKokoroIntegrationTests
    {
        private sealed class LocalFiles : IYuiLocalAiAssetHttpClient
        {
            private readonly string archive;
            public LocalFiles(string path) { archive = path; }
            public Task<string> GetStringAsync(string url, CancellationToken token) => throw new NotSupportedException();
            public Task DownloadFileAsync(string url, string destination, long expected, IProgress<YuiLocalAiAssetDownloadProgress> progress, CancellationToken token)
            { token.ThrowIfCancellationRequested(); File.Copy(archive, destination); return Task.CompletedTask; }
        }
        [UnityTest]
        public IEnumerator OptionalSixVoiceZipInstallsThenNativeSynthesisWorksOffline()
        {
#if !UNITY_EDITOR_OSX
            Assert.Ignore("macOS native integration test");
#endif
            var workspace = Directory.GetParent(Application.dataPath).Parent.FullName;
            var source = Path.Combine(workspace, "builds/kokoro-english-six-voices-20261005");
            if (!File.Exists(Path.Combine(source, "kokoro-manifest-entry.json"))) Assert.Ignore("Prepare optional voice data first.");
            var temp = Path.Combine(Path.GetTempPath(), "yui-kokoro-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var asset = JsonConvert.DeserializeObject<YuiLocalAiReleaseAsset>(File.ReadAllText(Path.Combine(source, "kokoro-manifest-entry.json")));
                asset.Url = "https://example.invalid/test-only.zip";
                var manifest = new YuiLocalAiAssetManifest(); manifest.Assets.Add(asset);
                var installer = new YuiLocalAiAssetDownloader(new LocalFiles(Path.Combine(source, asset.Filename)), temp, Path.Combine(temp, "cache"));
                var install = installer.InstallAssetsAsync(manifest, new[] { asset }, null, CancellationToken.None);
                while (!install.IsCompleted) yield return null;
                Assert.IsTrue(install.GetAwaiter().GetResult().Success);
                Assert.IsTrue(YuiKokoroSpeech.IsInstalled(temp));
                CollectionAssert.AreEqual(YuiKokoroSpeech.SupportedVoices, YuiKokoroSpeech.InstalledVoices(temp));
                Assert.AreEqual("af_bella", YuiKokoroSpeech.ResolveVoice("af_sky", YuiKokoroSpeech.InstalledVoices(temp)));
                var results = new System.Collections.Generic.List<object>();
                foreach (var voice in YuiKokoroSpeech.SupportedVoices)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var task = YuiKokoroSpeech.SynthesizeAsync("Hello, I am Yui. It is lovely to talk with you!", voice, temp, Application.dataPath, 1, CancellationToken.None);
                    while (!task.IsCompleted) yield return null;
                    var samples = task.GetAwaiter().GetResult();
                    Assert.That(samples.Length, Is.GreaterThan(24000)); Assert.IsTrue(samples.All(x => !float.IsNaN(x) && Math.Abs(x) <= 1));
                    var dir = Path.Combine(workspace, "docs/reports/kokoro_six_voice_pack_20261005");
                    WriteWav(Path.Combine(dir, voice + "-desktop.wav"), samples);
                    results.Add(new { voice, seconds = timer.Elapsed.TotalSeconds, audio_seconds = samples.Length / 24000.0 });
                    File.WriteAllText(Path.Combine(dir, "desktop-native.json"), JsonConvert.SerializeObject(results, Formatting.Indented));
                }
                // Corrupt installed voice data must be rejected before native inference.
                File.WriteAllBytes(Path.Combine(YuiKokoroSpeech.Root(temp), "af_heart.bin"), new byte[10]);
                var corrupt = YuiKokoroSpeech.SynthesizeAsync("Hello!", "af_heart", temp, Application.dataPath, 1, CancellationToken.None);
                while (!corrupt.IsCompleted) yield return null;
                Assert.IsTrue(corrupt.IsFaulted); Assert.IsInstanceOf<InvalidDataException>(corrupt.Exception.InnerException);
            }
            finally { Directory.Delete(temp, true); }
        }
        private static void WriteWav(string path, float[] samples)
        {
            using var stream = File.Create(path); using var writer = new BinaryWriter(stream);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(24000); writer.Write(48000); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
            foreach (var s in samples) writer.Write((short)(s * 32767));
        }
    }
}
