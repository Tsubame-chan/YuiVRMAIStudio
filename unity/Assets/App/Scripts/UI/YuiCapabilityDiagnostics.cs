using System;

namespace YuiPhysicalAI.UI
{
    public static class YuiCapabilityDiagnostics
    {
        public static string FormatBody(YuiCapabilitySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "Backend: <color=#AEB7C4><b>--</b></color> | Local VOICEVOX: <color=#AEB7C4><b>--</b></color>";
            }

            var backend = FormatLine("Backend", snapshot.Backend);
            var database = FormatLine("DB", snapshot.Database);
            var openAi = FormatLine("OpenAI", snapshot.OpenAi);
            var voicevox = FormatLine("Local VOICEVOX", snapshot.Tts("server"));
            var aivis = FormatLine("AivisSpeech HD", snapshot.Tts("aivis"));
            var irodori = FormatLine("Irodori TTS", snapshot.Tts("server-http"));
            return $"{backend} | {database} | {openAi}\n{voicevox} | {aivis} | {irodori}";
        }

        public static string FormatDetail(YuiCapabilitySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "Backendログと設定を確認してください。";
            }

            var backendAi = snapshot.Conversation(YuiPhysicalAI.Core.YuiConversationModes.BackendAi);
            if (backendAi.State == YuiCapabilityState.NeedsBackend)
            {
                return "Backend未接続です。基本会話はローカル/Direct APIで続行できますが、Realtime、DB、Backend TTSにはBackendが必要です。";
            }

            if (snapshot.OpenAi.State == YuiCapabilityState.SetupRequired)
            {
                return "OpenAI APIキーが未設定です。Backend TalkやDirect APIの高精度モデルを使う場合はキーを設定してください。";
            }

            var irodori = snapshot.Tts("server-http");
            if (irodori.State == YuiCapabilityState.SetupRequired)
            {
                return "Irodori TTSは表示されていますが、Backend側のHTTP TTS設定が必要です。";
            }

            var aivis = snapshot.Tts("aivis");
            if (aivis.State == YuiCapabilityState.SetupRequired)
            {
                return "AivisSpeech HDはBackend側の設定後に使用できます。VOICEVOXはローカルまたはBackendで使用できます。";
            }

            return "主要な会話/音声providerは使用できる状態です。音声や画像が動かない場合はSettingsのデバイス選択も確認してください。";
        }

        public static string DecorateTtsLabel(string label, string ttsMode, YuiCapabilitySnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(label) || snapshot == null)
            {
                return label;
            }

            var item = snapshot.Tts(ttsMode);
            if (item == null)
            {
                return label;
            }

            return $"{label} - {ShortState(item)}";
        }

        public static string DecorateConversationLabel(string label, string conversationMode, YuiCapabilitySnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(label) || snapshot == null)
            {
                return label;
            }

            var item = snapshot.Conversation(conversationMode);
            return item == null ? label : $"{label} - {ShortState(item)}";
        }

        public static string ShortState(YuiCapabilityItem item)
        {
            if (item == null)
            {
                return "--";
            }

            switch (item.State)
            {
                case YuiCapabilityState.Ready:
                    return item.Route == YuiCapabilityRoute.Native || item.Route == YuiCapabilityRoute.Local
                        ? "Local ready"
                        : item.Route == YuiCapabilityRoute.Backend
                            ? "Backend ready"
                            : "Ready";
                case YuiCapabilityState.Degraded:
                    return "Degraded";
                case YuiCapabilityState.NeedsBackend:
                    return "Needs backend";
                case YuiCapabilityState.SetupRequired:
                    return "Setup required";
                default:
                    return "Unavailable";
            }
        }

        private static string FormatLine(string label, YuiCapabilityItem item)
        {
            return $"{label}: {StatusBadge(item)}";
        }

        private static string StatusBadge(YuiCapabilityItem item)
        {
            var label = StatusText(item);
            var color = StatusColor(item);
            return $"<color={color}><b>{label}</b></color>";
        }

        private static string StatusText(YuiCapabilityItem item)
        {
            if (item == null)
            {
                return "--";
            }

            switch (item.State)
            {
                case YuiCapabilityState.Ready:
                    return "OK";
                case YuiCapabilityState.Degraded:
                    return "WARN";
                case YuiCapabilityState.NeedsBackend:
                    return "WAIT";
                case YuiCapabilityState.SetupRequired:
                    return "SETUP";
                case YuiCapabilityState.Unavailable:
                    return "NG";
                default:
                    return "--";
            }
        }

        private static string StatusColor(YuiCapabilityItem item)
        {
            if (item == null)
            {
                return "#AEB7C4";
            }

            switch (item.State)
            {
                case YuiCapabilityState.Ready:
                    return "#7FE391";
                case YuiCapabilityState.Degraded:
                case YuiCapabilityState.NeedsBackend:
                case YuiCapabilityState.SetupRequired:
                    return "#FFD166";
                case YuiCapabilityState.Unavailable:
                    return "#FF8A80";
                default:
                    return "#AEB7C4";
            }
        }
    }
}
