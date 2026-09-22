using System;
using System.IO;

namespace YuiPhysicalAI.Platform
{
    public enum YuiFilePurpose { Image, Vrm, Avatar }

    // Dialog filters are hints: Android/iOS cloud providers may show other files.
    // Validate again at the common boundary before any decoder or importer runs.
    public static class YuiPickedFilePolicy
    {
        public const long MaxImageBytes = 64L * 1024 * 1024;
        public const long MaxAvatarBytes = 512L * 1024 * 1024;
        public static string Extensions(YuiFilePurpose purpose) => purpose == YuiFilePurpose.Image
            ? "png,jpg,jpeg,webp,heic,heif" : purpose == YuiFilePurpose.Vrm ? "vrm" : "vrm,zip";
        public static string Validate(string path, YuiFilePurpose purpose)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "The selected file is no longer available. Please select it again.";
            var ext=Path.GetExtension(path).TrimStart('.');
            if (Array.FindIndex(Extensions(purpose).Split(','),e=>string.Equals(e,ext,StringComparison.OrdinalIgnoreCase))<0)
                return purpose == YuiFilePurpose.Image ? "Choose a PNG, JPEG, WebP or HEIF image." : "Choose a VRM or a ZIP exported by Yui Avatar Bridge.";
            var bytes=new FileInfo(path).Length;
            if(bytes==0) return "The selected file is empty.";
            if(bytes>(purpose==YuiFilePurpose.Image?MaxImageBytes:MaxAvatarBytes))
                return purpose==YuiFilePurpose.Image ? "Choose an image smaller than 64 MB." : "Choose an avatar file smaller than 512 MB.";
            return null;
        }
    }
}
