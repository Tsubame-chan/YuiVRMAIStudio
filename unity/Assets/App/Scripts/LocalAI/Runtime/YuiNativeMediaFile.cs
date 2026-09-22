using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiNativeMediaFile
    {
        // Enter on Unity's main thread. Only ordinary file/native work crosses
        // the thread boundary; native recognition retains the file until it ends.
        public static Task<T> RunAsync<T>(byte[] bytes, string extension,
            Func<string, T> recognize, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = Application.temporaryCachePath;
            var path = Path.Combine(directory, "yui-media-" + Guid.NewGuid().ToString("N") + extension);
            return Task.Run(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(path, bytes);
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = recognize(path);
                    cancellationToken.ThrowIfCancellationRequested();
                    return result;
                }
                finally
                {
                    try { File.Delete(path); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }, cancellationToken);
        }
    }
}
