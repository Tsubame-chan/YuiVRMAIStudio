using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChatdollKit.SpeechSynthesizer;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.Audio
{
    public sealed class YuiChatdollVoicevoxTts : MonoBehaviour
    {
        [SerializeField] private VoicevoxSpeechSynthesizer synthesizer;
        [SerializeField] private string endpointUrl = "http://127.0.0.1:50021";
        [SerializeField] private int speaker = 14;
        [SerializeField] private bool overwriteEndpoint = true;
        [SerializeField] private float speedScale = 1.0f;
        [SerializeField] private float pitchScale = 0.0f;
        [SerializeField] private float intonationScale = 1.0f;
        [SerializeField] private float volumeScale = 1.0f;
        [SerializeField] private float prePhonemeLength = 0.1f;
        [SerializeField] private float postPhonemeLength = 0.1f;

        private readonly HashSet<string> initializedSpeakerKeys = new HashSet<string>();
        private readonly HashSet<string> loggedEngineInfoEndpoints = new HashSet<string>();

        private void Awake()
        {
            EnsureSynthesizer();
            ConfigureSynthesizer();
        }

        public async Task<AudioClip> SynthesizeAsync(
            string text,
            string voiceStyle = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return await SynthesizeDirectAsync(text, cancellationToken);
        }

        public void Configure(string endpoint, int speakerId)
        {
            Configure(endpoint, speakerId, speedScale, pitchScale, intonationScale, volumeScale, prePhonemeLength, postPhonemeLength);
        }

        public void Configure(
            string endpoint,
            int speakerId,
            float speed,
            float pitch,
            float intonation,
            float synthesisVolume,
            float prePause,
            float postPause)
        {
            endpointUrl = endpoint;
            var settings = new YuiVoiceSettings(
                speakerId,
                speed,
                pitch,
                intonation,
                synthesisVolume,
                prePause,
                postPause);
            speaker = settings.SpeakerId;
            speedScale = settings.SpeedScale;
            pitchScale = settings.PitchScale;
            intonationScale = settings.IntonationScale;
            volumeScale = settings.SynthesisVolumeScale;
            prePhonemeLength = settings.PrePhonemeLength;
            postPhonemeLength = settings.PostPhonemeLength;
            EnsureSynthesizer();
            ConfigureSynthesizer();
        }

        private async Task<AudioClip> SynthesizeDirectAsync(string text, CancellationToken cancellationToken)
        {
            var totalTimer = System.Diagnostics.Stopwatch.StartNew();
            await LogEngineInfoOnceAsync(cancellationToken);
            var initTimer = System.Diagnostics.Stopwatch.StartNew();
            var initializedSpeaker = await EnsureSpeakerInitializedAsync(cancellationToken);
            initTimer.Stop();
            var queryUrl = endpointUrl.TrimEnd('/')
                + "/audio_query?speaker="
                + speaker
                + "&text="
                + UnityWebRequest.EscapeURL(text);

            using var queryRequest = UnityWebRequest.Post(queryUrl, new WWWForm());
            queryRequest.timeout = 6;
            var queryTimer = System.Diagnostics.Stopwatch.StartNew();
            await SendAsync(queryRequest, cancellationToken);
            queryTimer.Stop();
            var audioQuery = JObject.Parse(queryRequest.downloadHandler.text);
            audioQuery["speedScale"] = speedScale;
            audioQuery["pitchScale"] = pitchScale;
            audioQuery["intonationScale"] = intonationScale;
            audioQuery["volumeScale"] = volumeScale;
            audioQuery["prePhonemeLength"] = prePhonemeLength;
            audioQuery["postPhonemeLength"] = postPhonemeLength;

            var synthesisPath = "/cancellable_synthesis";
            var synthesisRequest = CreateSynthesisRequest(synthesisPath, audioQuery);
            var synthesisTimer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await SendAsync(synthesisRequest, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                synthesisRequest.Dispose();
                throw;
            }
            catch (InvalidOperationException)
            {
                if (synthesisRequest.responseCode != 404 && synthesisRequest.responseCode != 405)
                {
                    synthesisRequest.Dispose();
                    throw;
                }

                Debug.LogWarning("VOICEVOX cancellable_synthesis is unavailable; falling back to synthesis.");
                synthesisRequest.Dispose();
                synthesisPath = "/synthesis";
                synthesisRequest = CreateSynthesisRequest(synthesisPath, audioQuery);
                await SendAsync(synthesisRequest, cancellationToken);
            }
            synthesisTimer.Stop();
            try
            {
                var clip = CopyAudioClip(DownloadHandlerAudioClip.GetContent(synthesisRequest), "YuiVoicevoxAudio");
                Debug.Log(
                    $"Yui VOICEVOX direct latency: endpoint={endpointUrl}, path={synthesisPath}, speaker={speaker}, initialized={initializedSpeaker}, total={totalTimer.ElapsedMilliseconds} ms, init={initTimer.ElapsedMilliseconds} ms, query={queryTimer.ElapsedMilliseconds} ms, synthesis={synthesisTimer.ElapsedMilliseconds} ms, chars={text.Length}");
                return clip;
            }
            finally
            {
                synthesisRequest.Dispose();
            }
        }

        private UnityWebRequest CreateSynthesisRequest(string path, JObject audioQuery)
        {
            var synthesisUrl = endpointUrl.TrimEnd('/') + path + "?speaker=" + speaker;
            var request = UnityWebRequestMultimedia.GetAudioClip(synthesisUrl, AudioType.WAV);
            request.timeout = 18;
            request.method = UnityWebRequest.kHttpVerbPOST;
            request.SetRequestHeader("Content-Type", "application/json");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(audioQuery.ToString()));
            return request;
        }

        private async Task<bool> EnsureSpeakerInitializedAsync(CancellationToken cancellationToken)
        {
            var endpoint = endpointUrl.TrimEnd('/');
            var key = endpoint + "|" + speaker;
            if (initializedSpeakerKeys.Contains(key))
            {
                return false;
            }

            var initializeUrl = endpoint + "/initialize_speaker?speaker=" + speaker + "&skip_reinit=true";
            using var initializeRequest = UnityWebRequest.Post(initializeUrl, new WWWForm());
            initializeRequest.timeout = 10;
            try
            {
                await SendAsync(initializeRequest, cancellationToken);
                initializedSpeakerKeys.Add(key);
                return true;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    throw;
                }

                // Some VOICEVOX builds deprecate or omit this endpoint. Synthesis can still proceed.
                Debug.LogWarning($"VOICEVOX speaker pre-initialize skipped: {ex.Message}");
                initializedSpeakerKeys.Add(key);
                return false;
            }
        }

        private async Task LogEngineInfoOnceAsync(CancellationToken cancellationToken)
        {
            var endpoint = endpointUrl.TrimEnd('/');
            if (loggedEngineInfoEndpoints.Contains(endpoint))
            {
                return;
            }

            loggedEngineInfoEndpoints.Add(endpoint);
            using var versionRequest = UnityWebRequest.Get(endpoint + "/version");
            versionRequest.timeout = 3;
            try
            {
                await SendAsync(versionRequest, cancellationToken);
                Debug.Log($"Yui VOICEVOX engine: endpoint={endpoint}, version={versionRequest.downloadHandler.text}");
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    throw;
                }

                Debug.LogWarning($"Yui VOICEVOX engine info unavailable: endpoint={endpoint}, error={ex.Message}");
            }
        }

        private static async Task SendAsync(UnityWebRequest request, CancellationToken cancellationToken)
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
                throw new System.InvalidOperationException(request.error);
            }
        }

        private void EnsureSynthesizer()
        {
            if (synthesizer != null)
            {
                return;
            }

            synthesizer = GetComponent<VoicevoxSpeechSynthesizer>();
            if (synthesizer == null)
            {
                synthesizer = gameObject.AddComponent<VoicevoxSpeechSynthesizer>();
            }
        }

        private void ConfigureSynthesizer()
        {
            if (synthesizer == null)
            {
                return;
            }

            synthesizer.Configure(endpointUrl, overwriteEndpoint);
            synthesizer.Speaker = speaker;
            if (synthesizer.VoiceStyles == null)
            {
                synthesizer.VoiceStyles = new List<VoicevoxSpeechSynthesizer.VoiceStyle>();
            }
            synthesizer.IsEnabled = true;
            synthesizer.Timeout = 10;
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
    }
}
