using System;
using System.Threading;
using UnityEngine;

namespace YuiPhysicalAI.Api
{
    public sealed class YuiApiSmokeTester : MonoBehaviour
    {
        [SerializeField] private string backendUrl = "http://127.0.0.1:8000";
        [SerializeField] private string userId = "local_user";
        [SerializeField] private int speakerId = 14;
        [SerializeField] private float speedScale = 1.0f;
        [SerializeField] private AudioSource audioSource;
        [TextArea]
        [SerializeField] private string message = "こんにちは、ゆい。短く挨拶して。";

        private CancellationTokenSource cancellationTokenSource;
        private YuiBackendClient client;

        private void Awake()
        {
            client = new YuiBackendClient(backendUrl);
            cancellationTokenSource = new CancellationTokenSource();
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        private async void Start()
        {
            try
            {
                var health = await client.GetHealthAsync(cancellationTokenSource.Token);
                Debug.Log($"Yui backend health: {health.Status} v{health.Version}");

                var result = await client.SendChatWithSpeechAsync(
                    message,
                    userId,
                    speakerId,
                    speedScale,
                    cancellationTokenSource.Token);

                Debug.Log(
                    $"Yui: {result.Chat.Text} face={result.Chat.Face} animation={result.Chat.Animation}");

                if (audioSource != null && result.AudioClip != null)
                {
                    audioSource.clip = result.AudioClip;
                    audioSource.Play();
                }
            }
            catch (YuiBackendException ex) when (ex.StatusCode == 0)
            {
                Debug.LogError(
                    "Backendに接続できません。PowerShellで scripts/run_backend.ps1 を起動し、VOICEVOX Engineも起動してからPlay Modeを再実行してください。\n" +
                    ex);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        private void OnDestroy()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
    }
}
