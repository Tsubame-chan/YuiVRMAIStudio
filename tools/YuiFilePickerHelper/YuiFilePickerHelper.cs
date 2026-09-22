using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

internal static class YuiFilePickerHelper
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            return 2;
        }

        var mode = args[0];
        var resultPath = args[1];

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using (var dialog = new OpenFileDialog())
        {
            dialog.Title = mode == "avatar"
                ? "Open VRM or Unity Avatar Package ZIP"
                : mode == "vrm" ? "Open Custom VRM"
                : "Analyze image with Yui Vision";
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.Multiselect = false;
            dialog.RestoreDirectory = true;
            dialog.Filter = mode == "avatar"
                ? "Avatar files (*.vrm;*.zip)|*.vrm;*.zip|VRM files (*.vrm)|*.vrm|Unity Avatar Package ZIP (*.zip)|*.zip|All files (*.*)|*.*"
                : mode == "vrm" ? "VRM files (*.vrm)|*.vrm|All files (*.*)|*.*"
                : "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return 1;
            }

            try
            {
                var path = mode == "image" ? PrepareImage(dialog.FileName) : dialog.FileName;
                File.WriteAllText(resultPath, path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                File.WriteAllText(resultPath, ex is InvalidDataException ? ex.Message : "Could not decode this image. Try PNG or JPEG.", Encoding.UTF8);
                return 3;
            }
            return 0;
        }
    }

    private static string PrepareImage(string path)
    {
        if (new FileInfo(path).Length > 64L * 1024 * 1024) throw new InvalidDataException("Choose an image smaller than 64 MB.");
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") throw new InvalidDataException("Choose a PNG or JPEG image on Windows.");
        var folder = Path.Combine(Path.GetTempPath(), "YuiPickedFiles", "Image");
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".jpg");
        using (var image = Image.FromFile(path))
        {
            const int orientation = 0x112;
            if (Array.IndexOf(image.PropertyIdList, orientation) >= 0)
            {
                var value = image.GetPropertyItem(orientation).Value[0];
                var rotations = new[] { RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipX,
                    RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipX,
                    RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone };
                if (value < rotations.Length) image.RotateFlip(rotations[value]);
            }
            var scale = Math.Min(1.0, 1600.0 / Math.Max(image.Width, image.Height));
            using (var bitmap = new Bitmap(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale))))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(image, 0, 0, bitmap.Width, bitmap.Height);
                }
                bitmap.Save(target, ImageFormat.Jpeg);
            }
        }
        return target;
    }
}
