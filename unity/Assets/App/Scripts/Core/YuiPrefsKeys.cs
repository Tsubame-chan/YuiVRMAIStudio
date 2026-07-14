namespace YuiPhysicalAI.Core
{
    /// <summary>
    /// Centralized PlayerPrefs key namespace.
    /// Keep all runtime-persistent setting keys here so a rename, audit, or
    /// migration only has to touch one file.
    /// </summary>
    public static class YuiPrefsKeys
    {
        // Backend / connection
        public const string BackendUrl = "Yui.Settings.BackendUrl";
        public const string OpenAiApiKey = "Yui.Settings.OpenAiApiKey";
        public const string OpenAiModel = "Yui.Settings.OpenAiModel";
        public const string AutoAiFallbackEnabled = "Yui.Settings.AutoAiFallbackEnabled";

        // Voice / VOICEVOX
        public const string ConversationMode = "Yui.Settings.ConversationMode";
        public const string SpeakerId = "Yui.Settings.SpeakerId";
        public const string VoiceVolume = "Yui.Settings.VoiceVolume";
        public const string VoiceSpeed = "Yui.Settings.VoiceSpeed";
        public const string VoicePitch = "Yui.Settings.VoicePitch";
        public const string VoiceIntonation = "Yui.Settings.VoiceIntonation";
        public const string VoiceSynthesisVolume = "Yui.Settings.VoiceSynthesisVolume";
        public const string VoicePrePhonemeLength = "Yui.Settings.VoicePrePhonemeLength";
        public const string VoicePostPhonemeLength = "Yui.Settings.VoicePostPhonemeLength";
        public const string TtsMode = "Yui.Settings.TtsMode";
        public const string VoiceTuningSchemaVersion = "Yui.Settings.VoiceTuningSchemaVersion";
        public const string IrodoriVoiceGender = "Yui.Settings.IrodoriVoiceGender";
        public const string IrodoriVoiceInstruct = "Yui.Settings.IrodoriVoiceInstruct";
        public const string VoicePresetLibrary = "Yui.Settings.VoicePresetLibrary";

        // Microphone
        public const string MicrophoneDevice = "Yui.Settings.MicrophoneDevice";

        // Look / vision
        public const string LookCameraDevice = "Yui.Settings.LookCameraDevice";

        // Session / mode
        public const string SecretMode = "Yui.Session.SecretMode";
        public const string ChatInteractionMode = "Yui.Settings.ChatInteractionMode";
        public const string RealtimeVerboseLogging = "Yui.Settings.RealtimeVerboseLogging";

        // User-facing context
        public const string CustomInstruction = "Yui.Settings.CustomInstruction";
        public const string CharacterName = "Yui.Settings.CharacterName";

        // Avatar
        public const string AvatarSlot = "Yui.Settings.AvatarSlot";
        public const string AvatarSlotDefaultUpgraded = "Yui.Settings.AvatarSlotDefaultUpgraded";
        public const string CustomVrmPath = "Yui.Settings.CustomVrmPath";
        public const string CustomVrmPathPrefix = "Yui.Settings.CustomVrmPath";
        public const string CustomVrmNamePrefix = "Yui.Settings.CustomVrmName";

        // Background / scene
        public const string BackgroundPreset = "Yui.Settings.BackgroundPreset";

        // Window / display
        public const string WindowResolutionPreset = "Yui.Settings.WindowResolutionPreset";
        public const string WindowResolutionPresetDefaultUpgraded = "Yui.Settings.WindowResolutionPresetDefaultUpgraded";
        public const string WindowResolutionPresetListVersion = "Yui.Settings.WindowResolutionPresetListVersion";

        // Camera
        public const string CameraPresetPrefix = "Yui.Settings.CameraPreset";
    }
}
