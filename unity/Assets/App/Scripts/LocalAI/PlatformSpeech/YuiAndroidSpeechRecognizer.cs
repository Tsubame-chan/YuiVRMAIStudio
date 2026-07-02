using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

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

        private static TaskCompletionSource<YuiPlatformSpeechTranscriptionResult> pending;
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
            if (pending != null)
            {
                return YuiPlatformSpeechTranscriptionResult.Error("busy", "Speech recognition is already running.");
            }

            if (!await EnsureMicrophonePermissionAsync(cancellationToken))
            {
                return YuiPlatformSpeechTranscriptionResult.Error("permission_denied", "Microphone permission was not granted.");
            }

            EnsureBridge();
            pending = new TaskCompletionSource<YuiPlatformSpeechTranscriptionResult>();
            var registration = cancellationToken.Register(Cancel);
            try
            {
                using (var bridgeClass = new AndroidJavaClass(JavaClassName))
                {
                    bridgeClass.CallStatic("start", BridgeObjectName, string.IsNullOrWhiteSpace(languageCode) ? "ja-JP" : languageCode);
                }

                return await pending.Task;
            }
            catch (Exception ex)
            {
                var completion = pending;
                pending = null;
                completion?.TrySetResult(YuiPlatformSpeechTranscriptionResult.Error("bridge_error", ex.Message));
                return YuiPlatformSpeechTranscriptionResult.Error("bridge_error", ex.Message);
            }
            finally
            {
                registration.Dispose();
            }
#else
            return await Task.FromResult(YuiPlatformSpeechTranscriptionResult.Error("platform_unsupported", "Android speech recognition is not available."));
#endif
        }

        public static void Cancel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var bridgeClass = new AndroidJavaClass(JavaClassName))
                {
                    bridgeClass.CallStatic("cancel");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Android speech recognizer cancel failed: {ex.Message}");
            }
#endif
        }

        public void OnAndroidSpeechResult(string message)
        {
            var completion = pending;
            pending = null;
            if (completion == null)
            {
                return;
            }

            if (string.Equals(message, Cancelled, StringComparison.Ordinal))
            {
                completion.TrySetResult(YuiPlatformSpeechTranscriptionResult.Error("cancelled", "Speech recognition was cancelled."));
                return;
            }

            if (!string.IsNullOrEmpty(message) && message.StartsWith(ErrorPrefix, StringComparison.Ordinal))
            {
                completion.TrySetResult(YuiPlatformSpeechTranscriptionResult.Error("recognizer_error", message.Substring(ErrorPrefix.Length)));
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                completion.TrySetResult(YuiPlatformSpeechTranscriptionResult.Error("empty_transcript", "Platform STT returned an empty transcript."));
                return;
            }

            completion.TrySetResult(new YuiPlatformSpeechTranscriptionResult
            {
                Ok = true,
                Text = message,
                Confidence = null
            });
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
            permissionCallbacks = new PermissionCallbacks();
            permissionCallbacks.PermissionGranted += _ =>
            {
                permissionCallbacks = null;
                completion.TrySetResult(true);
            };
            permissionCallbacks.PermissionDenied += _ =>
            {
                permissionCallbacks = null;
                completion.TrySetResult(false);
            };
            permissionCallbacks.PermissionDeniedAndDontAskAgain += _ =>
            {
                permissionCallbacks = null;
                completion.TrySetResult(false);
            };

            var registration = cancellationToken.Register(() =>
            {
                permissionCallbacks = null;
                completion.TrySetCanceled();
            });
            completion.Task.ContinueWith(_ => registration.Dispose());
            Permission.RequestUserPermission(Permission.Microphone, permissionCallbacks);
            return completion.Task;
        }
#endif
    }
}
