using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace YuiPhysicalAI.Core
{
    public static class YuiApiKeyStore
    {
        private static string cached;
        private static bool unreadCredential;
        public static bool IsRecovering { get; private set; }
        public static string LastError { get; private set; }
#if UNITY_IOS && !UNITY_EDITOR
        private const string NativeLibrary = "__Internal";
#else
        private const string NativeLibrary = "YuiCredentialStore";
#endif
        [DllImport(NativeLibrary)] private static extern int YuiCredentialRead(string service, out IntPtr value);
        [DllImport(NativeLibrary)] private static extern int YuiCredentialReadInteractive(string service, out IntPtr value);
        [DllImport(NativeLibrary)] private static extern int YuiCredentialWrite(string service, string value);
        [DllImport(NativeLibrary)] private static extern void YuiCredentialFree(IntPtr value);

        public static string Read()
        {
            if (cached != null) return cached;
            var legacy = PlayerPrefs.GetString(YuiPrefsKeys.OpenAiApiKey, "");
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            try
            {
                var status = YuiCredentialRead(Application.identifier, out var pointer);
                if (status != 0) throw new InvalidOperationException("Keychain status " + status);
                try { cached = pointer == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(pointer); }
                finally { if (pointer != IntPtr.Zero) YuiCredentialFree(pointer); }
                if (string.IsNullOrEmpty(cached) && !string.IsNullOrEmpty(legacy))
                {
                    cached = legacy;
                    Write(legacy); // Keep the old copy only if migration fails.
                }
                else if (PlayerPrefs.HasKey(YuiPrefsKeys.OpenAiApiKey))
                {
                    PlayerPrefs.DeleteKey(YuiPrefsKeys.OpenAiApiKey); PlayerPrefs.Save();
                }
            }
            catch (Exception)
            {
                cached = legacy;
                unreadCredential = true;
                LastError = "Could not access the system key store.";
                Debug.LogWarning(LastError);
            }
#else
            cached = legacy;
#endif
            return cached;
        }

        public static bool Write(string value)
        {
            // An empty field after a failed read is not an instruction to erase
            // the saved credential. Recover it before allowing an empty write.
            if (IsRecovering || (unreadCredential && string.IsNullOrWhiteSpace(value))) return false;
            cached = (value ?? "").Trim();
            LastError = null;
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            try
            {
                if (YuiCredentialWrite(Application.identifier, cached) != 0)
                    throw new InvalidOperationException();
                PlayerPrefs.DeleteKey(YuiPrefsKeys.OpenAiApiKey);
                unreadCredential = false;
            }
            catch (Exception)
            {
                LastError = "API key could not be saved securely. It is available for this session only.";
                Debug.LogWarning(LastError);
                return false;
            }
#else
            // Windows/Android storage migration is a separate platform acceptance gate.
            PlayerPrefs.SetString(YuiPrefsKeys.OpenAiApiKey, cached);
#endif
            PlayerPrefs.Save();
            return true;
        }

        public static async Task<bool> RecoverAsync()
        {
            if (IsRecovering) return false;
            IsRecovering = true;
            try
            {
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
                var service = Application.identifier; // Unity API stays on the main thread.
                var recovered = await Task.Run(() => {
                    var status = YuiCredentialReadInteractive(service, out var pointer);
                    if (status != 0) throw new InvalidOperationException();
                    try { return pointer == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(pointer); }
                    finally { if (pointer != IntPtr.Zero) YuiCredentialFree(pointer); }
                });
                cached = recovered;
                unreadCredential = false;
#endif
                LastError = null;
                return true;
            }
            catch (Exception)
            {
                LastError = "Could not access the system key store.";
                return false;
            }
            finally { IsRecovering = false; }
        }
    }
}
