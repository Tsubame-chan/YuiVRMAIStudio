using System;
namespace YuiPhysicalAI.Core
{
    public static class YuiBackendAddressPolicy
    {
        public const string DesktopDefault = "http://127.0.0.1:8000";
        public static string Resolve(string saved, string sceneDefault, bool mobile)
        {
            var value = (saved ?? sceneDefault ?? "").Trim();
            if (mobile && (string.IsNullOrWhiteSpace(value) || IsLoopback(value))) return "";
            return value;
        }
        public static bool IsLoopback(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsLoopback;
    }
}
