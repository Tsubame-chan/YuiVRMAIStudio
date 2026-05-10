using System;
using System.IO;
using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public static class YuiVisionImageUtility
    {
        public static string ResolveImageMimeType(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".png":
                    return "image/png";
                case ".webp":
                    return "image/webp";
                case ".heic":
                    return "image/heic";
                case ".heif":
                    return "image/heif";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                default:
                    return null;
            }
        }

        public static string ToImageDataUrl(byte[] imageBytes, string mimeType)
        {
            return $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}";
        }

        public static byte[] EncodeTextureForVision(
            Texture2D source,
            int maxLongSide,
            int jpegQuality)
        {
            if (source == null)
            {
                return Array.Empty<byte>();
            }

            maxLongSide = Mathf.Max(256, maxLongSide);
            jpegQuality = Mathf.Clamp(jpegQuality, 40, 95);
            var longSide = Mathf.Max(source.width, source.height);
            if (longSide <= maxLongSide)
            {
                return source.EncodeToJPG(jpegQuality);
            }

            var scale = maxLongSide / (float)longSide;
            var targetWidth = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            var targetHeight = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            var previous = RenderTexture.active;
            var renderTexture = RenderTexture.GetTemporary(
                targetWidth,
                targetHeight,
                0,
                RenderTextureFormat.ARGB32);
            Texture2D resized = null;

            try
            {
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                resized = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
                resized.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                resized.Apply(false, false);
                return resized.EncodeToJPG(jpegQuality);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
                if (resized != null)
                {
                    UnityEngine.Object.Destroy(resized);
                }
            }
        }

        public static bool TryEncodeImageForVision(
            byte[] imageBytes,
            int maxLongSide,
            int jpegQuality,
            out byte[] optimizedBytes)
        {
            optimizedBytes = null;
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return false;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(imageBytes, false))
                {
                    return false;
                }

                optimizedBytes = EncodeTextureForVision(texture, maxLongSide, jpegQuality);
                return optimizedBytes != null && optimizedBytes.Length > 0;
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }
    }
}
