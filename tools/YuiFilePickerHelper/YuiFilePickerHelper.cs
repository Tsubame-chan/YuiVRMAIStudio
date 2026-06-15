using System;
using System.IO;
using System.Text;
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
            dialog.Title = mode == "vrm"
                ? "Open Custom VRM"
                : "Analyze image with Yui Vision";
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.Multiselect = false;
            dialog.RestoreDirectory = true;
            dialog.Filter = mode == "vrm"
                ? "VRM files (*.vrm)|*.vrm|All files (*.*)|*.*"
                : "Image files (*.png;*.jpg;*.jpeg;*.webp;*.heic;*.heif)|*.png;*.jpg;*.jpeg;*.webp;*.heic;*.heif|PNG files (*.png)|*.png|JPEG files (*.jpg;*.jpeg)|*.jpg;*.jpeg|All files (*.*)|*.*";

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return 1;
            }

            File.WriteAllText(resultPath, dialog.FileName, Encoding.UTF8);
            return 0;
        }
    }
}
