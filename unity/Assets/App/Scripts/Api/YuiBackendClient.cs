using System;
using System.IO;
using System.Net.Http;
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
        private static readonly HttpClient FallbackHttpClient = new HttpClient();

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

        public Task<ProviderStatusResponse> GetProviderStatusAsync(CancellationToken cancellationToken = default)
        {
            return GetJsonAsync<ProviderStatusResponse>("/providers/status", cancellationToken);
        }

        public Task<WeatherCurrentResponse> GetCurrentWeatherAsync(
            string location,
            CancellationToken cancellationToken = default)
        {
            var path = "/external/weather/current?location=" + UnityWebRequest.EscapeURL(location ?? string.Empty);
            return GetJsonAsync<WeatherCurrentResponse>(path, cancellationToken);
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
            try
            {
                await SendAsync(request, cancellationToken);
                return Deserialize<SttResponse>(request.downloadHandler.text);
            }
            catch (YuiBackendException ex) when (ShouldTryHttpClientFallback(ex))
            {
                Debug.LogWarning($"Yui backend UnityWebRequest STT failed; retrying with HttpClient. {ex.Message}");
                var content = new MultipartFormDataContent();
                var audioContent = new ByteArrayContent(wavBytes);
                audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                content.Add(audioContent, "audio", filename);
                if (durationMs.HasValue)
                {
                    content.Add(new StringContent(durationMs.Value.ToString()), "duration_ms");
                }

                var json = await SendHttpClientAsync(
                    HttpMethod.Post,
                    ToAbsoluteUrl("/stt"),
                    content,
                    60,
                    "application/json",
                    cancellationToken);
                return Deserialize<SttResponse>(json);
            }
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
            try
            {
                await SendAsync(request, cancellationToken);
                return Deserialize<VisionResponse>(request.downloadHandler.text);
            }
            catch (YuiBackendException ex) when (ShouldTryHttpClientFallback(ex))
            {
                Debug.LogWarning($"Yui backend UnityWebRequest vision failed; retrying with HttpClient. {ex.Message}");
                var content = new MultipartFormDataContent();
                var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
                content.Add(imageContent, "image", filename);
                content.Add(new StringContent(promptType ?? "screen"), "prompt_type");

                var json = await SendHttpClientAsync(
                    HttpMethod.Post,
                    ToAbsoluteUrl("/vision"),
                    content,
                    60,
                    "application/json",
                    cancellationToken);
                return Deserialize<VisionResponse>(json);
            }
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
            try
            {
                await SendAsync(request, cancellationToken);
                return Deserialize<RealtimeAudioResponse>(request.downloadHandler.text);
            }
            catch (YuiBackendException ex) when (ShouldTryHttpClientFallback(ex))
            {
                Debug.LogWarning($"Yui backend UnityWebRequest realtime audio failed; retrying with HttpClient. {ex.Message}");
                var content = new MultipartFormDataContent();
                var audioContent = new ByteArrayContent(wavBytes);
                audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                content.Add(audioContent, "audio", filename);
                content.Add(new StringContent(mode ?? "voice"), "mode");
                content.Add(new StringContent(instructions ?? string.Empty), "instructions");

                var json = await SendHttpClientAsync(
                    HttpMethod.Post,
                    ToAbsoluteUrl("/realtime/audio"),
                    content,
                    90,
                    "application/json",
                    cancellationToken);
                return Deserialize<RealtimeAudioResponse>(json);
            }
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
            using var request = UnityWebRequest.Get(url);
            request.timeout = 30;
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "audio/wav");
            try
            {
                await SendAsync(request, cancellationToken);
                return WavBytesToAudioClip(request.downloadHandler.data, "YuiBackendAudio");
            }
            catch (YuiBackendException ex) when (ShouldTryHttpClientFallback(ex))
            {
                Debug.LogWarning($"Yui backend UnityWebRequest audio download failed; retrying with HttpClient. {ex.Message}");
                var bytes = await SendHttpClientBytesAsync(
                    HttpMethod.Get,
                    url,
                    null,
                    30,
                    "audio/wav",
                    cancellationToken);
                return WavBytesToAudioClip(bytes, "YuiBackendAudio");
            }
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
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            request.SetRequestHeader("Accept", "audio/wav");
            try
            {
                await SendAsync(request, cancellationToken);
                return WavBytesToAudioClip(request.downloadHandler.data, "YuiBackendAudio");
            }
            catch (YuiBackendException ex) when (ShouldTryHttpClientFallback(ex))
            {
                Debug.LogWarning($"Yui backend UnityWebRequest TTS audio failed; retrying with HttpClient. {ex.Message}");
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var wavBytes = await SendHttpClientBytesAsync(
                    HttpMethod.Post,
                    url,
                    content,
                    60,
                    "audio/wav",
                    cancellationToken);
                return WavBytesToAudioClip(wavBytes, "YuiBackendAudio");
            }
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
            try
            {
                await SendAsync(request, cancellationToken);
                return Deserialize<TResponse>(request.downloadHandler.text);
            }
            catch (YuiBackendException ex) when (ShouldTryHttpClientFallback(ex))
            {
                if (!string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(path, "/config", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"Yui backend UnityWebRequest GET failed; retrying with HttpClient. {ex.Message}");
                }
                var json = await SendHttpClientAsync(
                    HttpMethod.Get,
                    ToAbsoluteUrl(path),
                    null,
                    10,
                    "application/json",
                    cancellationToken);
                return Deserialize<TResponse>(json);
            }
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

            try
            {
                await SendAsync(request, cancellationToken);
                return Deserialize<TResponse>(request.downloadHandler.text);
            }
            catch (YuiBackendException ex) when (ShouldTryHttpClientFallback(ex))
            {
                Debug.LogWarning($"Yui backend UnityWebRequest POST failed; retrying with HttpClient. {ex.Message}");
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var responseJson = await SendHttpClientAsync(
                    HttpMethod.Post,
                    ToAbsoluteUrl(path),
                    content,
                    60,
                    "application/json",
                    cancellationToken);
                return Deserialize<TResponse>(responseJson);
            }
        }

        private async Task<TResponse> DeleteJsonAsync<TResponse>(
            string path,
            CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Delete(ToAbsoluteUrl(path));
            request.timeout = 30;
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "application/json");
            try
            {
                await SendAsync(request, cancellationToken);
                return Deserialize<TResponse>(request.downloadHandler.text);
            }
            catch (YuiBackendException ex) when (ShouldTryHttpClientFallback(ex))
            {
                Debug.LogWarning($"Yui backend UnityWebRequest DELETE failed; retrying with HttpClient. {ex.Message}");
                var json = await SendHttpClientAsync(
                    HttpMethod.Delete,
                    ToAbsoluteUrl(path),
                    null,
                    30,
                    "application/json",
                    cancellationToken);
                return Deserialize<TResponse>(json);
            }
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

        private static bool ShouldTryHttpClientFallback(YuiBackendException ex)
        {
            return ex.StatusCode == 0;
        }

        private static async Task<string> SendHttpClientAsync(
            HttpMethod method,
            string url,
            HttpContent content,
            int timeoutSeconds,
            string accept,
            CancellationToken cancellationToken)
        {
            var bytes = await SendHttpClientBytesAsync(method, url, content, timeoutSeconds, accept, cancellationToken);
            return Encoding.UTF8.GetString(bytes);
        }

        private static async Task<byte[]> SendHttpClientBytesAsync(
            HttpMethod method,
            string url,
            HttpContent content,
            int timeoutSeconds,
            string accept,
            CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));

            var request = new HttpRequestMessage(method, url);
            try
            {
                request.Content = content;
                request.Headers.ConnectionClose = true;
                if (!string.IsNullOrWhiteSpace(accept))
                {
                    request.Headers.Accept.ParseAdd(accept);
                }

                using var response = await FallbackHttpClient.SendAsync(request, timeout.Token);
                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new YuiBackendException(
                        (long)response.StatusCode,
                        response.ReasonPhrase,
                        Encoding.UTF8.GetString(bytes),
                        url);
                }

                return bytes;
            }
            catch (YuiBackendException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new YuiBackendException(0, $"HttpClient fallback failed: {ex}", string.Empty, url);
            }
            finally
            {
                request.Content = null;
                request.Dispose();
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

        private static AudioClip WavBytesToAudioClip(byte[] wavBytes, string clipName)
        {
            if (wavBytes == null || wavBytes.Length < 44)
            {
                return null;
            }

            using var stream = new MemoryStream(wavBytes);
            using var reader = new BinaryReader(stream);
            if (ReadFourCc(reader) != "RIFF")
            {
                return null;
            }

            reader.ReadInt32();
            if (ReadFourCc(reader) != "WAVE")
            {
                return null;
            }

            short channels = 1;
            int sampleRate = 24000;
            short bitsPerSample = 16;
            byte[] data = null;

            while (stream.Position + 8 <= stream.Length)
            {
                var chunkId = ReadFourCc(reader);
                var chunkSize = reader.ReadInt32();
                var chunkStart = stream.Position;
                if (chunkId == "fmt ")
                {
                    var format = reader.ReadInt16();
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt16();
                    bitsPerSample = reader.ReadInt16();
                    if (format != 1 || bitsPerSample != 16)
                    {
                        return null;
                    }
                }
                else if (chunkId == "data")
                {
                    data = reader.ReadBytes(chunkSize);
                }

                stream.Position = chunkStart + chunkSize + (chunkSize % 2);
                if (data != null)
                {
                    break;
                }
            }

            if (data == null || data.Length < 2 || channels <= 0)
            {
                return null;
            }

            var sampleValueCount = data.Length / 2;
            var samples = new float[sampleValueCount];
            for (var i = 0; i < sampleValueCount; i++)
            {
                samples[i] = Mathf.Clamp(BitConverter.ToInt16(data, i * 2) / 32768f, -1f, 1f);
            }

            var clip = AudioClip.Create(
                string.IsNullOrWhiteSpace(clipName) ? "YuiBackendAudio" : clipName,
                sampleValueCount / channels,
                channels,
                sampleRate,
                false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static string ReadFourCc(BinaryReader reader)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(4));
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
