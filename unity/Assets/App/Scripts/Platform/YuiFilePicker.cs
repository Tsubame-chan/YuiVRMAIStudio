using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YuiPhysicalAI.Platform
{
    public static class YuiFilePicker
    {
        public readonly struct Result
        {
            public Result(bool opened, string path, string userMessage)
            {
                Opened = opened;
                Path = path;
                UserMessage = userMessage;
            }

            public bool Opened { get; }
            public string Path { get; }
            public string UserMessage { get; }
        }

        public static Task<Result> OpenImageFileAsync()
        {
            return OpenFileAsync("image");
        }

        public static Task<Result> OpenVrmFileAsync()
        {
            return OpenFileAsync("vrm");
        }

        public static bool TryOpenImageFile(out string path, out string userMessage)
        {
            var result = OpenImageFileAsync().GetAwaiter().GetResult();
            path = result.Path;
            userMessage = result.UserMessage;
            return result.Opened;
        }

        public static bool TryOpenVrmFile(out string path, out string userMessage)
        {
            var result = OpenVrmFileAsync().GetAwaiter().GetResult();
            path = result.Path;
            userMessage = result.UserMessage;
            return result.Opened;
        }

        private static Task<Result> OpenFileAsync(string mode)
        {
#if UNITY_EDITOR
            var path = mode == "vrm"
                ? EditorUtility.OpenFilePanel("Open Custom VRM", "", "vrm")
                : EditorUtility.OpenFilePanel(
                    "Analyze image with Yui Vision",
                    "",
                    "png,jpg,jpeg,webp,heic,heif");
            return Task.FromResult(new Result(!string.IsNullOrWhiteSpace(path), path, null));
#elif UNITY_STANDALONE_WIN
            return OpenWindowsHelperFilePanelAsync(mode);
#elif UNITY_STANDALONE_OSX
            return OpenMacFilePanelAsync(mode);
#elif UNITY_ANDROID
            return mode == "vrm"
                ? YuiAndroidFilePicker.OpenVrmAsync()
                : YuiAndroidFilePicker.OpenImageAsync();
#elif UNITY_IOS
            return mode == "vrm"
                ? YuiIOSDocumentPicker.OpenVrmAsync()
                : YuiIOSDocumentPicker.OpenImageAsync();
#elif UNITY_WEBGL
            var message = mode == "vrm"
                ? "WebGL版のVRM選択にはブラウザの<input type=file>連携とメモリ上ロード経路が必要です。"
                : "WebGL版の画像選択にはブラウザの<input type=file>連携が必要です。";
            return Task.FromResult(new Result(false, null, message));
#else
            return Task.FromResult(new Result(false, null, "この環境ではファイル選択にまだ対応していません。"));
#endif
        }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        private static async Task<Result> OpenMacFilePanelAsync(string mode)
        {
            var prompt = mode == "vrm" ? "Open Custom VRM" : "Analyze image with Yui Vision";
            var script = mode == "vrm"
                ? $"POSIX path of (choose file with prompt \"{EscapeAppleScript(prompt)}\")"
                : $"POSIX path of (choose file of type {{\"public.image\"}} with prompt \"{EscapeAppleScript(prompt)}\")";

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/osascript",
                    Arguments = "-e " + QuoteProcessArgument(script),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return new Result(false, null, "macOSファイル選択を起動できませんでした。");
                    }

                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit());
                    var output = (await outputTask).Trim();
                    var error = (await errorTask).Trim();
                    if (process.ExitCode != 0)
                    {
                        return new Result(false, null, string.IsNullOrWhiteSpace(error) ? null : error);
                    }

                    return new Result(!string.IsNullOrWhiteSpace(output) && File.Exists(output), output, null);
                }
            }
            catch (Exception ex)
            {
                return new Result(false, null, $"macOSファイル選択でエラーが発生しました: {ex.Message}");
            }
        }

        private static string EscapeAppleScript(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string QuoteProcessArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static async Task<Result> OpenWindowsHelperFilePanelAsync(string mode)
        {
            var helperPath = Path.Combine(AppContext.BaseDirectory, "YuiFilePickerHelper.exe");
            if (!File.Exists(helperPath))
            {
                return new Result(false, null, $"Windowsファイル選択ヘルパーが見つかりません: {helperPath}");
            }

            var resultPath = Path.Combine(
                Path.GetTempPath(),
                $"yui-file-picker-{Guid.NewGuid():N}.txt");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = helperPath,
                    Arguments = $"{mode} \"{resultPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return new Result(false, null, "Windowsファイル選択ヘルパーを起動できませんでした。");
                    }

                    await Task.Run(() => process.WaitForExit());
                }

                if (!File.Exists(resultPath))
                {
                    return new Result(false, null, null);
                }

                // Force UTF-8 so Japanese paths returned by the helper survive on Windows
                // hosts where the default ANSI code page is CP932/Shift-JIS.
                var path = File.ReadAllText(resultPath, Encoding.UTF8).Trim();
                return new Result(!string.IsNullOrWhiteSpace(path) && File.Exists(path), path, null);
            }
            catch (Exception ex)
            {
                return new Result(false, null, $"Windowsファイル選択ヘルパーでエラーが発生しました: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (File.Exists(resultPath))
                    {
                        File.Delete(resultPath);
                    }
                }
                catch
                {
                    // Best effort cleanup only.
                }
            }
        }

#endif
    }
}
