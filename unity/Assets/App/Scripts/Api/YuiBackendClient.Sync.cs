using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace YuiPhysicalAI.Api
{
    public sealed partial class YuiBackendClient
    {
        // Authorization belongs only to the paired server's sync routes.
        public async Task<JObject> SyncAsync(string path, JObject body, string token, CancellationToken cancellation = default)
        {
            if (!path.StartsWith("/sync/", StringComparison.Ordinal)) throw new ArgumentException("Sync route required.");
            return await PairedJsonAsync(path, body, token, cancellation);
        }

        // Protocol 2 remains opt-in. Pairing credentials are used only on the
        // same server's explicitly scoped Companion endpoints.
        public Task<JObject> CompanionAsync(string path, JObject body, string token, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("/companion/v2/", StringComparison.Ordinal)
                || path.Contains("..") || path.Contains("\\") || path.Contains("//"))
                throw new ArgumentException("Companion route required.");
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Paired device token required.");
            return PairedJsonAsync(path, body, token, cancellation);
        }

        public async Task<ChatResponse> SharedChatAsync(ChatRequest body, string token,
            CancellationToken cancellation = default)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.SharedCharacterId))
                throw new ArgumentException("共有キャラクターを確認できません。同期設定を確認してください。");
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("共有キャラクターの端末登録がありません。PCの管理画面でこの端末を再登録してください。");
            var result = await PairedJsonAsync("/chat", JObject.FromObject(body), token, cancellation);
            return result.ToObject<ChatResponse>();
        }

        private async Task<JObject> PairedJsonAsync(string path, JObject body, string token,
            CancellationToken cancellation, string methodOverride = null)
        {
            var method = methodOverride ?? (body == null ? "GET" : "POST");
            var json = body?.ToString(Formatting.None);
            using var request = new UnityWebRequest(ToAbsoluteUrl(path), method);
            request.timeout = 60; request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
            if (json != null) { request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)); request.SetRequestHeader("Content-Type", "application/json"); }
            try { await SendAsync(request, cancellation); return JObject.Parse(request.downloadHandler.text); }
            catch (YuiBackendException ex) when (ShouldTryHttpClientFallback(ex))
            {
                using var message = new HttpRequestMessage(new HttpMethod(method), ToAbsoluteUrl(path));
                if (json != null) message.Content = new StringContent(json, Encoding.UTF8, "application/json");
                if (!string.IsNullOrEmpty(token)) message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(60000);
                using var response = await FallbackHttpClient.SendAsync(message, timeout.Token);
                if (!response.IsSuccessStatusCode) throw new YuiBackendException((int)response.StatusCode, "Sync failed", string.Empty, ToAbsoluteUrl(path));
                return JObject.Parse(await response.Content.ReadAsStringAsync());
            }
        }
    }
}
