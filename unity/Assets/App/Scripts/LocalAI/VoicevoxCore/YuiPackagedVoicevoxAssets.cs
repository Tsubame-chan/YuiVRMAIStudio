using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    // Native VOICEVOX needs real files; Android StreamingAssets live inside an APK/OBB.
    public static class YuiPackagedVoicevoxAssets
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        public static async Task PrepareAsync(CancellationToken token)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var streaming = Application.streamingAssetsPath;
            if (!streaming.StartsWith("jar:", StringComparison.Ordinal)) return;
            var end = streaming.IndexOf("!/", StringComparison.Ordinal);
            if (end < 0) throw new IOException("Android音声データの保存先を確認できません。");
            var archive = new Uri(streaming.Substring(4, end - 4)).LocalPath;
            var destination = Path.Combine(Application.persistentDataPath, "YuiLocalAI", "Voicevox");
            var version = Application.version;
            await Gate.WaitAsync(token);
            try
            {
                var marker = Path.Combine(destination, ".bundled-version");
                if (File.Exists(marker) && File.ReadAllText(marker) == version
                    && File.Exists(Path.Combine(destination, "Models/meimei_himari_1.vvm"))
                    && File.Exists(Path.Combine(destination, "Models/metan_zundamon_0.vvm"))
                    && File.Exists(Path.Combine(destination, "Models/kyushu_sora_2.vvm"))
                    && File.Exists(Path.Combine(destination, "Models/sayo_15.vvm"))
                    && File.Exists(Path.Combine(destination, "open_jtalk_dic_utf_8-1.11/sys.dic"))) return;
                await Task.Run(() => Extract(archive, destination, version, token), token);
            }
            finally { Gate.Release(); }
#else
            await Task.CompletedTask;
#endif
        }

        public static void Extract(string archivePath, string destination, string version, CancellationToken token)
        {
            const string prefix = "assets/YuiLocalAI/Voicevox/";
            var stage = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var backup = stage + ".old";
            var replaced = false;
            try
            {
                token.ThrowIfCancellationRequested();
                Directory.CreateDirectory(stage);
                using (var archive = ZipFile.OpenRead(archivePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!entry.FullName.StartsWith(prefix, StringComparison.Ordinal) || entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;
                        var path = Path.GetFullPath(Path.Combine(stage, entry.FullName.Substring(prefix.Length)));
                        if (!path.StartsWith(Path.GetFullPath(stage) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                            throw new InvalidDataException("Unsafe bundled voice path.");
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        using var input = entry.Open(); using var output = File.Create(path);
                        var buffer = new byte[81920]; int count;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                        { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, count); }
                    }
                }
                if (!File.Exists(Path.Combine(stage, "Models/meimei_himari_1.vvm"))
                    || !File.Exists(Path.Combine(stage, "open_jtalk_dic_utf_8-1.11/sys.dic")))
                    throw new FileNotFoundException("同梱の音声モデル・辞書が見つかりません。");
                File.WriteAllText(Path.Combine(stage, ".bundled-version"), version);
                token.ThrowIfCancellationRequested();
                if (Directory.Exists(destination)) Directory.Move(destination, backup);
                try { Directory.Move(stage, destination); replaced = true; }
                catch { if (Directory.Exists(backup)) Directory.Move(backup, destination); throw; }
            }
            finally
            {
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
                if (replaced && Directory.Exists(backup)) Directory.Delete(backup, true);
            }
        }
    }
}
