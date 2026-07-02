using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace YuiPhysicalAI.Platform
{
#if UNITY_EDITOR
    public sealed class YuiMacEditorRealtimeMicrophoneStreamer : IDisposable
    {
        private readonly ConcurrentQueue<byte[]> audioChunks = new ConcurrentQueue<byte[]>();
        private Process process;
        private volatile float latestLevel;
        private string tempDirectory;
        private string chunkDirectory;
        private string stopPath;
        private string statusPath;
        private bool fileChunkMode;
        private long statusReadPosition;
        private int nextFileChunkIndex = 1;
        private int latestFileChunkIndex;

        private static string ScriptPath
        {
            get
            {
                return FindToolPath("YuiMacMicStreamer.swift");
            }
        }

        private static string BinaryPath => FindToolPath("YuiMacMicStreamer");
        private static string AppPath => FindToolDirectory("YuiMacMicStreamer.app");
        public static bool IsSupported => UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXEditor
            && (Directory.Exists(AppPath) || File.Exists(BinaryPath) || File.Exists(ScriptPath));
        public float LatestLevel => latestLevel;
        public bool IsRunning => process != null && !process.HasExited;

        public bool Start(int sampleRate)
        {
            if (!IsSupported || process != null)
            {
                UnityEngine.Debug.LogWarning($"Yui macOS realtime microphone streamer unavailable. supported={IsSupported}, running={process != null}, script={ScriptPath}");
                return false;
            }

            tempDirectory = Path.Combine(Path.GetTempPath(), $"yui-mac-editor-realtime-mic-{Guid.NewGuid():N}");
            chunkDirectory = Path.Combine(tempDirectory, "chunks");
            stopPath = Path.Combine(tempDirectory, "stop");
            statusPath = Path.Combine(tempDirectory, "status.log");
            Directory.CreateDirectory(chunkDirectory);

            fileChunkMode = Directory.Exists(AppPath);
            var parentPid = Process.GetCurrentProcess().Id;
            var startInfo = fileChunkMode
                ? new ProcessStartInfo
                {
                    FileName = "/usr/bin/open",
                    Arguments = $"-n -W {Quote(AppPath)} --args --files {Quote(chunkDirectory)} {Quote(stopPath)} {Quote(statusPath)} {Math.Max(8000, sampleRate)} --parent {parentPid}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
                : new ProcessStartInfo
            {
                FileName = File.Exists(BinaryPath) ? BinaryPath : "/usr/bin/swift",
                Arguments = File.Exists(BinaryPath)
                    ? $"{Math.Max(8000, sampleRate)}"
                    : $"{Quote(ScriptPath)} {Math.Max(8000, sampleRate)}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, args) => HandleLine(args.Data);
            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    UnityEngine.Debug.LogWarning($"Yui macOS realtime microphone streamer stderr: {args.Data}");
                }
            };

            try
            {
                if (!process.Start())
                {
                    Cleanup();
                    return false;
                }
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                UnityEngine.Debug.Log($"Yui macOS realtime microphone streamer started: {startInfo.FileName}");
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Yui macOS realtime microphone streamer failed to start: {ex.Message}");
                Cleanup();
                return false;
            }
        }

        public List<byte[]> DrainChunks(int maxChunks = 8)
        {
            if (!IsRunning)
            {
                return new List<byte[]>();
            }

            if (fileChunkMode)
            {
                RefreshStatusFile();
                DrainFileChunks(maxChunks);
            }

            var chunks = new List<byte[]>();
            while (chunks.Count < maxChunks && audioChunks.TryDequeue(out var chunk))
            {
                if (chunk != null && chunk.Length > 0)
                {
                    chunks.Add(chunk);
                }
            }
            return chunks;
        }

        public void DiscardPendingChunks()
        {
            while (audioChunks.TryDequeue(out _)) { }

            if (fileChunkMode)
            {
                RefreshStatusFile();
                nextFileChunkIndex = Math.Max(nextFileChunkIndex, latestFileChunkIndex + 1);
            }
        }

        public void Dispose()
        {
            Cleanup();
        }

        private void HandleLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (line.StartsWith("ready", StringComparison.Ordinal))
            {
                UnityEngine.Debug.Log($"Yui macOS realtime microphone streamer {line}");
                return;
            }

            if (line.StartsWith("error=", StringComparison.Ordinal))
            {
                UnityEngine.Debug.LogWarning($"Yui macOS realtime microphone streamer {line}");
                return;
            }

            var audioPrefix = "audio=";
            var audioStart = line.IndexOf(audioPrefix, StringComparison.Ordinal);
            if (audioStart < 0)
            {
                return;
            }

            var rms = ReadFloatField(line, "rms=");
            latestLevel = Math.Max(0f, Math.Min(1f, rms));
            var start = audioStart + audioPrefix.Length;
            var end = line.IndexOf(';', start);
            var encoded = end >= 0 ? line.Substring(start, end - start) : line.Substring(start);
            try
            {
                audioChunks.Enqueue(Convert.FromBase64String(encoded));
            }
            catch (FormatException ex)
            {
                UnityEngine.Debug.LogWarning($"Yui macOS realtime microphone streamer invalid audio chunk: {ex.Message}");
            }
        }

        private static float ReadFloatField(string line, string key)
        {
            var start = line.IndexOf(key, StringComparison.Ordinal);
            if (start < 0)
            {
                return 0f;
            }

            start += key.Length;
            var end = line.IndexOf(';', start);
            var value = end >= 0 ? line.Substring(start, end - start) : line.Substring(start);
            return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0f;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string FindToolPath(string fileName)
        {
            var directory = new DirectoryInfo(UnityEngine.Application.dataPath);
            for (var depth = 0; directory != null && depth < 6; depth++)
            {
                var candidate = Path.Combine(directory.FullName, "Tools", "macos", fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return string.Empty;
        }

        private static string FindToolDirectory(string directoryName)
        {
            var directory = new DirectoryInfo(UnityEngine.Application.dataPath);
            for (var depth = 0; directory != null && depth < 6; depth++)
            {
                var candidate = Path.Combine(directory.FullName, "Tools", "macos", directoryName);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return string.Empty;
        }

        private void DrainFileChunks(int maxChunks)
        {
            if (string.IsNullOrEmpty(chunkDirectory) || !Directory.Exists(chunkDirectory))
            {
                return;
            }

            var read = 0;
            while (read < maxChunks && nextFileChunkIndex <= latestFileChunkIndex)
            {
                var file = Path.Combine(chunkDirectory, $"chunk-{nextFileChunkIndex:000000000000}.pcm");
                if (!File.Exists(file))
                {
                    break;
                }

                try
                {
                    var bytes = File.ReadAllBytes(file);
                    File.Delete(file);
                    nextFileChunkIndex++;
                    if (bytes.Length > 0)
                    {
                        audioChunks.Enqueue(bytes);
                        read++;
                    }
                }
                catch (IOException)
                {
                    // The helper may still be moving the file into place; retry next frame.
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    // Retry next frame.
                    break;
                }
            }
        }

        private void RefreshStatusFile()
        {
            if (string.IsNullOrEmpty(statusPath) || !File.Exists(statusPath))
            {
                return;
            }

            string[] lines;
            try
            {
                using (var stream = new FileStream(statusPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < statusReadPosition)
                    {
                        statusReadPosition = 0;
                    }

                    stream.Seek(statusReadPosition, SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream))
                    {
                        var text = reader.ReadToEnd();
                        statusReadPosition = stream.Position;
                        if (string.IsNullOrEmpty(text))
                        {
                            return;
                        }

                        lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                }
            }
            catch (IOException)
            {
                return;
            }

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].TrimEnd('\r');
                if (line.StartsWith("ready", StringComparison.Ordinal))
                {
                    UnityEngine.Debug.Log($"Yui macOS realtime microphone streamer {line}");
                }
                else if (line.StartsWith("error=", StringComparison.Ordinal))
                {
                    UnityEngine.Debug.LogWarning($"Yui macOS realtime microphone streamer {line}");
                }

                var rms = ReadFloatField(line, "rms=");
                if (rms > 0f)
                {
                    latestLevel = Math.Max(0f, Math.Min(1f, rms));
                }

                if (TryReadIntField(line, "chunk=", out var chunkIndex))
                {
                    latestFileChunkIndex = Math.Max(latestFileChunkIndex, chunkIndex);
                }
            }

        }

        private static bool TryReadIntField(string line, string key, out int value)
        {
            value = 0;
            var start = line.IndexOf(key, StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            start += key.Length;
            var end = line.IndexOf(';', start);
            var text = end >= 0 ? line.Substring(start, end - start) : line.Substring(start);
            return int.TryParse(text, out value);
        }

        private void Cleanup()
        {
            var runningProcess = process;
            process = null;
            try
            {
                if (!string.IsNullOrEmpty(stopPath))
                {
                    File.WriteAllText(stopPath, "stop");
                }
                if (runningProcess != null && !runningProcess.HasExited)
                {
                    if (!runningProcess.WaitForExit(3000) && !runningProcess.HasExited)
                    {
                        runningProcess.Kill();
                    }
                }
            }
            catch
            {
                // Best-effort cleanup during editor teardown.
            }
            finally
            {
                runningProcess?.Dispose();
                latestLevel = 0f;
                while (audioChunks.TryDequeue(out _)) { }
                try
                {
                    if (!string.IsNullOrEmpty(tempDirectory) && Directory.Exists(tempDirectory))
                    {
                        Directory.Delete(tempDirectory, true);
                    }
                }
                catch
                {
                    // Best-effort cleanup.
                }
                tempDirectory = null;
                chunkDirectory = null;
                stopPath = null;
                statusPath = null;
                fileChunkMode = false;
                statusReadPosition = 0;
                nextFileChunkIndex = 1;
                latestFileChunkIndex = 0;
            }
        }
    }
#else
    public sealed class YuiMacEditorRealtimeMicrophoneStreamer : IDisposable
    {
        public static bool IsSupported => false;
        public float LatestLevel => 0f;
        public bool IsRunning => false;
        public bool Start(int sampleRate) => false;
        public List<byte[]> DrainChunks(int maxChunks = 8) => new List<byte[]>();
        public void DiscardPendingChunks() { }
        public void Dispose() { }
    }
#endif
}
