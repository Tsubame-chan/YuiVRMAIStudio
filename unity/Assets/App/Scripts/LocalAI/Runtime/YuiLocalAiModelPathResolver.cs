using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiLocalAiModelPathResolver
    {
        private const string ModelDirectoryName = "YuiLocalAI/Models";
        private const string RuntimeCacheDirectoryName = "YuiLocalAI/RuntimeCache";

        public static string ModelFileName(YuiLocalAiModelPack pack)
        {
            var value = !string.IsNullOrWhiteSpace(pack?.RuntimeModelRef)
                ? pack.RuntimeModelRef
                : pack?.ModelId;
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            value = value.Trim().Replace('\\', '/');
            var slash = value.LastIndexOf('/');
            return slash >= 0 ? value.Substring(slash + 1) : value;
        }

        public static string PersistentModelPath(YuiLocalAiModelPack pack)
        {
            return Path.Combine(Application.persistentDataPath, ModelDirectoryName, ModelFileName(pack));
        }

        public static string StreamingAssetsModelPath(YuiLocalAiModelPack pack)
        {
            return Path.Combine(Application.streamingAssetsPath, ModelDirectoryName, ModelFileName(pack));
        }

        public static string RuntimeCacheDirectory(YuiLocalAiModelPack pack)
        {
            var packId = string.IsNullOrWhiteSpace(pack?.Id) ? "default" : SanitizePathPart(pack.Id);
            return Path.Combine(Application.persistentDataPath, RuntimeCacheDirectoryName, packId);
        }

        public static async Task<string> EnsureLocalFileAsync(
            YuiLocalAiModelPack pack,
            CancellationToken cancellationToken)
        {
            var fileName = ModelFileName(pack);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new FileNotFoundException("Local AI model pack does not define a runtime model file name.");
            }

            var persistentPath = PersistentModelPath(pack);
            if (File.Exists(persistentPath))
            {
                return persistentPath;
            }

            var streamingPath = StreamingAssetsModelPath(pack);
            if (File.Exists(streamingPath))
            {
                return streamingPath;
            }

            if (streamingPath.Contains("://", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(persistentPath));
                using var request = UnityWebRequest.Get(streamingPath);
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    File.WriteAllBytes(persistentPath, request.downloadHandler.data);
                    return persistentPath;
                }
            }

            throw new FileNotFoundException(
                $"Local AI model file is missing. Expected {persistentPath} or {streamingPath}.");
        }

        private static string SanitizePathPart(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Trim().ToCharArray();
            for (var index = 0; index < chars.Length; index++)
            {
                if (Array.IndexOf(invalid, chars[index]) >= 0)
                {
                    chars[index] = '_';
                }
            }

            return new string(chars);
        }
    }
}
