using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YuiPhysicalAI.Core
{
    public static class YuiExternalDataConsent
    {
        public const string OpenAiDestination = "https://api.openai.com";
        private const string Ledger = "Yui.Privacy.ExternalDataConsent.v1";
        public static string Destination(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo))
                throw new ArgumentException("Enter a valid HTTP or HTTPS server URL without embedded credentials.");
            return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        }
        private static string Id(string destination)
        {
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(Destination(destination))));
        }
        public static bool HasPermission(string destination) =>
            PlayerPrefs.GetString(Ledger, "").Contains("|" + Id(destination) + "|");
        public static void Grant(string destination)
        {
            if (HasPermission(destination)) return;
            PlayerPrefs.SetString(Ledger, PlayerPrefs.GetString(Ledger, "") + "|" + Id(destination) + "|");
            PlayerPrefs.Save();
        }
        public static void RevokeAll() { PlayerPrefs.DeleteKey(Ledger); PlayerPrefs.Save(); }

        // Used at the actual transport boundary, including fallback paths.
        public static async Task<T> SendAsync<T>(Func<CancellationToken, Task> permission,
            Func<Task<T>> send, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            await permission(token);
            token.ThrowIfCancellationRequested();
            return await send();
        }
    }
}
