using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace YuiPhysicalAI.Avatar
{
    public sealed class YuiAvatarPackageLoadResult
    {
        public GameObject Root { get; set; }
        public string AvatarId { get; set; }
        public string DisplayName { get; set; }
    }

    public static class YuiAvatarPackageLoader
    {
        public const string Format = "unity-avatar-package";
        public const int CurrentSchemaVersion = 1;
        private const long MaximumExpandedBytes = 8L * 1024L * 1024L * 1024L;
        private const int MaximumEntries = 256;
        private const long MaximumManifestBytes = 1024 * 1024;
        private static readonly HashSet<string> ForbiddenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".dll", ".exe", ".bat", ".cmd", ".com", ".ps1", ".sh", ".command", ".dylib", ".so", ".js", ".jar",
        };

        public static async Task<YuiAvatarPackageLoadResult> LoadAsync(string packagePath, Transform parent, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            {
                throw new FileNotFoundException("Avatar package ZIP was not found.", packagePath);
            }

            cancellationToken.ThrowIfCancellationRequested();
            JObject manifest;
            string payloadHash;
            long payloadSize;
            string extractedPath = null;
            using (var archive = ZipFile.OpenRead(packagePath))
            {
                ValidateArchive(archive);
                var manifestEntry = archive.GetEntry("manifest.json")
                    ?? throw new InvalidDataException("Avatar package does not contain manifest.json.");
                using (var reader = new StreamReader(manifestEntry.Open()))
                {
                    manifest = JObject.Parse(await reader.ReadToEndAsync());
                }

                ValidateManifest(manifest);
                var payload = SelectPayload(manifest);
                var filename = payload.Value<string>("filename");
                var entry = archive.GetEntry(filename)
                    ?? throw new InvalidDataException($"Avatar package payload is missing: {filename}");
                var expectedHash = payload.Value<string>("sha256");
                var expectedSize = payload.Value<long?>("sizeBytes")
                    ?? throw new InvalidDataException("Avatar package payload size is missing from manifest.json.");
                if (expectedSize <= 0 || expectedSize != entry.Length)
                    throw new InvalidDataException("Avatar package payload size does not match manifest.json.");
                if (string.IsNullOrWhiteSpace(expectedHash) || expectedHash.Length != 64
                    || expectedHash.Any(character => !Uri.IsHexDigit(character)))
                {
                    throw new InvalidDataException("Avatar package payload SHA-256 is missing from manifest.json.");
                }

                var avatarIdForPath = SafeSegment(manifest.Value<string>("avatarId"));
                var installRootForPath = Path.Combine(Application.persistentDataPath, "AvatarPackages", avatarIdForPath);
                Directory.CreateDirectory(installRootForPath);
                extractedPath = Path.Combine(installRootForPath, ".partial-" + Guid.NewGuid().ToString("N"));
                try
                {
                    (payloadHash, payloadSize) = await ExtractAndHashAsync(entry, extractedPath, cancellationToken);
                }
                catch
                {
                    TryDelete(extractedPath);
                    throw;
                }

                if (!string.Equals(payloadHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(extractedPath);
                    throw new InvalidDataException("Avatar package payload SHA-256 does not match manifest.json.");
                }
                if (expectedSize != payloadSize)
                {
                    TryDelete(extractedPath);
                    throw new InvalidDataException("Avatar package payload size does not match manifest.json.");
                }
            }

            var avatarId = SafeSegment(manifest.Value<string>("avatarId"));
            var installRoot = Path.Combine(Application.persistentDataPath, "AvatarPackages", avatarId);
            Directory.CreateDirectory(installRoot);
            var bundlePath = Path.Combine(installRoot, payloadHash + ".bundle");
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (YuiAvatarBundleLease.IsInUse(bundlePath)) TryDelete(extractedPath);
                else if (File.Exists(bundlePath)) File.Replace(extractedPath, bundlePath, null);
                else File.Move(extractedPath, bundlePath);
                extractedPath = null;
            }
            finally
            {
                TryDelete(extractedPath);
            }

            var bundle = await YuiAvatarBundleLease.AcquireAsync(bundlePath);
            GameObject loadedRoot = null;
            GameObject inactiveStaging = null;
            var ownershipTransferred = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var prefabAddress = manifest.Value<string>("prefabAddress") ?? "avatar/prefab";
                var prefabRequest = bundle.LoadAssetAsync<GameObject>(prefabAddress);
                await AwaitRequest(prefabRequest);
                cancellationToken.ThrowIfCancellationRequested();
                var prefab = prefabRequest.asset as GameObject
                    ?? throw new InvalidDataException($"Avatar prefab is missing from the bundle: {prefabAddress}");
                ValidateRuntimePrefab(prefab);
                // Legacy packages may contain AudioSources. Instantiate under an inactive
                // parent so playOnAwake cannot run before these unused components are removed.
                inactiveStaging = new GameObject("Yui Avatar Import Staging");
                inactiveStaging.SetActive(false);
                var root = UnityEngine.Object.Instantiate(prefab, inactiveStaging.transform, false);
                loadedRoot = root;
                root.SetActive(false);
                foreach (var audio in root.GetComponentsInChildren<AudioSource>(true))
                {
                    audio.playOnAwake = false; audio.enabled = false;
                    DestroyRuntimeObject(audio);
                }
                root.transform.SetParent(parent, false);
                root.name = string.IsNullOrWhiteSpace(manifest.Value<string>("displayName"))
                    ? "Yui Imported Unity Avatar"
                    : manifest.Value<string>("displayName");
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                var metadata = root.AddComponent<YuiAvatarPackageMetadata>();
                metadata.Configure(
                    manifest.Value<string>("avatarId"),
                    manifest.Value<string>("displayName"),
                    packagePath,
                    ParseVisemes(manifest));
                var springs = manifest["diagnostics"]?["physBones"]?.Children<JObject>().ToArray();
                if (springs != null && springs.Length > 0)
                {
                    var motion = root.AddComponent<YuiAvatarSpringMotion>();
                    motion.InitializeBodyExclusions(root.GetComponentInChildren<Animator>(true));
                    foreach (var spring in springs.Take(128))
                    {
                        var bonePath = spring.Value<string>("rootPath");
                        // Do not interpret an empty/missing path as permission to simulate the entire avatar.
                        if (string.IsNullOrWhiteSpace(bonePath)) continue;
                        var bone = root.transform.Find(bonePath);
                        var excluded = new HashSet<Transform>();
                        foreach (var ignored in spring["ignorePaths"]?.Values<string>() ?? Enumerable.Empty<string>())
                        {
                            if (!string.IsNullOrWhiteSpace(ignored)) { var ignoredBone = root.transform.Find(ignored); if (ignoredBone != null) excluded.Add(ignoredBone); }
                        }
                        motion.AddChain(bone, spring.Value<float?>("pull") ?? .3f, spring.Value<float?>("gravity") ?? 0, excluded);
                    }
                }
                root.AddComponent<YuiAvatarBundleLease>().Own(bundlePath);
                ownershipTransferred = true;
                return new YuiAvatarPackageLoadResult
                {
                    Root = root,
                    AvatarId = manifest.Value<string>("avatarId"),
                    DisplayName = manifest.Value<string>("displayName"),
                };
            }
            finally
            {
                if (inactiveStaging != null) DestroyRuntimeObject(inactiveStaging);
                if (!ownershipTransferred) { if (loadedRoot != null) DestroyRuntimeObject(loadedRoot); YuiAvatarBundleLease.Release(bundlePath); }
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        public static void ValidateRuntimePrefab(GameObject prefab)
        {
            if (prefab == null) throw new InvalidDataException("Avatar prefab is missing.");
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component is Transform || component is Animator || component is SkinnedMeshRenderer
                    || component is MeshRenderer || component is MeshFilter || component is LODGroup
                    || component is AudioSource) continue; // inert legacy data; stripped before activation
                throw new InvalidDataException("このアバターには対応外の実行コンポーネントがあります。最新のYui Avatar Bridgeで再書出ししてください: "
                    + (component == null ? "Missing script" : component.GetType().Name));
            }
        }

        public static void ValidateArchive(ZipArchive archive)
        {
            if (archive == null) throw new ArgumentNullException(nameof(archive));
            if (archive.Entries.Count > MaximumEntries) throw new InvalidDataException("Avatar package contains too many entries.");
            long expandedBytes = 0;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                var normalized = (entry.FullName ?? string.Empty).Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(normalized)
                    || normalized.StartsWith("/", StringComparison.Ordinal)
                    || normalized.Split('/').Any(segment => segment == "." || segment == "..")
                    || normalized.Contains(":")
                    || ForbiddenExtensions.Contains(Path.GetExtension(normalized)))
                {
                    throw new InvalidDataException($"Avatar package contains an unsafe entry: {entry.FullName}");
                }
                if (!names.Add(normalized))
                    throw new InvalidDataException("Avatar package contains duplicate entry names.");
                if (string.Equals(normalized, "manifest.json", StringComparison.OrdinalIgnoreCase)
                    && entry.Length > MaximumManifestBytes)
                    throw new InvalidDataException("Avatar package manifest exceeds the safety limit.");
                expandedBytes = checked(expandedBytes + entry.Length);
                if (expandedBytes > MaximumExpandedBytes) throw new InvalidDataException("Avatar package expands beyond the safety limit.");
            }
        }

        public static void ValidateManifest(JObject manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (!string.Equals(manifest.Value<string>("format"), Format, StringComparison.Ordinal))
            {
                throw new InvalidDataException("ZIP is not a supported Unity avatar package.");
            }
            if (manifest.Value<int?>("schemaVersion") != CurrentSchemaVersion)
            {
                throw new InvalidDataException("Avatar package schema version is not supported.");
            }
            if (string.IsNullOrWhiteSpace(manifest.Value<string>("avatarId")))
            {
                throw new InvalidDataException("Avatar package avatarId is missing.");
            }
        }

        public static JObject SelectPayload(JObject manifest, string platform = null)
        {
            platform = platform ?? CurrentPlatform();
            return manifest["payloads"]?.Children<JObject>()
                .FirstOrDefault(item => string.Equals(item.Value<string>("platform"), platform, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"このZIPには {platform} 用アバターがありません（含まれるOS: {string.Join(", ", manifest["payloads"]?.Children<JObject>().Select(item => item.Value<string>("platform")) ?? Enumerable.Empty<string>())}）。元のUnityで {platform} を選んで再書出しし、ZIPを端末へコピーしてください。");
        }

        public static string CurrentPlatform()
        {
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
            return "windows";
#elif UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
            return "macos";
#elif UNITY_ANDROID
            return "android";
#elif UNITY_IOS
            return "ios";
#else
            return "unsupported";
#endif
        }

        private static YuiAvatarPackageMetadata.Viseme[] ParseVisemes(JObject manifest)
        {
            return manifest["diagnostics"]?["visemes"]?.Children<JObject>()
                .Where(item => item.Value<bool?>("found") == true)
                .Select(item => new YuiAvatarPackageMetadata.Viseme
                {
                    Vowel = item.Value<string>("vowel"),
                    RendererPath = item.Value<string>("rendererPath"),
                    BlendShape = item.Value<string>("blendShape"),
                })
                .ToArray() ?? Array.Empty<YuiAvatarPackageMetadata.Viseme>();
        }

        private static string SafeSegment(string value)
        {
            var safe = new string((value ?? string.Empty).Where(character => char.IsLetterOrDigit(character) || character == '-' || character == '_').ToArray());
            return string.IsNullOrWhiteSpace(safe) ? "avatar" : safe;
        }

        private static async Task<(string Hash, long Size)> ExtractAndHashAsync(ZipArchiveEntry entry, string outputPath, CancellationToken cancellationToken)
        {
            using var sha = SHA256.Create();
            using var input = entry.Open();
            using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true);
            var buffer = new byte[1024 * 1024];
            long size = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer, 0, read, cancellationToken);
                sha.TransformBlock(buffer, 0, read, null, 0);
                size = checked(size + read);
                if (size > MaximumExpandedBytes)
                {
                    throw new InvalidDataException("Avatar package payload exceeds the safety limit.");
                }
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return (string.Concat(sha.Hash.Select(value => value.ToString("x2"))), size);
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // Best-effort cleanup; a future import can remove an abandoned partial file.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup; do not hide the original import result.
            }
        }

        private static Task AwaitRequest(AsyncOperation request)
        {
            var completion = new TaskCompletionSource<bool>();
            if (request.isDone) completion.SetResult(true);
            else request.completed += _ => completion.TrySetResult(true);
            return completion.Task;
        }
    }
}
