using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using YuiPhysicalAI.Backend;

namespace YuiPhysicalAI.LocalAI
{
    // A bundled worker, owned by this call. No listening socket or external backend process.
    public static class YuiDesktopInferenceProcess
    {
        private static string python;
        private static string worker;
        private static string nativeLibraries;
        public static string NativeLibraryDirectory => nativeLibraries;

        // Capture Unity paths on the main thread before dispatching any work.
        public static bool IsAvailable
        {
            get
            {
#if UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
                var root = YuiDesktopBackendPaths.ResolveBackendRoot(Application.dataPath, Application.persistentDataPath);
#if UNITY_EDITOR
                root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
#endif
                var windows = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
                python = Path.Combine(root, windows ? "backend/.venv/Scripts/python.exe" : "backend/.venv/bin/python3");
                worker = Path.Combine(root, "scripts/yui_desktop_inference.py");
                nativeLibraries = Path.Combine(root, "runtime/voicevox");
                return File.Exists(python) && File.Exists(worker);
#else
                return false;
#endif
            }
        }

        public static string Invoke(string requestJson, CancellationToken token)
        {
            if (string.IsNullOrEmpty(python) || string.IsNullOrEmpty(worker))
                throw new InvalidOperationException("端末内AIの実行データが未準備です。設定からデータを取得してください。");
            token.ThrowIfCancellationRequested();
            var start = new ProcessStartInfo {
                FileName = python, Arguments = QuoteArgument(worker), UseShellExecute = false,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, CreateNoWindow = true
            };
            start.EnvironmentVariables["PYTHONUTF8"] = "1";
            start.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            var venv = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(python), ".."));
            if (Directory.Exists(Path.Combine(venv, "lib/python3.12/encodings"))) start.EnvironmentVariables["PYTHONHOME"] = venv;
            using var process = Process.Start(start) ?? throw new InvalidOperationException("端末内AIを起動できません。");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            var timer = Stopwatch.StartNew();
            try {
                // Do not put conversation text in command-line arguments or write it to disk.
                var bytes = Encoding.UTF8.GetBytes(requestJson);
                var writing = process.StandardInput.BaseStream.WriteAsync(bytes, 0, bytes.Length, token);
                while (!writing.IsCompleted) { token.ThrowIfCancellationRequested(); if (timer.Elapsed.TotalSeconds > 120) throw new TimeoutException("端末内AIの応答がタイムアウトしました。"); Thread.Sleep(20); }
                writing.GetAwaiter().GetResult(); process.StandardInput.Close();
                while (!process.WaitForExit(50)) {
                    token.ThrowIfCancellationRequested();
                    if (timer.Elapsed.TotalSeconds > 120) throw new TimeoutException("端末内AIの応答がタイムアウトしました。もう一度お試しください。");
                }
                token.ThrowIfCancellationRequested();
                if (process.ExitCode != 0) throw new InvalidOperationException("端末内AIが終了しました (" + process.ExitCode + ")。実行データと空きメモリを確認してください。");
                return stdout.GetAwaiter().GetResult().Trim();
            } finally {
                if (!process.HasExited) { try { process.Kill(); process.WaitForExit(2000); } catch (InvalidOperationException) { } }
                // Drain both pipes; native diagnostics must not be mistaken for a character's reply.
                if (stderr.IsCompleted) _ = stderr.GetAwaiter().GetResult();
            }
        }

        public static string QuoteArgument(string value)
        {
            // ProcessStartInfo's Windows-compatible quoting, including trailing slashes and quotes.
            var result = new StringBuilder("\""); var slashes = 0;
            foreach (var c in value ?? "") {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes); result.Append(c); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
    }
}
