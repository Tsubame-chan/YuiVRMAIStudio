using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Audio;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiSettingsOverlay
    {
        private void StartMicrophoneMonitor()
        {
            StopMicrophoneMonitor();
            var device = MicrophoneValue();
            if (microphoneTestDeviceSelector == null)
            {
                microphoneTestDeviceSelector = new YuiMicrophoneDeviceSelector(44100);
            }
            device = microphoneTestDeviceSelector.Select(device == "Default" ? string.Empty : device);

            if (string.IsNullOrWhiteSpace(device))
            {
                SetMicrophoneTestStatus("Mic Test: no microphone");
                SetMicrophoneTestLevel(0f);
                return;
            }

            microphoneTestFrequency = microphoneTestDeviceSelector.ResolveFrequency(device);
            microphoneTestDevice = device;
            microphoneTestRecorder = new YuiUnityMicrophoneRecorder();

            if (!microphoneTestRecorder.Start(microphoneTestDevice, microphoneTestFrequency, 5, true))
            {
                microphoneTestRecorder = null;
                microphoneTestDevice = null;
                microphoneTestStartedAt = -1f;
                SetMicrophoneTestStatus("Mic Test: failed");
                return;
            }

            if (YuiMacEditorMicrophoneRecorder.IsSupported)
            {
                var recorder = new YuiMacEditorMicrophoneRecorder();
                if (recorder.Start(microphoneTestFrequency, 8))
                {
                    microphoneTestMacFallback = recorder;
                }
                else
                {
                    recorder.Dispose();
                }
            }

            microphoneTestStartedAt = Time.realtimeSinceStartup;
            SetMicrophoneTestStatus($"Mic Test: listening ({microphoneTestDevice})");
            Debug.Log($"Yui mic test monitor: device='{microphoneTestDevice}', frequency={microphoneTestFrequency}");
        }

        private void StopMicrophoneMonitor()
        {
            microphoneTestRecorder?.Stop();
            microphoneTestRecorder = null;
            microphoneTestMacFallback?.Dispose();
            microphoneTestMacFallback = null;
            microphoneTestDevice = null;
            microphoneTestStartedAt = -1f;
            SetMicrophoneTestLevel(0f);
        }

        private void UpdateMicrophoneMonitor()
        {
            if (microphoneTestRecorder == null || !microphoneTestRecorder.HasClip)
            {
                return;
            }

            if (Time.realtimeSinceStartup - microphoneTestStartedAt > 8f)
            {
                StopMicrophoneMonitor();
                SetMicrophoneTestStatus("Mic Test: complete");
                return;
            }

            var fallbackLevel = microphoneTestMacFallback != null ? microphoneTestMacFallback.LatestLevel : 0f;
            var level = microphoneTestRecorder.RecentLevel(microphoneTestSamples, fallbackLevel);
            SetMicrophoneTestLevel(Mathf.Clamp01(level));
        }

        private void SetMicrophoneTestLevel(float level)
        {
            if (microphoneTestLevelFill == null)
            {
                return;
            }

            level = Mathf.Clamp01(level);
            var rect = microphoneTestLevelFill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(level, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetMicrophoneTestStatus(string text)
        {
            if (microphoneTestStatusText != null)
            {
                microphoneTestStatusText.text = text;
            }
        }
    }
}
