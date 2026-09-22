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
        public readonly struct Result : IDisposable
        {
            public Result(bool opened, string path, string userMessage, bool temporaryCopy = false)
            {
                Opened = opened;
                Path = path;
                UserMessage = userMessage;
                TemporaryCopy = temporaryCopy;
            }

            public bool Opened { get; }
            public string Path { get; }
            public string UserMessage { get; }
            public bool TemporaryCopy { get; }
            public void Dispose()
            {
                if (!TemporaryCopy || string.IsNullOrEmpty(Path)) return;
                try { File.Delete(Path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        public static Task<Result> OpenImageFileAsync()
        {
            return OpenFileAsync(YuiFilePurpose.Image);
        }

        public static Task<Result> OpenVrmFileAsync()
        {
            return OpenFileAsync(YuiFilePurpose.Vrm);
        }

        public static Task<Result> OpenAvatarFileAsync()
        {
            return OpenFileAsync(YuiFilePurpose.Avatar);
        }

        private static bool pickerOpen;
        public static bool IsOpen => pickerOpen;
        private static async Task<Result> OpenFileAsync(YuiFilePurpose purpose)
        {
            if (pickerOpen) return new Result(false,null,"A file picker is already open.");
            pickerOpen=true;
            Result result = default;
            try
            {
                result=await OpenPlatformFileAsync(purpose.ToString().ToLowerInvariant());
                if (!result.Opened) return result;
                var error=YuiPickedFilePolicy.Validate(result.Path,purpose);
                if (error==null) return result;
                result.Dispose();
                return new Result(false,null,error);
            }
            catch(Exception ex)
            {
                result.Dispose();
                UnityEngine.Debug.LogWarning("File selection failed: "+ex.GetType().Name);
                return new Result(false,null,"Could not read the selected file. Please select it again.");
            }
            finally { pickerOpen=false; }
        }

        private static Task<Result> OpenPlatformFileAsync(string mode)
        {
#if UNITY_EDITOR
            var path = EditorUtility.OpenFilePanelWithFilters(
                YuiPhysicalAI.UI.YuiUiLocalization.Text(mode == "image" ? "Choose image" : "Open Custom VRM"), "",
                new[] { mode == "image" ? "Images" : "Avatars", mode == "image" ? "png,jpg,jpeg" : mode == "avatar" ? "vrm,zip" : "vrm" });
            return Task.FromResult(new Result(!string.IsNullOrWhiteSpace(path), path, null));
#elif UNITY_STANDALONE_WIN
            return OpenWindowsHelperFilePanelAsync(mode);
#elif UNITY_STANDALONE_OSX
            return OpenMacFilePanelAsync(mode);
#elif UNITY_ANDROID
            return mode == "avatar"
                ? YuiAndroidFilePicker.OpenAvatarAsync()
                : mode == "vrm"
                ? YuiAndroidFilePicker.OpenVrmAsync()
                : YuiAndroidFilePicker.OpenImageAsync();
#elif UNITY_IOS
            return mode == "avatar"
                ? YuiIOSDocumentPicker.OpenAvatarAsync()
                : mode == "vrm"
                ? YuiIOSDocumentPicker.OpenVrmAsync()
                : YuiIOSDocumentPicker.OpenImageAsync();
#elif UNITY_WEBGL
            var message = mode == "vrm" || mode == "avatar"
                ? "WebGL版のVRM選択にはブラウザの<input type=file>連携とメモリ上ロード経路が必要です。"
                : "WebGL版の画像選択にはブラウザの<input type=file>連携が必要です。";
            return Task.FromResult(new Result(false, null, message));
#else
            return Task.FromResult(new Result(false, null, "この環境ではファイル選択にまだ対応していません。"));
#endif
        }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        private static bool macPickerOpen;
        [System.Runtime.InteropServices.DllImport("YuiMacFilePicker")]
        private static extern int YuiMacFilePicker_Open(string mode, string title);
        [System.Runtime.InteropServices.DllImport("YuiMacFilePicker")]
        private static extern int YuiMacFilePicker_Status();
        [System.Runtime.InteropServices.DllImport("YuiMacFilePicker")]
        private static extern IntPtr YuiMacFilePicker_Result();

        private static async Task<Result> OpenMacFilePanelAsync(string mode)
        {
            if (macPickerOpen) return new Result(false, null, "A file picker is already open.");
            macPickerOpen = true;
            try
            {
                var prompt = mode == "avatar" ? "Open VRM or Unity Avatar Package ZIP" : mode == "vrm" ? "Open Custom VRM" : "Choose image";
                var started = YuiMacFilePicker_Open(mode, YuiPhysicalAI.UI.YuiUiLocalization.Text(prompt));
                if (started != 1) return new Result(false, null, "Could not open the file picker.");
                // Captures Unity's main-thread context; Cocoa must never be polled on Task.Run.
                while (YuiMacFilePicker_Status() == 0) await Task.Delay(40);
                if (YuiMacFilePicker_Status() == 3) return new Result(false, null, "Could not decode this image. Try PNG or JPEG.");
                if (YuiMacFilePicker_Status() != 1) return new Result(false, null, null);
                var path = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(YuiMacFilePicker_Result());
                return new Result(!string.IsNullOrWhiteSpace(path) && File.Exists(path), path, null, mode == "image");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("macOS file picker failed: " + ex.GetType().Name);
                return new Result(false, null, "Could not open the file picker.");
            }
            finally { macPickerOpen = false; }
        }

#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static async Task<Result> OpenWindowsHelperFilePanelAsync(string mode)
        {
            var helperPath = Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).FullName, "YuiFilePickerHelper.exe");
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

                var exitCode = 0;
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return new Result(false, null, "Windowsファイル選択ヘルパーを起動できませんでした。");
                    }

                    await Task.Run(() => process.WaitForExit());
                    exitCode = process.ExitCode;
                }

                if (!File.Exists(resultPath))
                {
                    return new Result(false, null, null);
                }

                // Force UTF-8 so Japanese paths returned by the helper survive on Windows
                // hosts where the default ANSI code page is CP932/Shift-JIS.
                var path = File.ReadAllText(resultPath, Encoding.UTF8).Trim();
                if (exitCode == 3) return new Result(false, null, path);
                return new Result(!string.IsNullOrWhiteSpace(path) && File.Exists(path), path, null, mode == "image");
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
