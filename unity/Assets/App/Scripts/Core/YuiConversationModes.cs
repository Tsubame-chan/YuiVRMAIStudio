using System;

namespace YuiPhysicalAI.Core
{
    public static class YuiConversationModes
    {
        public const string Stable = "stable";
        public const string BackendAi = "backend_ai";
        public const string RealtimeVoice = "realtime_voice";
        public const string RealtimeVoicevox = "realtime_voicevox";
        public const string RealtimeAivis = "realtime_aivis";
        public const string RealtimeTranslate = "realtime_translate";
        public const string LocalAi = "local_ai";
        public const string DirectOpenAi = "direct_openai";

        public const string BackendVoice = "voice";
        public const string BackendVoiceText = "voice_text";
        public const string BackendTranslate = "translate";

        public static readonly string[] DropdownLabels =
        {
            "Auto Select (Backend > Local)",
            "Local Gemma SLM (On-device)",
            "Backend Talk (Standard)",
            "Realtime Talk (OpenAI Voice)",
            "Realtime Talk (VOICEVOX)",
            "Realtime Talk (AivisSpeech HD)",
            "Realtime Translation (Backend)",
            "Direct OpenAI API (No Backend)"
        };

        public static bool IsRealtime(string mode)
        {
            var normalized = Normalize(mode);
            return string.Equals(normalized, RealtimeVoice, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, RealtimeVoicevox, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, RealtimeAivis, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, RealtimeTranslate, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRealtimeVoicevox(string mode)
        {
            return string.Equals(Normalize(mode), RealtimeVoicevox, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRealtimeAivis(string mode)
        {
            return string.Equals(Normalize(mode), RealtimeAivis, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRealtimeTextTts(string mode)
        {
            var normalized = Normalize(mode);
            return string.Equals(normalized, RealtimeVoicevox, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, RealtimeAivis, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRealtimeTranslate(string mode)
        {
            return string.Equals(Normalize(mode), RealtimeTranslate, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, BackendTranslate, StringComparison.OrdinalIgnoreCase);
        }

        public static string BackendMode(string mode)
        {
            var normalized = Normalize(mode);
            if (string.Equals(normalized, RealtimeTranslate, StringComparison.OrdinalIgnoreCase))
            {
                return BackendTranslate;
            }

            if (string.Equals(normalized, RealtimeVoicevox, StringComparison.OrdinalIgnoreCase))
            {
                return BackendVoiceText;
            }

            if (string.Equals(normalized, RealtimeAivis, StringComparison.OrdinalIgnoreCase))
            {
                return BackendVoiceText;
            }

            return BackendVoice;
        }

        public static int DropdownIndex(string mode)
        {
            var normalized = Normalize(mode);
            if (string.Equals(normalized, BackendAi, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (string.Equals(normalized, RealtimeVoice, StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (string.Equals(normalized, RealtimeVoicevox, StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            if (string.Equals(normalized, RealtimeTranslate, StringComparison.OrdinalIgnoreCase))
            {
                return 6;
            }

            if (string.Equals(normalized, RealtimeAivis, StringComparison.OrdinalIgnoreCase))
            {
                return 5;
            }

            if (string.Equals(normalized, DirectOpenAi, StringComparison.OrdinalIgnoreCase))
            {
                return 7;
            }

            if (string.Equals(normalized, LocalAi, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 0;
        }

        public static string FromDropdownIndex(int index)
        {
            switch (index)
            {
                case 1:
                    return LocalAi;
                case 2:
                    return BackendAi;
                case 3:
                    return RealtimeVoice;
                case 4:
                    return RealtimeVoicevox;
                case 5:
                    return RealtimeAivis;
                case 6:
                    return RealtimeTranslate;
                case 7:
                    return DirectOpenAi;
                default:
                    return Stable;
            }
        }

        public static string Normalize(string mode)
        {
            if (string.Equals(mode, RealtimeVoice, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, BackendVoice, StringComparison.OrdinalIgnoreCase))
            {
                return RealtimeVoice;
            }

            if (string.Equals(mode, BackendAi, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "backend", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "backend-ai", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "local_backend", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "local-backend", StringComparison.OrdinalIgnoreCase))
            {
                return BackendAi;
            }

            if (string.Equals(mode, LocalAi, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "local", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "on_device", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "on-device", StringComparison.OrdinalIgnoreCase))
            {
                return LocalAi;
            }

            if (string.Equals(mode, DirectOpenAi, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "openai_direct", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "direct-api", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "direct_api", StringComparison.OrdinalIgnoreCase))
            {
                return DirectOpenAi;
            }

            if (string.Equals(mode, RealtimeVoicevox, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, BackendVoiceText, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "voicevox", StringComparison.OrdinalIgnoreCase))
            {
                return RealtimeVoicevox;
            }

            if (string.Equals(mode, RealtimeAivis, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "aivis_realtime", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "realtime-aivis", StringComparison.OrdinalIgnoreCase))
            {
                return RealtimeAivis;
            }

            if (string.Equals(mode, RealtimeTranslate, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, BackendTranslate, StringComparison.OrdinalIgnoreCase))
            {
                return RealtimeTranslate;
            }

            return Stable;
        }

        public static string StatusLabel(string mode)
        {
            var normalized = Normalize(mode);
            if (string.Equals(normalized, RealtimeVoice, StringComparison.OrdinalIgnoreCase))
            {
                return "Realtime Talk ON";
            }

            if (string.Equals(normalized, BackendAi, StringComparison.OrdinalIgnoreCase))
            {
                return "Backend Talk ON";
            }

            if (string.Equals(normalized, LocalAi, StringComparison.OrdinalIgnoreCase))
            {
                return "Local AI ON";
            }

            if (string.Equals(normalized, DirectOpenAi, StringComparison.OrdinalIgnoreCase))
            {
                return "API Mode ON";
            }

            if (string.Equals(normalized, RealtimeVoicevox, StringComparison.OrdinalIgnoreCase))
            {
                return "Realtime Talk VOICEVOX ON";
            }

            if (string.Equals(normalized, RealtimeAivis, StringComparison.OrdinalIgnoreCase))
            {
                return "Realtime Talk Aivis ON";
            }

            if (string.Equals(normalized, RealtimeTranslate, StringComparison.OrdinalIgnoreCase))
            {
                return "Realtime Translate ON";
            }

            return string.Empty;
        }

        public static string ExperimentalWarningText(string mode)
        {
            var label = StatusLabel(mode);
            return string.IsNullOrEmpty(label)
                ? string.Empty
                : string.Equals(Normalize(mode), LocalAi, StringComparison.OrdinalIgnoreCase)
                    ? $"{label}: 端末内モデルを優先します。難しい質問や高精度な画像理解はAPIの方が向いています。"
                    : string.Equals(Normalize(mode), BackendAi, StringComparison.OrdinalIgnoreCase)
                        ? $"{label}: バックエンドの標準会話基盤を固定で使用します。バックエンド未起動時はAuto SelectかLocal Gemmaを使ってください。"
                    : string.Equals(Normalize(mode), DirectOpenAi, StringComparison.OrdinalIgnoreCase)
                        ? $"{label}: BackendなしでAPIキーを使います。声はSettingsのTTS Modeがそのまま使われます。"
                        : $"{label}: 実験機能です。音声ストリーム接続中はAPIコストが増えやすいので、使う時だけオンにしてください。";
        }

        public static string InstructionsForMode(string mode, string characterName)
        {
            if (string.Equals(mode, BackendTranslate, StringComparison.OrdinalIgnoreCase))
            {
                return "You are a realtime interpreter between Japanese and English. If the user speaks Japanese, translate it into natural English. If the user speaks English, translate it into natural Japanese. If the utterance mixes both languages, translate each meaningful part into the other language while preserving names, titles, and numbers. Do not answer questions, acknowledge setup requests, or add commentary; output only the translation.";
            }

            var name = string.IsNullOrWhiteSpace(characterName) ? "Yui" : characterName.Trim();
            if (string.Equals(mode, BackendVoiceText, StringComparison.OrdinalIgnoreCase))
            {
                return $"{name}として、日本語で自然に会話してください。音声はUnity側のVOICEVOXで読み上げるので、テキストだけを返してください。返答は原則1〜2文、80字前後を目安にしてください。複雑な質問では必要な要点を短く返し、相づちや短い質問にはさらに短く返してください。「短くまとめると」「少し整理して」など、返答方針の前置きは言わず、答えから始めてください。検索ツールが使える場合、天気、ニュース、場所、営業時間、価格、最新情報など現在性のある質問では必要に応じて確認してから答えてください。検索、調査、候補出し、比較、イベント探しを依頼されたら、その場で調べて具体的な候補を返してください。検索できる、絞り込める、などの説明だけで終わらないでください。検索系の回答では、可能なら3〜6件を、名称、日付またはエリア、短い理由つきで話してください。URLはユーザーが明示的に求めた場合以外は本文に入れず、必要ならリンクも出せると短く伝えてください。外部アプリ操作や予約、購入、ナビ開始など実際の操作はできません。できない操作は短く伝え、代わりに調べられる情報を答えてください。";
            }

            return $"{name}として、日本語で自然に会話してください。返答は短めに、音声会話として聞き取りやすくしてください。「短くまとめると」「少し整理して」など、返答方針の前置きは言わず、答えから始めてください。可能な範囲で、明るく若い女性らしい高めの声に寄せてください。Web検索、天気、最新情報、外部アプリ操作はこのモードではできません。求められた場合は、調べているふりをせず、このモードでは取得できないことを短く伝えてください。";
        }
    }
}
