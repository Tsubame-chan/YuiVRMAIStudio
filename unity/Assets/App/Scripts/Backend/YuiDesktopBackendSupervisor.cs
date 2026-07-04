using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Backend
{
    public sealed class YuiDesktopBackendSupervisor : MonoBehaviour
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        [SerializeField] private string backendUrl = YuiDesktopBackendPaths.DefaultBackendUrl;
        [SerializeField] private int startupTimeoutSeconds = 90;
        [SerializeField] private bool autoStartBundledBackend = true;

        private string backendRoot;
        private string ownershipFile;
        private bool startedByThisProcess;
        private CancellationTokenSource cancellationTokenSource;

        public static bool ShouldAutoStart(string backendUrl, bool autoStartEnabled, string backendRoot)
        {
            return autoStartEnabled
                && YuiDesktopBackendPaths.IsLocalBackendUrl(backendUrl)
                && !string.IsNullOrWhiteSpace(backendRoot)
                && File.Exists(YuiDesktopBackendPaths.StartScriptPath(backendRoot));
        }

        private void Awake()
        {
            cancellationTokenSource = new CancellationTokenSource();
            Application.quitting += StopOwnedBackendProcesses;
        }

        private void Start()
        {
#if (UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN) && !UNITY_EDITOR
            _ = EnsureBackendAsync(cancellationTokenSource.Token);
#endif
        }

        public void RequestEnsureBackend()
        {
#if (UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN) && !UNITY_EDITOR
            if (cancellationTokenSource != null)
            {
                _ = EnsureBackendAsync(cancellationTokenSource.Token);
            }
#endif
        }

        private void OnDestroy()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            Application.quitting -= StopOwnedBackendProcesses;
        }

        private async Task EnsureBackendAsync(CancellationToken cancellationToken)
        {
            var configuredBackendUrl = PlayerPrefs.GetString(YuiPrefsKeys.BackendUrl, backendUrl);
            if (await IsHealthyAsync(configuredBackendUrl, cancellationToken))
            {
                return;
            }

            backendRoot = YuiDesktopBackendPaths.ResolveBackendRoot(Application.dataPath, Application.persistentDataPath);
            if (!ShouldAutoStart(configuredBackendUrl, autoStartBundledBackend, backendRoot))
            {
                return;
            }

            ownershipFile = YuiDesktopBackendPaths.OwnershipFilePath(backendRoot);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ownershipFile));
                if (File.Exists(ownershipFile))
                {
                    File.Delete(ownershipFile);
                }

                var startInfo = CreateStartInfo(backendRoot);
                startInfo.Environment["YUI_REUSE_EXISTING_BACKEND"] = "1";
                startInfo.Environment["YUI_BACKEND_OWNERSHIP_FILE"] = ownershipFile;

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        UnityEngine.Debug.LogWarning("Yui bundled backend start process could not be created.");
                        return;
                    }

                    await Task.Run(() => process.WaitForExit(15000), cancellationToken);
                    if (!process.HasExited)
                    {
                        UnityEngine.Debug.Log("Yui bundled backend start is still running; continuing health checks.");
                    }
                    else if (process.ExitCode != 0)
                    {
                        UnityEngine.Debug.LogWarning($"Yui bundled backend start exited with code {process.ExitCode}: {process.StandardError.ReadToEnd()}");
                    }
                }

                startedByThisProcess = File.Exists(ownershipFile);
                await WaitUntilHealthyAsync(configuredBackendUrl, TimeSpan.FromSeconds(startupTimeoutSeconds), cancellationToken);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                UnityEngine.Debug.LogWarning($"Yui bundled backend auto-start failed: {ex.Message}");
            }
        }

        private static async Task<bool> IsHealthyAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, CombineUrl(url, "/health")))
                using (var response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        private static async Task WaitUntilHealthyAsync(string url, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                if (await IsHealthyAsync(url, cancellationToken))
                {
                    return;
                }

                await Task.Delay(1000, cancellationToken);
            }
        }

        private void StopOwnedBackendProcesses()
        {
            if (!startedByThisProcess || string.IsNullOrWhiteSpace(ownershipFile) || !File.Exists(ownershipFile))
            {
                return;
            }

            foreach (var pid in ReadOwnedPids(ownershipFile))
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    process.Kill();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.Log($"Yui backend owned process already stopped or could not be stopped: pid={pid}, {ex.Message}");
                }
            }
        }

        private static IEnumerable<int> ReadOwnedPids(string path)
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && int.TryParse(parts[1], out var pid))
                {
                    yield return pid;
                }
            }
        }

        private static string CombineUrl(string baseUrl, string path)
        {
            return (string.IsNullOrWhiteSpace(baseUrl) ? YuiDesktopBackendPaths.DefaultBackendUrl : baseUrl.Trim()).TrimEnd('/')
                + "/"
                + (path ?? string.Empty).TrimStart('/');
        }

        private static ProcessStartInfo CreateStartInfo(string backendRoot)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(YuiDesktopBackendPaths.StartScriptPath(backendRoot)) + " -NoWait -SkipVoicevox",
                WorkingDirectory = backendRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
#else
            return new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = Quote(YuiDesktopBackendPaths.StartScriptPath(backendRoot)),
                WorkingDirectory = backendRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
#endif
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
