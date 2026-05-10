using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace YuiPhysicalAI.Api
{
    public sealed class YuiBackendClient
    {
        private readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public YuiBackendClient(string baseUrl)
        {
            BaseUrl = NormalizeBaseUrl(baseUrl);
        }

        public string BaseUrl { get; }

        public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            return GetJsonAsync<HealthResponse>("/health", cancellationToken);
        }

        public Task<ConfigResponse> GetConfigAsync(CancellationToken cancellationToken = default)
        {
            return GetJsonAsync<ConfigResponse>("/config", cancellationToken);
        }

        public Task<RealtimeStatusResponse> GetRealtimeStatusAsync(CancellationToken cancellationToken = default)
        {
            return GetJsonAsync<RealtimeStatusResponse>("/realtime/status", cancellationToken);
        }

        public Task<RealtimeProbeResponse> ProbeRealtimeAsync(
            RealtimeProbeRequest request,
            CancellationToken cancellationToken = default)
        {
            return PostJsonAsync<RealtimeProbeRequest, RealtimeProbeResponse>("/realtime/probe", request, cancellationToken);
        }

        public Task<ChatResponse> SendChatAsync(
            ChatRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureRequestId(request);
            return PostJsonAsync<ChatRequest, ChatResponse>("/chat", request, cancellationToken);
        }

        public Task<TtsResponse> SynthesizeSpeechAsync(
            TtsRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.RequestId))
            {
                request.RequestId = Guid.NewGuid().ToString("N");
            }

            return PostJsonAsync<TtsRequest, TtsResponse>("/tts", request, cancellationToken);
        }

        public async Task<SttResponse> TranscribeAudioAsync(
            byte[] wavBytes,
            string filename = "recording.wav",
            int? durationMs = null,
            CancellationToken cancellationToken = default)
        {
            if (wavBytes == null || wavBytes.Length == 0)
            {
                throw new ArgumentException("Audio bytes are required.", nameof(wavBytes));
            }

            var form = new WWWForm();
            form.AddBinaryData("audio", wavBytes, filename, "audio/wav");
            if (durationMs.HasValue)
            {
                form.AddField("duration_ms", durationMs.Value);
            }

            using var request = UnityWebRequest.Post(ToAbsoluteUrl("/stt"), form);
            request.timeout = 60;
            request.SetRequestHeader("Accept", "application/json");
            await SendAsync(request, cancellationToken);
            return Deserialize<SttResponse>(request.downloadHandler.text);
        }

        public async Task<VisionResponse> AnalyzeImageAsync(
            byte[] imageBytes,
            string filename = "screen.jpg",
            string promptType = "screen",
            string mimeType = "image/jpeg",
            CancellationToken cancellationToken = default)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new ArgumentException("Image bytes are required.", nameof(imageBytes));
            }

            var form = new WWWForm();
            form.AddBinaryData("image", imageBytes, filename, mimeType);
            form.AddField("prompt_type", promptType);

            using var request = UnityWebRequest.Post(ToAbsoluteUrl("/vision"), form);
            request.timeout = 60;
            request.SetRequestHeader("Accept", "application/json");
            await SendAsync(request, cancellationToken);
            return Deserialize<VisionResponse>(request.downloadHandler.text);
        }

        public async Task<RealtimeAudioResponse> SendRealtimeAudioAsync(
            byte[] wavBytes,
            string mode = "voice",
            string instructions = "",
            string filename = "realtime_recording.wav",
            CancellationToken cancellationToken = default)
        {
            if (wavBytes == null || wavBytes.Length == 0)
            {
                throw new ArgumentException("Audio bytes are required.", nameof(wavBytes));
            }

            var form = new WWWForm();
            form.AddBinaryData("audio", wavBytes, filename, "audio/wav");
            form.AddField("mode", mode ?? "voice");
            form.AddField("instructions", instructions ?? string.Empty);

            using var request = UnityWebRequest.Post(ToAbsoluteUrl("/realtime/audio"), form);
            request.timeout = 90;
            request.SetRequestHeader("Accept", "application/json");
            await SendAsync(request, cancellationToken);
            return Deserialize<RealtimeAudioResponse>(request.downloadHandler.text);
        }

        public Task<MemoryItem> SaveMemoryAsync(
            MemorySaveRequest request,
            CancellationToken cancellationToken = default)
        {
            return PostJsonAsync<MemorySaveRequest, MemoryItem>("/memory/save", request, cancellationToken);
        }

        public Task<MemorySearchResponse> SearchMemoryAsync(
            MemorySearchRequest request,
            CancellationToken cancellationToken = default)
        {
            return PostJsonAsync<MemorySearchRequest, MemorySearchResponse>("/memory/search", request, cancellationToken);
        }

        public Task<UsageResponse> GetUsageAsync(
            string userId = null,
            CancellationToken cancellationToken = default)
        {
            var path = string.IsNullOrWhiteSpace(userId)
                ? "/usage"
                : "/usage?user_id=" + UnityWebRequest.EscapeURL(userId);
            return GetJsonAsync<UsageResponse>(path, cancellationToken);
        }

        public Task<RecentConversationsResponse> GetRecentConversationsAsync(
            string userId = "local_user",
            int limit = 20,
            CancellationToken cancellationToken = default)
        {
            var path = "/conversations/recent?user_id="
                + UnityWebRequest.EscapeURL(userId)
                + "&limit="
                + limit;
            return GetJsonAsync<RecentConversationsResponse>(path, cancellationToken);
        }

        public Task<ClearConversationsResponse> ClearConversationsAsync(
            string userId = "local_user",
            CancellationToken cancellationToken = default)
        {
            var path = "/conversations?user_id=" + UnityWebRequest.EscapeURL(userId);
            return DeleteJsonAsync<ClearConversationsResponse>(path, cancellationToken);
        }

        public async Task<AudioClip> DownloadAudioClipAsync(
            string audioUrl,
            CancellationToken cancellationToken = default)
        {
            var url = ToAbsoluteUrl(audioUrl);
            using var request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV);
            request.timeout = 30;
            await SendAsync(request, cancellationToken);
            return CopyAudioClip(DownloadHandlerAudioClip.GetContent(request), "YuiBackendAudio");
        }

        public async Task<AudioClip> SynthesizeSpeechClipAsync(
            TtsRequest body,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(body.RequestId))
            {
                body.RequestId = Guid.NewGuid().ToString("N");
            }

            var url = ToAbsoluteUrl("/tts/audio");
            var json = JsonConvert.SerializeObject(body, jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.timeout = 60;
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.WAV);
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            request.SetRequestHeader("Accept", "audio/wav");
            await SendAsync(request, cancellationToken);
            return CopyAudioClip(DownloadHandlerAudioClip.GetContent(request), "YuiBackendAudio");
        }

        public async Task<ChatSpeechResult> SendChatWithSpeechAsync(
            string message,
            string userId = "local_user",
            int speakerId = 14,
            float? speedScale = 1.0f,
            CancellationToken cancellationToken = default,
            RequestContext context = null)
        {
            var chatRequestId = Guid.NewGuid().ToString("N");
            var chat = await SendChatAsync(
                new ChatRequest
                {
                    RequestId = chatRequestId,
                    UserId = userId,
                    Message = message,
                    Context = context ?? new RequestContext()
                },
                cancellationToken);

            if (!chat.ShouldTts)
            {
                return new ChatSpeechResult { Chat = chat };
            }

            var tts = await SynthesizeSpeechAsync(
                new TtsRequest
                {
                    RequestId = chatRequestId + "-tts",
                    Text = chat.Text,
                    SpeakerId = speakerId,
                    SpeedScale = speedScale
                },
                cancellationToken);

            var clip = await DownloadAudioClipAsync(tts.AudioUrl, cancellationToken);
            return new ChatSpeechResult
            {
                Chat = chat,
                Tts = tts,
                AudioClip = clip
            };
        }

        private async Task<TResponse> GetJsonAsync<TResponse>(
            string path,
            CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Get(ToAbsoluteUrl(path));
            request.timeout = 10;
            request.SetRequestHeader("Accept", "application/json");
            await SendAsync(request, cancellationToken);
            return Deserialize<TResponse>(request.downloadHandler.text);
        }

        private async Task<TResponse> PostJsonAsync<TRequest, TResponse>(
            string path,
            TRequest body,
            CancellationToken cancellationToken)
        {
            var json = JsonConvert.SerializeObject(body, jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(ToAbsoluteUrl(path), UnityWebRequest.kHttpVerbPOST);
            request.timeout = 60;
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            request.SetRequestHeader("Accept", "application/json");

            await SendAsync(request, cancellationToken);
            return Deserialize<TResponse>(request.downloadHandler.text);
        }

        private async Task<TResponse> DeleteJsonAsync<TResponse>(
            string path,
            CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Delete(ToAbsoluteUrl(path));
            request.timeout = 30;
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "application/json");
            await SendAsync(request, cancellationToken);
            return Deserialize<TResponse>(request.downloadHandler.text);
        }

        private static async Task SendAsync(
            UnityWebRequest request,
            CancellationToken cancellationToken)
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (request.result != UnityWebRequest.Result.Success)
            {
                var body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                throw new YuiBackendException(
                    request.responseCode,
                    request.error,
                    body,
                    request.url);
            }
        }

        private T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, jsonSettings);
        }

        private string ToAbsoluteUrl(string pathOrUrl)
        {
            if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return pathOrUrl;
            }

            return BaseUrl + "/" + pathOrUrl.TrimStart('/');
        }

        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Backend base URL is required.", nameof(baseUrl));
            }

            return baseUrl.TrimEnd('/');
        }

        private static void EnsureRequestId(ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RequestId))
            {
                request.RequestId = Guid.NewGuid().ToString("N");
            }
        }

        private static AudioClip CopyAudioClip(AudioClip source, string fallbackName)
        {
            if (source == null)
            {
                return null;
            }

            var samples = new float[source.samples * source.channels];
            source.GetData(samples, 0);
            var copy = AudioClip.Create(
                string.IsNullOrWhiteSpace(source.name) ? fallbackName : source.name + "_owned",
                source.samples,
                source.channels,
                source.frequency,
                false);
            copy.SetData(samples, 0);
            return copy;
        }

        public static AudioClip Pcm16Base64ToAudioClip(
            string audioBase64,
            int sampleRate,
            string clipName = "YuiRealtimeAudio")
        {
            if (string.IsNullOrWhiteSpace(audioBase64))
            {
                return null;
            }

            var pcm = Convert.FromBase64String(audioBase64);
            var sampleCount = pcm.Length / 2;
            if (sampleCount <= 0)
            {
                return null;
            }

            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var value = BitConverter.ToInt16(pcm, i * 2);
                samples[i] = Mathf.Clamp(value / 32768f, -1f, 1f);
            }

            var clip = AudioClip.Create(
                string.IsNullOrWhiteSpace(clipName) ? "YuiRealtimeAudio" : clipName,
                sampleCount,
                1,
                sampleRate > 0 ? sampleRate : 24000,
                false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
