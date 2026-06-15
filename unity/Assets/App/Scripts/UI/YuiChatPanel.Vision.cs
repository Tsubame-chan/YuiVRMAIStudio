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
                latestVisionImageDataUrl = YuiVisionImageUtility.ToImageDataUrl(
                    imageBytes,
                    "image/jpeg");
                latestVision = await client.AnalyzeImageAsync(
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
                Color32[] pixels = null;
                while (Time.realtimeSinceStartup - startedAt < 6f)
                {
                    await Task.Delay(120, cancellationTokenSource.Token);
                    if (webcam.width <= 16 || webcam.height <= 16 || !webcam.didUpdateThisFrame)
                    {
                        continue;
                    }

                    pixels = webcam.GetPixels32();
                    if (pixels == null || pixels.Length == 0 || IsProbablyBlackFrame(pixels))
                    {
                        continue;
                    }

                    var frame = new Texture2D(webcam.width, webcam.height, TextureFormat.RGB24, false);
                    frame.SetPixels32(pixels);
                    frame.Apply(false, false);
                    Debug.Log($"Yui Look camera frame captured: device={selectedDevice}, size={webcam.width}x{webcam.height}, requested={useRequestedSize}");
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
            if (string.IsNullOrWhiteSpace(preferredLookCameraDevice))
            {
                return null;
            }

            foreach (var device in devices)
            {
                if (device.name == preferredLookCameraDevice)
                {
                    return device.name;
                }
            }

            AppendLog("System", $"選択中のLook用カメラが見つかりません: {preferredLookCameraDevice}");
            return null;
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
                latestVisionImageDataUrl = YuiVisionImageUtility.ToImageDataUrl(imageBytes, mimeType);
                latestVision = await client.AnalyzeImageAsync(
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


    }
}
