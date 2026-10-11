using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace YuiPhysicalAI.LocalAI
{
    public sealed class YuiAndroidSpeechRecognizer : MonoBehaviour
    {
        private const string BridgeObjectName = "YuiAndroidSpeechRecognizerBridge";
        private const string JavaClassName = "jp.tsubamechan.yuivrm.localai.YuiAndroidSpeechRecognizer";
        private const string Cancelled = "__YUI_CANCELLED__";
        private const string ErrorPrefix = "__YUI_ERROR__:";

        private sealed class RecognitionRequest
        {
            public readonly string Id = Guid.NewGuid().ToString("N");
            public readonly TaskCompletionSource<YuiPlatformSpeechTranscriptionResult> Completion =
                new TaskCompletionSource<YuiPlatformSpeechTranscriptionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            public Action CancelNative;
        }
        private static readonly object RequestLock = new object();
        private static RecognitionRequest pending;
        private static YuiAndroidSpeechRecognizer bridge;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static PermissionCallbacks permissionCallbacks;
#endif

        public static bool IsSupported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using (var bridgeClass = new AndroidJavaClass(JavaClassName))
                    {
                        return bridgeClass.CallStatic<bool>("isAvailable");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Android speech recognizer availability check failed: {ex.Message}");
                    return false;
                }
#else
                return false;
#endif
            }
        }

        public static async Task<YuiPlatformSpeechTranscriptionResult> TranscribeLiveAsync(
            string languageCode = "ja-JP",
            CancellationToken cancellationToken = default)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureBridge();
            return await RunRecognitionAsync(EnsureMicrophonePermissionAsync, id =>
            {
                using var bridgeClass = new AndroidJavaClass(JavaClassName);
                bridgeClass.CallStatic("start", BridgeObjectName, id,
                    string.IsNullOrWhiteSpace(languageCode) ? "ja-JP" : languageCode);
            }, id =>
            {
                using var bridgeClass = new AndroidJavaClass(JavaClassName);
                bridgeClass.CallStatic("cancel", id);
            }, cancellationToken);
#else
            return await Task.FromResult(YuiPlatformSpeechTranscriptionResult.Error("platform_unsupported", "Android speech recognition is not available."));
#endif
        }

        // Kept platform independent so cancellation and late native callbacks can be tested.
        public static async Task<YuiPlatformSpeechTranscriptionResult> RunRecognitionAsync(
            Func<CancellationToken, Task<bool>> requestPermission, Action<string> startNative,
            Action<string> cancelNative, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = new RecognitionRequest();
            var context = SynchronizationContext.Current;
            request.CancelNative = () =>
            {
                void Stop(object _) { try { cancelNative(request.Id); }
                    catch (Exception ex) { Debug.LogWarning("Android speech cancel failed: " + ex.Message); } }
                // JNI/Unity work belongs to the caller's Unity thread, not a token worker.
                if (context != null) context.Post(Stop, null); else Stop(null);
            };
            lock (RequestLock)
            {
                if (pending != null)
                    return YuiPlatformSpeechTranscriptionResult.Error("busy", "Speech recognition is already running.");
                pending = request;
            }
            using var registration = cancellationToken.Register(() => CancelRequest(request, cancellationToken));
            try
            {
                var permission = requestPermission(cancellationToken);
                // Stop also settles a request whose OS permission dialog has not returned.
                var ready = await Task.WhenAny(permission, request.Completion.Task);
                if (ready == request.Completion.Task)
                {
                    _ = permission.ContinueWith(t => { _ = t.Exception; },
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    return await request.Completion.Task;
                }
                if (!await permission)
                    return YuiPlatformSpeechTranscriptionResult.Error("permission_denied", "Microphone permission was not granted.");
                cancellationToken.ThrowIfCancellationRequested();
                lock (RequestLock)
                {
                    // Serialize start with cancel; a queued cancel cannot precede a later start.
                    if (ReferenceEquals(pending, request)) startNative(request.Id);
                }
                return await request.Completion.Task;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                request.CancelNative();
                return YuiPlatformSpeechTranscriptionResult.Error("bridge_error", ex.Message);
            }
            finally
            {
                lock (RequestLock) { if (ReferenceEquals(pending, request)) pending = null; }
            }
        }

        public static void Cancel()
        {
            RecognitionRequest request;
            lock (RequestLock) request = pending;
            CancelRequest(request, default);
        }

        private static void CancelRequest(RecognitionRequest request, CancellationToken token)
        {
            lock (RequestLock)
            {
                if (request == null || !ReferenceEquals(pending, request)) return;
                pending = null;
            }
            request.CancelNative();
            if (token.IsCancellationRequested) request.Completion.TrySetCanceled(token);
            else request.Completion.TrySetResult(YuiPlatformSpeechTranscriptionResult.Error("cancelled", "Speech recognition was cancelled."));
        }

        public void OnAndroidSpeechResult(string message) => CompleteNativeResult(message);

        public static void CompleteNativeResult(string message)
        {
            string id, result;
            try
            {
                var payload = JObject.Parse(message ?? "");
                id = (string)payload["request_id"];
                result = (string)payload["message"];
            }
            catch { return; }
            RecognitionRequest request;
            lock (RequestLock)
            {
                request = pending;
                if (request == null || request.Id != id) return;
                pending = null;
            }
            var completion = request.Completion;
            if (string.Equals(result, Cancelled, StringComparison.Ordinal))
                completion.TrySetResult(YuiPlatformSpeechTranscriptionResult.Error("cancelled", "Speech recognition was cancelled."));
            else if (result != null && result.StartsWith(ErrorPrefix, StringComparison.Ordinal))
                completion.TrySetResult(YuiPlatformSpeechTranscriptionResult.Error("recognizer_error", result.Substring(ErrorPrefix.Length)));
            else if (string.IsNullOrWhiteSpace(result))
                completion.TrySetResult(YuiPlatformSpeechTranscriptionResult.Error("empty_transcript", "Platform STT returned an empty transcript."));
            else completion.TrySetResult(new YuiPlatformSpeechTranscriptionResult { Ok = true, Text = result, Confidence = null });
        }

        private static void EnsureBridge()
        {
            if (bridge != null)
            {
                return;
            }

            var existing = GameObject.Find(BridgeObjectName);
            var owner = existing != null ? existing : new GameObject(BridgeObjectName);
            DontDestroyOnLoad(owner);
            bridge = owner.GetComponent<YuiAndroidSpeechRecognizer>();
            if (bridge == null)
            {
                bridge = owner.AddComponent<YuiAndroidSpeechRecognizer>();
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static Task<bool> EnsureMicrophonePermissionAsync(CancellationToken cancellationToken)
        {
            if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                return Task.FromResult(true);
            }

            var completion = new TaskCompletionSource<bool>();
            cancellationToken.ThrowIfCancellationRequested();
            var callbacks = new PermissionCallbacks();
            permissionCallbacks = callbacks;
            callbacks.PermissionGranted += _ =>
            {
                if (ReferenceEquals(permissionCallbacks, callbacks)) permissionCallbacks = null;
                completion.TrySetResult(true);
            };
            callbacks.PermissionDenied += _ =>
            {
                if (ReferenceEquals(permissionCallbacks, callbacks)) permissionCallbacks = null;
                completion.TrySetResult(false);
            };
            callbacks.PermissionDeniedAndDontAskAgain += _ =>
            {
                if (ReferenceEquals(permissionCallbacks, callbacks)) permissionCallbacks = null;
                completion.TrySetResult(false);
            };

            var registration = cancellationToken.Register(() =>
            {
                if (ReferenceEquals(permissionCallbacks, callbacks)) permissionCallbacks = null;
                completion.TrySetCanceled();
            });
            completion.Task.ContinueWith(_ => registration.Dispose());
            Permission.RequestUserPermission(Permission.Microphone, callbacks);
            return completion.Task;
        }
#endif
    }
}
