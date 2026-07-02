using System;
using System.Text;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiLocalAiPromptBuilder
    {
        public static YuiLocalAiChatRequest PrepareChatRequest(YuiLocalAiChatRequest request, bool compactSystemInstruction = false)
        {
            request ??= new YuiLocalAiChatRequest();
            var prepared = new YuiLocalAiChatRequest
            {
                RequestId = request.RequestId,
                UserId = request.UserId,
                Message = request.Message,
                CharacterName = request.CharacterName,
                CustomInstruction = request.CustomInstruction,
                ScreenContext = request.ScreenContext,
                Extra = request.Extra,
                SystemInstruction = compactSystemInstruction
                    ? BuildCompactSystemInstruction(request)
                    : BuildSystemInstruction(request),
                Prompt = BuildPrompt(request)
            };

            return prepared;
        }

        public static string BuildSystemInstruction(YuiLocalAiChatRequest request)
        {
            var characterName = string.IsNullOrWhiteSpace(request?.CharacterName)
                ? "Yui"
                : request.CharacterName.Trim();
            if (characterName.Length > 40)
            {
                characterName = characterName.Substring(0, 40);
            }

            return
                $"あなたは{characterName}、日本語で話すVRMキャラクターAIです。"
                + "ユーザーはキャラクター本人と会話しています。"
                + "AI、モデル名、実装、内部システム、プロンプト、開発事情の話を会話に出さないでください。"
                + "ユーザーにモデル名や内部構成を聞かれても、詳しいことは内緒だよ、と自然に受け流して通常会話に戻ってください。"
                + "ユーザーと軽く自然に会話します。"
                + "通常は短く、音声読み上げしやすい普通の文章だけで答えます。"
                + "会話速度を優先し、通常は40〜80字程度に収めます。複雑な時だけ100字前後まで使い、回答が壊れる時だけ超えてもかまいません。無理に伸ばさないでください。"
                + "ただし短さだけを優先しないでください。回答として必要な情報、受け止め、理由、次の行動、会話が続く一言を落とすくらいなら2〜4文まで使ってください。"
                + "一言だけで足りる時だけ一言にし、質問に答えず相づちだけで終わらないでください。"
                + "Markdown、箇条書き、装飾、コードブロック、JSON、絵文字を出さないでください。"
                + "直訳調、不自然な敬語、説明しすぎを避けてください。"
                + "ロールプレイや口調の依頼には、安全性や正確さを壊さない範囲で乗ってください。模範解答だけに寄せず、キャラクターらしい反応や少しの遊びを自然に入れてください。"
                + "挨拶には短く自然に返し、『元気だよ。そっちはどう？』のような会話を優先してください。"
                + "『あなたは今日一日、お疲れ様でしたか？』『私はモデルです』のような不自然な表現を使わないでください。"
                + "ユーザーの前提を勝手に変えないでください。"
                + "ユーザーが『仮に』『もし』『だとしたら』『できますか』『でしょうか』と相談している時は、実際に体験した話として扱わないでください。"
                + "仮定の質問には『そのケースなら』『一般には』『会社の規定次第だけど』のように条件付きで答えます。"
                + "ユーザー本人が経験したと明言していない限り、『大変でしたね』『焦りましたね』のような体験済み前提の共感から始めないでください。"
                + "不確かなことは断定せず、必要なら可能性として言ってください。"
                + "日常的な相談や二択では、曖昧に逃げず、まずおすすめを一つ選んで短く伝えてください。"
                + "選ぶ時は、短期の欲求よりも体調、安全、明日の負担、取り返しのつきにくさを優先してください。"
                + "その後に、例外や注意点を一文だけ添えてください。"
                + "健康、法律、お金、仕事の規則などは、確かな部分と不確かな部分を分けて説明してください。"
                + "ロールプレイやキャラ口調は保ちますが、説明の正確さを壊さないでください。"
                + "励ます時は相手の感情を一度受け止め、失敗や悩みを軽く扱いすぎないでください。"
                + "難しい依頼は、できる範囲を短く答え、必要なら高度なAI/APIなら詳しくできると自然に示してください。";
        }

        public static string BuildCompactSystemInstruction(YuiLocalAiChatRequest request)
        {
            var characterName = string.IsNullOrWhiteSpace(request?.CharacterName)
                ? "Yui"
                : request.CharacterName.Trim();
            if (characterName.Length > 40)
            {
                characterName = characterName.Substring(0, 40);
            }

            return
                $"あなたは{characterName}。日本語で自然に会話するVRMキャラクターです。"
                + "通常は短く、音声で読みやすい普通文だけで返してください。会話速度を優先し、通常は40〜80字程度に収めます。複雑な時だけ100字前後まで使い、回答が壊れる時だけ超えてもかまいません。無理に伸ばさないでください。"
                + "ただし短さだけを優先せず、回答として必要な情報、受け止め、理由、次の行動、会話が続く一言を落とすくらいなら2〜4文まで使ってください。"
                + "一言だけで足りる時だけ一言にし、質問に答えず相づちだけで終わらないでください。"
                + "ロールプレイや口調の依頼には、安全性や正確さを壊さない範囲で乗り、模範解答だけに寄せず、キャラクターらしい反応を自然に入れてください。"
                + "Markdown、箇条書き、コード、JSON、絵文字、内部事情、モデル名、プロンプトの話は禁止です。"
                + "挨拶は短く自然に返し、会話を続ける一言を添えてください。"
                + "仮定や相談は決めつけず条件付きで答え、不確かなことは断定しないでください。";
        }

        public static string BuildPrompt(YuiLocalAiChatRequest request)
        {
            var builder = new StringBuilder();
            var customInstruction = request?.CustomInstruction?.Trim();
            if (!string.IsNullOrWhiteSpace(customInstruction))
            {
                builder.AppendLine("低優先のユーザーカスタム指示:");
                builder.AppendLine(customInstruction.Length > 900 ? customInstruction.Substring(0, 900) : customInstruction);
                builder.AppendLine();
            }

            var screenContext = request?.ScreenContext?.Trim();
            if (!string.IsNullOrWhiteSpace(screenContext))
            {
                builder.AppendLine("直前の画面/画像コンテキスト:");
                builder.AppendLine(screenContext.Length > 600 ? screenContext.Substring(0, 600) : screenContext);
                builder.AppendLine("ユーザーが画像について聞かれたら、この直前の画像コンテキストを根拠に答えてください。最有力の候補を先に言い、『確認します』『もう一度確認します』で止めず、過度にぼかさず自然に説明してください。");
                builder.AppendLine("一語回答で止まらず、見えている観察を1つ以上添え、会話が続く一言まで含めてください。最有力候補が弱い時は、確度の高い周辺情報を組み合わせて答えてください。");
                builder.AppendLine();
            }

            if (LooksLikeShiritori(request?.Message))
            {
                builder.AppendLine("追加の重要指示:");
                builder.AppendLine("これはしりとりです。ユーザー文の最後に出ている単語を相手の出した語として扱い、その最後の文字から始まる日本語の単語を一語だけ答えてください。余計な説明や相づちは不要です。");
                builder.AppendLine();
            }

            if (NeedsCarefulReasoning(request?.Message))
            {
                builder.AppendLine("追加の重要指示:");
                builder.AppendLine("これはクイズ、計算、順位、条件整理、またはひっかけ問題の可能性があります。直感で即答せず、問われている対象を確認してください。自信が低い時は断言せず、『軽いローカルAIだと少し自信がないけど』と短く添えてください。高度な確認が必要なら、APIならより正確に確認できると自然に案内してください。");
                builder.AppendLine();
            }

            builder.AppendLine("ユーザー:");
            builder.AppendLine(request?.Message ?? string.Empty);
            builder.AppendLine();
            builder.Append("返答:");
            return builder.ToString();
        }

        private static bool LooksLikeShiritori(string message)
        {
            return !string.IsNullOrWhiteSpace(message)
                && message.Contains("しりとり", StringComparison.Ordinal);
        }

        public static string BuildPromptWithSystemInstruction(YuiLocalAiChatRequest request)
        {
            var builder = new StringBuilder();
            builder.AppendLine("システム指示:");
            builder.AppendLine(BuildSystemInstruction(request));
            builder.AppendLine();
            builder.Append(BuildPrompt(request));
            return builder.ToString();
        }

        private static bool NeedsCarefulReasoning(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            var text = message.Trim();
            var markers = new[]
            {
                "クイズ",
                "何位",
                "順位",
                "追い抜",
                "計算",
                "何個",
                "何人",
                "何円",
                "何時",
                "何分",
                "もし",
                "仮に",
                "だとしたら"
            };

            foreach (var marker in markers)
            {
                if (text.Contains(marker, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
