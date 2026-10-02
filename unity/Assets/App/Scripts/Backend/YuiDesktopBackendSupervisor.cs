using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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
        private Task ensureTask;
        private bool localServicesChecked;

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

        public void RequestEnsureBackend(bool forceRestart = false)
        {
#if (UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN) && !UNITY_EDITOR
            if (cancellationTokenSource != null && (ensureTask == null || ensureTask.IsCompleted))
            {
                ensureTask = EnsureBackendAsync(forceRestart, cancellationTokenSource.Token);
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

        private async Task EnsureBackendAsync(bool forceRestart, CancellationToken cancellationToken)
        {
            var configuredBackendUrl = PlayerPrefs.GetString(YuiPrefsKeys.BackendUrl, backendUrl);
            if (!forceRestart && localServicesChecked && await IsHealthyAsync(configuredBackendUrl, cancellationToken))
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
                if (!startedByThisProcess && File.Exists(ownershipFile))
                {
                    File.Delete(ownershipFile);
                }

                // Process.Start can load Mono's native process helpers on first use.
                // Do all process/pipe work off the Unity thread, not just WaitForExit.
                var launchStatus = await Task.Run(() =>
                {
                    var startInfo = CreateStartInfo(backendRoot);
                    startInfo.Environment["YUI_REUSE_EXISTING_BACKEND"] = ShouldReuseExistingBackend(forceRestart) ? "1" : "0";
                    startInfo.Environment["YUI_BACKEND_OWNERSHIP_FILE"] = ownershipFile;
                    using var process = Process.Start(startInfo);
                    if (process == null) return "Backend launcher could not be created.";
                    // Drain both streams so a verbose engine cannot fill a pipe and stall startup.
                    process.OutputDataReceived += (_, __) => { };
                    process.ErrorDataReceived += (_, __) => { };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(15000)) return "Backend services are still starting.";
                    return process.ExitCode == 0 ? "Backend services checked." : $"Backend launcher exited with code {process.ExitCode}.";
                }, cancellationToken);
                UnityEngine.Debug.Log(launchStatus);

                localServicesChecked = true;
                startedByThisProcess = File.Exists(ownershipFile);
                await WaitUntilHealthyAsync(configuredBackendUrl, TimeSpan.FromSeconds(startupTimeoutSeconds), cancellationToken);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                UnityEngine.Debug.LogWarning($"Yui bundled backend auto-start failed: {ex.Message}");
            }
        }

        private static Task<bool> IsHealthyAsync(string url, CancellationToken cancellationToken)
        {
            return Task.Run(() => IsHealthyWorkerAsync(url, cancellationToken), cancellationToken);
        }

        private static async Task<bool> IsHealthyWorkerAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                using (var request = new HttpRequestMessage(HttpMethod.Get, CombineUrl(url, "/health")))
                using (var response = await HttpClient.SendAsync(request, timeout.Token))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return false;
                    }

                    return IsHealthyPayload(await response.Content.ReadAsStringAsync());
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsHealthyPayload(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                var payload = JObject.Parse(json);
                return string.Equals((string)payload["status"], "ok", StringComparison.OrdinalIgnoreCase)
                    && string.Equals((string)payload["database"], "ok", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool ShouldReuseExistingBackend(bool forceRestart)
        {
            return !forceRestart;
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

            try {
                var python=Path.Combine(backendRoot,"backend",".venv",
                    Application.platform==RuntimePlatform.WindowsPlayer?"Scripts/python.exe":"bin/python");
                var helper=Path.Combine(backendRoot,"scripts","service_ownership.py");
                if(!File.Exists(python)||!File.Exists(helper))return;
                var info=new ProcessStartInfo{FileName=python,UseShellExecute=false,CreateNoWindow=true,
                    Arguments=Quote(helper)+" stop-ledger --file "+Quote(ownershipFile)+" --directory "+Quote(Path.Combine(backendRoot,"runtime","owned-services"))};
                var packagedHome=Path.Combine(backendRoot,"backend",".venv");
                if(Directory.Exists(Path.Combine(packagedHome,"lib","python3.12","encodings")))info.Environment["PYTHONHOME"]=packagedHome;
                // The controller verifies birth identities and stops owned descendants.
                // It may finish after the player exits; closing this handle does not kill it.
                using var process=Process.Start(info);
            }catch(Exception ex){UnityEngine.Debug.LogWarning("Owned Backend stop: "+ex.GetType().Name);}
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
