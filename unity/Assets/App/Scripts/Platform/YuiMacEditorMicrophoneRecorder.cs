using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace YuiPhysicalAI.Platform
{
#if UNITY_EDITOR_OSX
    public sealed class YuiMacEditorMicrophoneRecorder : IDisposable
    {
        private Process process;
        private string tempDirectory;
        private string wavPath;
        private string stopPath;
        private string statusPath;
        private volatile float latestLevel;
        private volatile float finalRms;
        private volatile float finalPeak;
        private readonly System.Text.StringBuilder stderr = new System.Text.StringBuilder();
        private readonly object stderrLock = new object();

        private static string HelperExecutablePath
        {
            get
            {
                var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
                return string.IsNullOrEmpty(projectRoot)
                    ? string.Empty
                    : Path.Combine(projectRoot, "Tools", "macos", "YuiMacMicRecorder.app");
            }
        }

        public static bool IsSupported => Directory.Exists(HelperExecutablePath);
        public float LatestLevel
        {
            get
            {
                RefreshStatusFile();
                return latestLevel;
            }
        }
        public float FinalRms => finalRms;
        public float FinalPeak => finalPeak;

        public bool Start(int sampleRate, int maxSeconds)
        {
            if (!IsSupported || process != null)
            {
                UnityEngine.Debug.LogWarning(
                    $"Yui macOS editor microphone fallback is not available. supported={IsSupported}, alreadyRunning={process != null}, helper={HelperExecutablePath}");
                return false;
            }

            tempDirectory = Path.Combine(Path.GetTempPath(), $"yui-mac-editor-mic-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);
            wavPath = Path.Combine(tempDirectory, "recording.wav");
            stopPath = Path.Combine(tempDirectory, "stop");
            statusPath = Path.Combine(tempDirectory, "status.log");
            UnityEngine.Debug.Log(
                $"Yui macOS editor microphone fallback launching. helper={HelperExecutablePath}, wav={wavPath}, stop={stopPath}, status={statusPath}, sampleRate={sampleRate}, maxSeconds={maxSeconds}");

            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/open",
                Arguments = $"-n -W {Quote(HelperExecutablePath)} --args {Quote(wavPath)} {Quote(stopPath)} {Quote(statusPath)} {Math.Max(8000, sampleRate)} {Math.Max(1, maxSeconds)}",
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, args) => HandleOutput(args.Data);
            process.ErrorDataReceived += (_, args) =>
            {
                if (string.IsNullOrEmpty(args.Data))
                {
                    return;
                }

                lock (stderrLock)
                {
                    stderr.AppendLine(args.Data);
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
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Yui macOS editor microphone fallback failed to start: {ex.Message}");
                Cleanup();
                return false;
            }
        }

        public async Task<byte[]> StopAsync()
        {
            var runningProcess = process;
            process = null;
            if (runningProcess == null)
            {
                return null;
            }

            try
            {
                if (!runningProcess.HasExited)
                {
                    File.WriteAllText(stopPath, "stop");
                    await Task.Run(() => runningProcess.WaitForExit(3000));
                    if (!runningProcess.HasExited)
                    {
                        runningProcess.Kill();
                        runningProcess.WaitForExit();
                    }
                }

                RefreshStatusFile();
                var fileExists = File.Exists(wavPath);
                var fileLength = fileExists ? new FileInfo(wavPath).Length : 0;
                UnityEngine.Debug.Log(
                    $"Yui macOS editor microphone fallback process exited. exitCode={runningProcess.ExitCode}, fileExists={fileExists}, fileBytes={fileLength}, latestLevel={latestLevel:F6}, helper={HelperExecutablePath}");
                LogStatusFile();

                if (File.Exists(wavPath))
                {
                    var bytes = File.ReadAllBytes(wavPath);
                    if (bytes.Length > 44)
                    {
                        return bytes;
                    }
                }

                lock (stderrLock)
                {
                    if (stderr.Length > 0)
                    {
                        UnityEngine.Debug.LogWarning($"Yui macOS editor microphone fallback stderr: {stderr}");
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Yui macOS editor microphone fallback failed to stop: {ex.Message}");
                return null;
            }
            finally
            {
                runningProcess.Dispose();
                Cleanup();
            }
        }

        public void Dispose()
        {
            var runningProcess = process;
            process = null;
            try
            {
                if (runningProcess != null && !runningProcess.HasExited)
                {
                    runningProcess.Kill();
                }
            }
            catch
            {
                // Best-effort cleanup during editor teardown.
            }
            finally
            {
                runningProcess?.Dispose();
                Cleanup();
            }
        }

        private void HandleOutput(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (TryReadFloat(line, "level=", out var level))
            {
                latestLevel = Math.Max(0f, Math.Min(1f, level));
                return;
            }

            if (TryReadFloat(line, "rms=", out var rms))
            {
                finalRms = rms;
                return;
            }

            if (TryReadFloat(line, "peak=", out var peak))
            {
                finalPeak = peak;
            }
        }

        private static bool TryReadFloat(string line, string prefix, out float value)
        {
            value = 0f;
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            return float.TryParse(
                line.Substring(prefix.Length),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private void Cleanup()
        {
            try
            {
                if (!string.IsNullOrEmpty(tempDirectory) && Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
            catch
            {
                // Temp cleanup is non-critical.
            }

            tempDirectory = null;
            wavPath = null;
            stopPath = null;
            statusPath = null;
            latestLevel = 0f;
        }

        private void RefreshStatusFile()
        {
            if (string.IsNullOrEmpty(statusPath) || !File.Exists(statusPath))
            {
                return;
            }

            try
            {
                foreach (var line in File.ReadAllLines(statusPath))
                {
                    HandleOutput(line);
                }
            }
            catch
            {
                // Status is best-effort telemetry for the editor fallback.
            }
        }

        private void LogStatusFile()
        {
            if (string.IsNullOrEmpty(statusPath) || !File.Exists(statusPath))
            {
                UnityEngine.Debug.LogWarning("Yui macOS editor microphone fallback status file was not created.");
                return;
            }

            try
            {
                UnityEngine.Debug.Log($"Yui macOS editor microphone fallback status:\n{File.ReadAllText(statusPath)}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Yui macOS editor microphone fallback status could not be read: {ex.Message}");
            }
        }
    }
#else
    public sealed class YuiMacEditorMicrophoneRecorder : IDisposable
    {
        public static bool IsSupported => false;
        public float LatestLevel => 0f;
        public float FinalRms => 0f;
        public float FinalPeak => 0f;
        public bool Start(int sampleRate, int maxSeconds) => false;
        public Task<byte[]> StopAsync() => Task.FromResult<byte[]>(null);
        public void Dispose() { }
    }
#endif
}
