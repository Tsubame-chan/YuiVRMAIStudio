using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YuiPhysicalAI.Audio;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private async Task ImportImageAndAnalyzeFromPickerAsync()
        {
            AppendLog("System", "画像ファイル選択を開きます...");
            var result = await YuiFilePicker.OpenImageFileAsync();
            if (!result.Opened)
            {
                if (!string.IsNullOrWhiteSpace(result.UserMessage))
                {
                    AppendLog("System", result.UserMessage);
                }

                AppendLog("System", "画像ファイル選択はキャンセルされました。");
                return;
            }

            if (string.IsNullOrWhiteSpace(result.Path))
            {
                return;
            }

            await ImportImageAndAnalyzeAsync(result.Path);
        }

        private async Task CaptureScreenAndAnalyzeAsync()
        {
            if (!await TryCaptureCameraAndAnalyzeAsync())
            {
                SetStatus("Look camera not selected");
                AppendLog("System", "Look用カメラが未設定です。Settings > Camera > Device で使用するカメラを選んでください。");
            }

            SetInteractable(true);
        }

        private async Task<bool> TryCaptureCameraAndAnalyzeAsync()
        {
            if (!await EnsureWebCamAuthorizationAsync())
            {
                AppendLog("System", "カメラ権限が許可されていないため、Lookを使えません。iOS設定でカメラ権限を確認してください。");
                SetStatus("Camera permission denied");
                return true;
            }

            var devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                return false;
            }

            var selectedDevice = SelectLookCameraDevice(devices);
            if (string.IsNullOrWhiteSpace(selectedDevice))
            {
                return false;
            }

            Texture2D frame = null;
            try
            {
                isSending = true;
                SetInteractable(false);
                SetStatus("Looking through camera...");
                AppendLog("System", $"カメラを見ています... {selectedDevice}");

                frame = await CaptureCameraFrameAsync(selectedDevice, true);
                if (frame == null)
                {
                    AppendLog("System", "指定解像度では有効なカメラ画像を取得できませんでした。デフォルト解像度で再試行します。");
                    frame = await CaptureCameraFrameAsync(selectedDevice, false);
                }

                if (frame == null)
                {
                    AppendLog("System", "カメラ画像を取得できませんでした。Camo/OBSなどの仮想カメラは、出力元アプリで映像が動いていることを確認してください。");
                    SetStatus("Camera unavailable");
                    return true;
                }

                AppendLog("System", $"カメラ画像を取得しました: {frame.width}x{frame.height}");
                var imageBytes = YuiVisionImageUtility.EncodeTextureForVision(
                    frame,
                    visionImageMaxLongSide,
                    visionJpegQuality);
                pendingVisionImageAttachment.SetImageDataUrl(YuiVisionImageUtility.ToImageDataUrl(
                    imageBytes,
                    "image/jpeg"));
                if (ShouldAttachImageForApiChat())
                {
                    latestVision = CreateApiAttachedVision("camera");
                    AppendLog("Vision", latestVision.Summary);
                    SetStatus("Ready");
                    return true;
                }

                latestVision = await AnalyzeImageViaRuntimeAsync(
                    imageBytes,
                    "camera.jpg",
                    "camera",
                    "image/jpeg",
                    cancellationTokenSource.Token);

                AppendLog("Vision", latestVision.Summary);
                SetStatus("Ready");
                return true;
            }
            catch (YuiBackendException ex)
            {
                SetStatus("Error");
                AppendLog("System", $"カメラ画像を解析できませんでした。{ex.UserMessage}");
                Debug.LogError(ex);
                return true;
            }
            catch (Exception ex)
            {
                SetStatus("Error");
                AppendLog("System", "カメラ画像を解析できませんでした。カメラ権限、OpenAI Vision設定、Backendログを確認してください。");
                Debug.LogError(ex);
                return true;
            }
            finally
            {
                if (frame != null)
                {
                    Destroy(frame);
                }

                isSending = false;
                SetInteractable(true);
            }
        }

        private async Task<bool> EnsureWebCamAuthorizationAsync()
        {
            if (Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                return true;
            }

            var authorization = Application.RequestUserAuthorization(UserAuthorization.WebCam);
            while (!authorization.isDone)
            {
                await Task.Delay(100, cancellationTokenSource.Token);
            }

            return Application.HasUserAuthorization(UserAuthorization.WebCam);
        }

        private async Task<Texture2D> CaptureCameraFrameAsync(string selectedDevice, bool useRequestedSize)
        {
            WebCamTexture webcam = null;
            try
            {
                webcam = useRequestedSize
                    ? new WebCamTexture(
                        selectedDevice,
                        Mathf.Max(320, lookCameraRequestedWidth),
                        Mathf.Max(240, lookCameraRequestedHeight),
                        15)
                    : new WebCamTexture(selectedDevice);
                webcam.Play();

                var startedAt = Time.realtimeSinceStartup;
                var readyAt = -1f;
                Color32[] pixels = null;
                Color32[] bestPixels = null;
                var bestWidth = 0;
                var bestHeight = 0;
                var bestScore = float.MinValue;
                var candidateFrames = 0;
                while (Time.realtimeSinceStartup - startedAt < 6f)
                {
                    await Task.Delay(140, cancellationTokenSource.Token);
                    if (webcam.width <= 16 || webcam.height <= 16 || !webcam.didUpdateThisFrame)
                    {
                        continue;
                    }

                    pixels = webcam.GetPixels32();
                    if (pixels == null || pixels.Length == 0 || IsProbablyBlackFrame(pixels))
                    {
                        continue;
                    }

                    if (readyAt < 0f)
                    {
                        readyAt = Time.realtimeSinceStartup;
                    }

                    var score = ScoreLookCameraFrame(pixels, webcam.width, webcam.height);
                    candidateFrames++;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestWidth = webcam.width;
                        bestHeight = webcam.height;
                        bestPixels = pixels;
                    }

                    var warmedUp = Time.realtimeSinceStartup - readyAt >= Mathf.Max(0.2f, lookCameraWarmupSeconds);
                    var enoughSamples = candidateFrames >= Mathf.Max(1, lookCameraMaxCandidateFrames);
                    if (warmedUp || enoughSamples)
                    {
                        break;
                    }
                }

                if (bestPixels != null && bestWidth > 16 && bestHeight > 16)
                {
                    var frame = new Texture2D(bestWidth, bestHeight, TextureFormat.RGB24, false);
                    frame.SetPixels32(bestPixels);
                    frame.Apply(false, false);
                    Debug.Log($"Yui Look camera frame captured: device={selectedDevice}, size={bestWidth}x{bestHeight}, requested={useRequestedSize}, candidates={candidateFrames}, score={bestScore:0.00}");
                    return frame;
                }

                if (webcam.width > 16 && webcam.height > 16 && pixels != null && pixels.Length > 0)
                {
                    Debug.LogWarning($"Yui Look camera returned only black frames: device={selectedDevice}, size={webcam.width}x{webcam.height}, requested={useRequestedSize}");
                }

                return null;
            }
            finally
            {
                if (webcam != null)
                {
                    webcam.Stop();
                    Destroy(webcam);
                }
            }
        }

        private static float ScoreLookCameraFrame(Color32[] pixels, int width, int height)
        {
            if (pixels == null || pixels.Length == 0 || width <= 2 || height <= 2)
            {
                return float.MinValue;
            }

            var strideX = Mathf.Max(1, width / 48);
            var strideY = Mathf.Max(1, height / 36);
            var samples = 0;
            var edgeTotal = 0f;
            var brightnessTotal = 0f;
            var saturationTotal = 0f;

            for (var y = strideY; y < height - strideY; y += strideY)
            {
                var row = y * width;
                for (var x = strideX; x < width - strideX; x += strideX)
                {
                    var current = pixels[row + x];
                    var right = pixels[row + x + strideX];
                    var down = pixels[(y + strideY) * width + x];

                    var currentLuma = Luma(current);
                    var rightLuma = Luma(right);
                    var downLuma = Luma(down);
                    edgeTotal += Mathf.Abs(currentLuma - rightLuma) + Mathf.Abs(currentLuma - downLuma);
                    brightnessTotal += currentLuma;
                    saturationTotal += Math.Max(current.r, Math.Max(current.g, current.b)) - Math.Min(current.r, Math.Min(current.g, current.b));
                    samples++;
                }
            }

            if (samples == 0)
            {
                return float.MinValue;
            }

            var brightness = brightnessTotal / samples;
            var brightnessPenalty = brightness < 24f ? (24f - brightness) * 3f : 0f;
            return (edgeTotal / samples) + (saturationTotal / samples * 0.05f) - brightnessPenalty;
        }

        private static float Luma(Color32 pixel)
        {
            return (pixel.r * 0.2126f) + (pixel.g * 0.7152f) + (pixel.b * 0.0722f);
        }

        private static bool IsProbablyBlackFrame(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
            {
                return true;
            }

            var step = Mathf.Max(1, pixels.Length / 2048);
            var samples = 0;
            var brightSamples = 0;
            long total = 0;
            var maxChannel = 0;
            for (var i = 0; i < pixels.Length; i += step)
            {
                var pixel = pixels[i];
                var brightness = pixel.r + pixel.g + pixel.b;
                total += brightness;
                maxChannel = Math.Max(maxChannel, Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)));
                if (brightness > 48)
                {
                    brightSamples++;
                }
                samples++;
            }

            if (samples == 0)
            {
                return true;
            }

            var average = total / (samples * 3f);
            return average < 4f && maxChannel < 20 && brightSamples < 4;
        }

        private string SelectLookCameraDevice(WebCamDevice[] devices)
        {
            preferredLookCameraDevice = NormalizeLookCameraDevice(preferredLookCameraDevice);
            if (!string.IsNullOrWhiteSpace(preferredLookCameraDevice))
            {
                foreach (var device in devices)
                {
                    if (device.name == preferredLookCameraDevice)
                    {
                        return device.name;
                    }
                }

                AppendLog("System", $"選択中のLook用カメラが見つかりません: {preferredLookCameraDevice}");
            }

            foreach (var device in devices)
            {
                if (!device.isFrontFacing)
                {
                    preferredLookCameraDevice = device.name;
                    AppendLog("System", $"Look用カメラを自動選択しました: {preferredLookCameraDevice}");
                    return device.name;
                }
            }

            preferredLookCameraDevice = devices[0].name;
            AppendLog("System", $"Look用カメラを自動選択しました: {preferredLookCameraDevice}");
            return preferredLookCameraDevice;
        }

        private async Task ImportImageAndAnalyzeAsync(string path)
        {
            try
            {
                isSending = true;
                SetInteractable(false);
                SetStatus("Analyzing image...");
                AppendLog("System", $"画像を見ています... {Path.GetFileName(path)}");

                var mimeType = YuiVisionImageUtility.ResolveImageMimeType(path);
                if (string.IsNullOrEmpty(mimeType))
                {
                    AppendLog("System", "対応している画像形式は PNG / JPG / WebP / HEIC / HEIF です。");
                    SetStatus("Ready");
                    return;
                }

                var originalBytes = File.ReadAllBytes(path);
                var imageBytes = YuiVisionImageUtility.TryEncodeImageForVision(
                    originalBytes,
                    visionImageMaxLongSide,
                    visionJpegQuality,
                    out var optimizedBytes)
                    ? optimizedBytes
                    : originalBytes;
                if (optimizedBytes != null)
                {
                    mimeType = "image/jpeg";
                }
                pendingVisionImageAttachment.SetImageDataUrl(YuiVisionImageUtility.ToImageDataUrl(imageBytes, mimeType));
                if (ShouldAttachImageForApiChat())
                {
                    latestVision = CreateApiAttachedVision("general");
                    AppendLog("Vision", latestVision.Summary);
                    SetStatus("Ready");
                    return;
                }

                latestVision = await AnalyzeImageViaRuntimeAsync(
                    imageBytes,
                    Path.GetFileName(path),
                    "general",
                    mimeType,
                    cancellationTokenSource.Token);

                AppendLog("Vision", latestVision.Summary);
                SetStatus("Ready");
            }
            catch (YuiBackendException ex)
            {
                SetStatus("Error");
                AppendLog("System", $"画像を解析できませんでした。{ex.UserMessage}");
                Debug.LogError(ex);
            }
            catch (Exception ex)
            {
                SetStatus("Error");
                AppendLog("System", "画像を解析できませんでした。ファイル形式、OpenAI Vision設定、Backendログを確認してください。");
                Debug.LogError(ex);
            }
            finally
            {
                isSending = false;
                SetInteractable(true);
            }
        }

        private bool ShouldAttachImageForApiChat()
        {
            return IsDirectOpenAiConversationMode()
                || string.Equals(
                    YuiConversationModes.Normalize(conversationMode),
                    YuiConversationModes.Stable,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static VisionResponse CreateApiAttachedVision(string promptType)
        {
            return new VisionResponse
            {
                VisionResultId = "api-attachment-" + Guid.NewGuid().ToString("N"),
                Summary = promptType == "camera"
                    ? "API Mode用にカメラ画像を添付しました。次のメッセージで画像を直接見ながら返答します。"
                    : "API Mode用に画像を添付しました。次のメッセージで画像を直接見ながら返答します。",
                Structured = new VisionStructured(),
                CreatedAt = DateTime.UtcNow.ToString("o")
            };
        }

    }
}
