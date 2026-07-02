using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public sealed class YuiLightweightImageVisionRuntime : IYuiLocalAiRuntime
    {
        public string RuntimeName => "lightweight-image-vision";

        public YuiLocalAiStatus GetStatus()
        {
            return new YuiLocalAiStatus
            {
                Available = true,
                RuntimeName = RuntimeName,
                Detail = "Local lightweight image descriptor is available. It extracts image size, brightness, contrast, and dominant color without loading a neural vision model.",
                Capabilities = new[] { YuiLocalAiCapability.Vision }
            };
        }

        public bool Supports(YuiLocalAiCapability capability)
        {
            return capability == YuiLocalAiCapability.Vision;
        }

        public Task WarmAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(YuiLocalAiCapability capability, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<YuiLocalAiChatResponse> ChatAsync(YuiLocalAiChatRequest request, CancellationToken cancellationToken)
        {
            return Unsupported<YuiLocalAiChatResponse>(YuiLocalAiCapability.Chat);
        }

        public Task<YuiLocalAiTranscriptionResponse> TranscribeAsync(YuiLocalAiAudioRequest request, CancellationToken cancellationToken)
        {
            return Unsupported<YuiLocalAiTranscriptionResponse>(YuiLocalAiCapability.Transcription);
        }

        public Task<YuiLocalAiSpeechResponse> SynthesizeSpeechAsync(YuiLocalAiSpeechRequest request, CancellationToken cancellationToken)
        {
            return Unsupported<YuiLocalAiSpeechResponse>(YuiLocalAiCapability.SpeechSynthesis);
        }

        public Task<YuiLocalAiVisionResponse> AnalyzeImageAsync(YuiLocalAiVisionRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request?.ImageBytes == null || request.ImageBytes.Length == 0)
            {
                return Task.FromResult(new YuiLocalAiVisionResponse
                {
                    Success = false,
                    ErrorCode = "invalid_image",
                    ErrorMessage = "Image bytes are required."
                });
            }

            Texture2D texture = null;
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, request.ImageBytes, markNonReadable: false))
                {
                    return Task.FromResult(new YuiLocalAiVisionResponse
                    {
                        Success = false,
                        ErrorCode = "image_decode_failed",
                        ErrorMessage = "The local image descriptor could not decode the selected image."
                    });
                }

                var analysis = AnalyzeTexture(texture);
                var summary = FormatSummary(analysis, request.PromptType);
                return Task.FromResult(new YuiLocalAiVisionResponse
                {
                    Success = true,
                    ModelId = RuntimeName,
                    VisionResultId = Guid.NewGuid().ToString("N"),
                    Summary = summary,
                    Structured = new Dictionary<string, object>
                    {
                        ["width"] = analysis.Width,
                        ["height"] = analysis.Height,
                        ["orientation"] = analysis.Orientation,
                        ["brightness"] = analysis.BrightnessLabel,
                        ["contrast"] = analysis.ContrastLabel,
                        ["dominant_color"] = analysis.DominantColorLabel,
                        ["prompt_type"] = string.IsNullOrWhiteSpace(request.PromptType) ? "general" : request.PromptType
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new YuiLocalAiVisionResponse
                {
                    Success = false,
                    ErrorCode = "vision_descriptor_error",
                    ErrorMessage = ex.Message
                });
            }
            finally
            {
                if (texture != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(texture);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                }
            }
        }

        private static ImageAnalysis AnalyzeTexture(Texture2D texture)
        {
            var width = texture.width;
            var height = texture.height;
            var pixels = texture.GetPixels32();
            var step = Mathf.Max(1, pixels.Length / 4096);
            var samples = 0;
            double r = 0;
            double g = 0;
            double b = 0;
            double luma = 0;
            double lumaSquared = 0;

            for (var i = 0; i < pixels.Length; i += step)
            {
                var pixel = pixels[i];
                var pr = pixel.r / 255.0;
                var pg = pixel.g / 255.0;
                var pb = pixel.b / 255.0;
                var y = (0.2126 * pr) + (0.7152 * pg) + (0.0722 * pb);
                r += pr;
                g += pg;
                b += pb;
                luma += y;
                lumaSquared += y * y;
                samples++;
            }

            samples = Mathf.Max(1, samples);
            var averageLuma = luma / samples;
            var variance = Math.Max(0.0, (lumaSquared / samples) - (averageLuma * averageLuma));
            var dominant = DominantColorLabel(r / samples, g / samples, b / samples);
            return new ImageAnalysis
            {
                Width = width,
                Height = height,
                Orientation = width > height ? "landscape" : width < height ? "portrait" : "square",
                BrightnessLabel = averageLuma < 0.28 ? "暗め" : averageLuma > 0.72 ? "明るめ" : "中程度",
                ContrastLabel = variance < 0.015 ? "低め" : variance > 0.075 ? "高め" : "中程度",
                DominantColorLabel = dominant
            };
        }

        private static string DominantColorLabel(double r, double g, double b)
        {
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            if (max - min < 0.08)
            {
                return max > 0.72 ? "白・明色系" : max < 0.28 ? "黒・暗色系" : "グレー系";
            }

            if (r >= g && r >= b)
            {
                return g > b * 1.25 ? "黄・暖色系" : "赤・暖色系";
            }

            if (g >= r && g >= b)
            {
                return b > r * 1.15 ? "青緑系" : "緑系";
            }

            return r > g * 1.15 ? "紫系" : "青系";
        }

        private static string FormatSummary(ImageAnalysis analysis, string promptType)
        {
            var source = string.Equals(promptType, "camera", StringComparison.OrdinalIgnoreCase)
                ? "カメラ画像"
                : "選択画像";
            return $"{source}をローカルで軽量解析しました。{analysis.Width}x{analysis.Height}の{analysis.Orientation}画像で、明るさは{analysis.BrightnessLabel}、コントラストは{analysis.ContrastLabel}、全体の色味は{analysis.DominantColorLabel}です。";
        }

        private static Task<TResponse> Unsupported<TResponse>(YuiLocalAiCapability capability)
            where TResponse : YuiLocalAiResponse, new()
        {
            return Task.FromResult(new TResponse
            {
                Success = false,
                ErrorCode = "capability_unavailable",
                ErrorMessage = $"{RuntimeNameStatic} does not support {capability}."
            });
        }

        private const string RuntimeNameStatic = "lightweight-image-vision";

        private sealed class ImageAnalysis
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public string Orientation { get; set; }
            public string BrightnessLabel { get; set; }
            public string ContrastLabel { get; set; }
            public string DominantColorLabel { get; set; }
        }
    }
}
