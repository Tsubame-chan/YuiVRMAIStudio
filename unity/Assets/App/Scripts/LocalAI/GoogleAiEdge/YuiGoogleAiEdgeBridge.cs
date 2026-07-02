using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiGoogleAiEdgeBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern IntPtr YuiGoogleAiEdgeBridge_Invoke(string requestJson);

        [DllImport("__Internal")]
        private static extern void YuiGoogleAiEdgeBridge_Free(IntPtr pointer);
#endif

        public static bool IsSupported
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return true;
#elif UNITY_ANDROID && !UNITY_EDITOR
                return true;
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
                return !string.IsNullOrWhiteSpace(FindStandaloneLiteRtLmBinary());
#else
                return false;
#endif
            }
        }

        public static YuiGoogleAiEdgeBridgeResponse Invoke(YuiGoogleAiEdgeBridgeRequest request)
        {
            if (request == null)
            {
                return Error("invalid_request", "Google AI Edge bridge request is null.");
            }

            var requestJson = JsonConvert.SerializeObject(request);
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                return Parse(InvokeNativeJson(() => YuiGoogleAiEdgeBridge_Invoke(requestJson)));
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (var bridge = new AndroidJavaClass("jp.tsubamechan.yuivrm.localai.YuiGoogleAiEdgeBridge"))
                {
                    return Parse(bridge.CallStatic<string>("invoke", requestJson));
                }
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
                return InvokeStandaloneLiteRtLm(request);
#else
                return Error("platform_unsupported", "Google AI Edge bridge is only available on supported player builds.");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Yui Google AI Edge bridge failed: {ex.Message}");
                return Error("bridge_error", ex.Message);
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        private static string InvokeNativeJson(Func<IntPtr> invoke)
        {
            var pointer = invoke();
            if (pointer == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                return PtrToUtf8String(pointer);
            }
            finally
            {
                YuiGoogleAiEdgeBridge_Free(pointer);
            }
        }

        private static string PtrToUtf8String(IntPtr pointer)
        {
            var length = 0;
            while (Marshal.ReadByte(pointer, length) != 0)
            {
                length++;
            }

            if (length == 0)
            {
                return string.Empty;
            }

            var buffer = new byte[length];
            Marshal.Copy(pointer, buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }
#endif

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        private static YuiGoogleAiEdgeBridgeResponse InvokeStandaloneLiteRtLm(YuiGoogleAiEdgeBridgeRequest request)
        {
            if (!string.Equals(request.Capability, YuiLocalAiCapability.Chat.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Error("capability_unavailable", "Standalone LiteRT-LM bridge currently supports chat only.");
            }

            var binary = FindStandaloneLiteRtLmBinary();
            if (string.IsNullOrWhiteSpace(binary))
            {
                return Error("runtime_missing", "litert-lm command was not found for the standalone local AI bridge.");
            }

            if (string.IsNullOrWhiteSpace(request.ModelPath) || !File.Exists(request.ModelPath))
            {
                return Error("model_file_missing", $"Local model file was not found: {request.ModelPath}");
            }

            var executionModelPath = PrepareStandaloneModelPath(request.ModelPath, request.CacheDirectory);

            var chat = JsonConvert.DeserializeObject<YuiLocalAiChatRequest>(request.PayloadJson ?? "{}")
                ?? new YuiLocalAiChatRequest();
            var prompt = string.IsNullOrWhiteSpace(chat.Prompt)
                ? chat.Message
                : chat.Prompt;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return Error("invalid_request", "Local chat prompt is empty.");
            }

            var standalonePrompt = string.IsNullOrWhiteSpace(request.SystemInstruction)
                ? YuiLocalAiPromptBuilder.BuildPromptWithSystemInstruction(chat)
                : CombineStandaloneSystemPrompt(request.SystemInstruction, prompt);

            var response = InvokeStandaloneLiteRtLmBackend(
                binary,
                executionModelPath,
                standalonePrompt,
                request.RuntimeModelRef,
                "gpu");
            if (!response.Ok && IsStandaloneGpuInitializationFailure(response.ErrorMessage))
            {
                Debug.LogWarning("Yui LiteRT-LM GPU backend failed; retrying with CPU backend for macOS standalone validation.");
                response = InvokeStandaloneLiteRtLmBackend(
                    binary,
                    executionModelPath,
                    standalonePrompt,
                    request.RuntimeModelRef,
                    "cpu");
            }

            return response;
        }

        private static string CombineStandaloneSystemPrompt(string systemInstruction, string prompt)
        {
            return "システム指示:\n"
                + (systemInstruction ?? string.Empty).Trim()
                + "\n\n"
                + (prompt ?? string.Empty).Trim();
        }

        private static YuiGoogleAiEdgeBridgeResponse InvokeStandaloneLiteRtLmBackend(
            string binary,
            string executionModelPath,
            string prompt,
            string runtimeModelRef,
            string backend)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = binary,
                Arguments = string.Join(
                    " ",
                    "run",
                    QuoteArgument(executionModelPath),
                    "--prompt",
                    QuoteArgument(prompt),
                    "--backend",
                    backend,
                    "--temperature",
                    "0.45",
                    "--top-k",
                    "30",
                    "--top-p",
                    "0.85",
                    "--max-num-tokens",
                    "1024",
                    "--cache",
                    "disk"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };

            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                if (process == null)
                {
                    return Error("runtime_start_failed", "Failed to start litert-lm.");
                }

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(120000))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception)
                    {
                        // Best-effort cleanup.
                    }

                    return Error("runtime_timeout", "litert-lm did not finish within 120 seconds.");
                }

                if (process.ExitCode != 0)
                {
                    return Error("runtime_error", string.IsNullOrWhiteSpace(stderr) ? $"litert-lm exited with {process.ExitCode}." : stderr.Trim());
                }

                timer.Stop();
                var text = CleanStandaloneOutput(stdout);
                if (IsStandaloneErrorOutput(text))
                {
                    return Error("runtime_error", string.IsNullOrWhiteSpace(stderr) ? text : stderr.Trim());
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    return Error("empty_response", "litert-lm returned an empty response.");
                }

                var response = new YuiLocalAiChatResponse
                {
                    Success = true,
                    Text = text,
                    Face = "Neutral",
                    Animation = "idle_normal",
                    VoiceStyle = "normal",
                    ShouldTts = true,
                    LatencyMs = timer.ElapsedMilliseconds
                };

                return new YuiGoogleAiEdgeBridgeResponse
                {
                    Ok = true,
                    ModelId = runtimeModelRef,
                    PayloadJson = JsonConvert.SerializeObject(response)
                };
            }
        }

        private static bool IsStandaloneGpuInitializationFailure(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.Contains("Failed to initialize WebGPU", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Failed to get WebGPU", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Failed to create LiteRT-LM engine", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Failed to create engine", StringComparison.OrdinalIgnoreCase);
        }

        private static string PrepareStandaloneModelPath(string sourceModelPath, string cacheDirectory)
        {
            if (string.IsNullOrWhiteSpace(cacheDirectory))
            {
                return sourceModelPath;
            }

            Directory.CreateDirectory(cacheDirectory);
            var targetModelPath = Path.Combine(cacheDirectory, Path.GetFileName(sourceModelPath));
            if (File.Exists(targetModelPath))
            {
                try
                {
                    var sourceInfo = new FileInfo(sourceModelPath);
                    var targetInfo = new FileInfo(targetModelPath);
                    if (sourceInfo.Length == targetInfo.Length)
                    {
                        return targetModelPath;
                    }

                    File.Delete(targetModelPath);
                }
                catch (Exception)
                {
                    return sourceModelPath;
                }
            }

            if (TryCreateHardLink(sourceModelPath, targetModelPath))
            {
                return targetModelPath;
            }

            try
            {
                File.Copy(sourceModelPath, targetModelPath, overwrite: true);
                return targetModelPath;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Yui LiteRT-LM cache model staging failed; using source model path. {ex.Message}");
                return sourceModelPath;
            }
        }

        private static bool TryCreateHardLink(string sourcePath, string targetPath)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/ln",
                    Arguments = $"{QuoteArgument(sourcePath)} {QuoteArgument(targetPath)}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return false;
                    }

                    process.WaitForExit(10000);
                    return process.ExitCode == 0 && File.Exists(targetPath);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string FindStandaloneLiteRtLmBinary()
        {
            var env = Environment.GetEnvironmentVariable("YUI_LITERT_LM_BIN");
            if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            {
                return env;
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var candidates = new[]
            {
                "/private/tmp/yui-litert-lm-venv/bin/litert-lm",
                Path.Combine(home, ".cache/yui-vrm-ai-studio/litert-lm-venv/bin/litert-lm"),
                "/opt/homebrew/bin/litert-lm",
                "/usr/local/bin/litert-lm"
            };

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string CleanStandaloneOutput(string output)
        {
            return (output ?? string.Empty).Trim();
        }

        private static bool IsStandaloneErrorOutput(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            return output.StartsWith("An error occurred", StringComparison.OrdinalIgnoreCase)
                || output.Contains("Traceback (most recent call last)", StringComparison.Ordinal);
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
#endif

        private static YuiGoogleAiEdgeBridgeResponse Parse(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                return Error("empty_response", "Google AI Edge bridge returned an empty response.");
            }

            return JsonConvert.DeserializeObject<YuiGoogleAiEdgeBridgeResponse>(responseJson)
                ?? Error("invalid_response", "Google AI Edge bridge returned invalid JSON.");
        }

        private static YuiGoogleAiEdgeBridgeResponse Error(string code, string message)
        {
            return new YuiGoogleAiEdgeBridgeResponse
            {
                Ok = false,
                ErrorCode = code,
                ErrorMessage = message
            };
        }
    }
}
