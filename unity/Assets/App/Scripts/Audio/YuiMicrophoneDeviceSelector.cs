using UnityEngine;

namespace YuiPhysicalAI.Audio
{
    public sealed class YuiMicrophoneDeviceSelector
    {
        private readonly int preferredFrequency;

        public YuiMicrophoneDeviceSelector(int preferredFrequency)
        {
            this.preferredFrequency = preferredFrequency;
        }

        public string Select(string preferredDevice)
        {
            var devices = GetDevices();
            if (devices == null || devices.Length == 0)
            {
                Debug.LogWarning("Unity Microphone.devices is empty.");
                return null;
            }

            Debug.Log($"Unity microphones: {string.Join(", ", devices)}");
            if (!string.IsNullOrWhiteSpace(preferredDevice))
            {
                foreach (var device in devices)
                {
                    if (device == preferredDevice)
                    {
                        return device;
                    }
                }

                Debug.LogWarning($"Preferred microphone was not found: {preferredDevice}");
            }

            return devices[0];
        }

        public string[] GetOptions()
        {
            var devices = GetDevices();
            if (devices == null || devices.Length == 0)
            {
                return new[] { "Default" };
            }

            var options = new string[devices.Length + 1];
            options[0] = "Default";
            System.Array.Copy(devices, 0, options, 1, devices.Length);
            return options;
        }

        public string[] GetDevices()
        {
            return Microphone.devices ?? System.Array.Empty<string>();
        }

        public string DescribeCaps(string device)
        {
            Microphone.GetDeviceCaps(device, out var minFrequency, out var maxFrequency);
            return minFrequency == 0 && maxFrequency == 0
                ? $"{preferredFrequency}Hz"
                : $"{minFrequency}-{maxFrequency}Hz";
        }

        public int ResolveFrequency(string device)
        {
            Microphone.GetDeviceCaps(device, out var minFrequency, out var maxFrequency);
            Debug.Log($"Microphone caps device='{device}', min={minFrequency}, max={maxFrequency}");
            if (minFrequency == 0 && maxFrequency == 0)
            {
                return preferredFrequency;
            }

            return Mathf.Clamp(preferredFrequency, minFrequency, maxFrequency);
        }
    }
}
