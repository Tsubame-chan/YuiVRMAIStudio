# はじめに・よくある質問 / Help & FAQ

[README 日本語](../README.md) · [README English](../README.en.md) · [ダウンロード / Releases](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.4-beta.2) · [不具合報告 / Issues](https://github.com/Tsubame-chan/YuiVRMAIStudio/issues)

## 日本語

**どれをダウンロードすればいい？**

MacはMacOSPublicBeta、WindowsはWindowsPublicBetaのアプリZIPです。Code ZIPはソースで、そのまま起動できません。[Mac導入](MAC_PUBLIC_BETA.md) / [Windows導入](SETUP_GUIDE.md)。

**iPhone版は？**

iOS 26以降。日本向け無料版を[App Store](https://apps.apple.com/jp/app/id6815341780)から入手できます。

**初回に通信する？**

デスクトップ版は確認後、約2.5GBのAI・音声データとOS別実行環境を取得します。展開にはさらに空き容量が必要です。iOS版は標準E2B・音声を同梱しており、初回の追加取得はありません。E4Bは設定から任意に取得します。取得中は容量・通信・進捗を確認してください。

**どのAIを使う？**

標準E2Bは軽量でオフライン会話向け。macOS/iOSの任意E4Bはより高品質ですが、応答時間と負荷が増えます。高品質な会話にはOpenAI APIを推奨します。APIキーと利用料金が必要で、ChatGPTの契約とは別です。詳細設定ではTalk/Work別に生成上限、コンテキスト、温度などを調整できます。迷ったら既定値を使ってください。

**性格はどこで変える？**

設定のキャラクター項目で性格や口調を文章で指定します。共通のAI指示やモデル別指示も併用できます。APIに切り替えてもキャラクター設定を使います。

**自分のアバターを使いたい。**

設定からVRM 0.x / 1.0を読み込みます。Unity/VRChatアバターは先にVRMへ書き出します。購入ZIP・unitypackage・FBXを直接入れることはできません。[アバター導入](AVATAR_IMPORT.md)。読み込み表示が消えるまで待ってください。「着替え」は同じキャラクターの外見だけを変えます。

**記憶を消したい／内緒話をしたい。**

設定の記憶管理で確認・削除できます。会話履歴は別です。秘密モードは既存記憶を参照し、新規の会話を記録しません。外部APIへの送信は通常どおり発生します。[記憶の仕様](CONVERSATION_IDENTITY.md) / [プライバシー](PRIVACY.md)。

**音声が出ない／英語で話さない。**

音声エンジン、ミュート、端末音量、選択した声のデータを確認してください。標準は日本語VOICEVOXの5声です。英語メニューはありますが英語専用TTSは同梱していません。Backend音声はBackendと対象エンジンの設定も必要です。

**モデル・データの取得に失敗した。**

通信と空き容量を確認し、表示されたエラーを控えて再試行してください。異なる版のmanifestや古いデータを手動で混ぜないでください。[データとチェックサム](LOCAL_AI_ASSETS.md)。改善しなければOS、アプリ版、モデル名、エラー全文、再現手順をIssueに記載してください。APIキーや私的な会話・アバターは添付しないでください。

**操作案内をもう一度見たい。**

アプリのヘルプから「チュートリアルをもう一度見る」を開けます。高度なモデル設定はヘルプから確認できます。

**VPNにつながっているのにBackendが未接続。**

アプリにはPCのVPNアドレスとBackendのポートを指定します。PCで開く `127.0.0.1` は他端末から使えません。BackendがVPN側のアドレスで待ち受けていること、VPNの通信ルールとPCのファイアウォールも確認してください。Safari等で `http://PCのVPNアドレス:8000/health` に `status: ok` が表示されるか確認します。この `/health` は確認用で、アプリには付けません。ブラウザから到達できてもアプリが未接続なら、保存したURLとアプリの版を確認してください。管理画面の提供元が「接続済み」でも、iPhoneからPCへ到達できることを示すわけではありません。[Backend案内](BACKEND_CONSOLE.md)。

## English

- Download the **MacOSPublicBeta** or **WindowsPublicBeta** app ZIP from [Releases](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.4-beta.2). The Code ZIP is source only. See the [Mac guide](MAC_PUBLIC_BETA.en.md).
- The free Japan iOS release requires iOS 26 and is available on the [App Store](https://apps.apple.com/jp/app/id6815341780).
- Desktop first-run setup downloads approximately 2.5GB of AI/voice data plus its OS runtime after confirmation. Leave extra room for extraction. iOS includes standard E2B and speech data; E4B is optional. macOS also offers optional E4B.
- E2B favors lighter offline use; E4B improves quality at the cost of latency, memory and storage. OpenAI API is recommended for higher-quality chat and requires an API key and API charges, separately from a ChatGPT subscription.
- Character settings accept written personality/tone instructions. Common and model-specific instructions also apply. Local advanced settings control context/output/thinking budgets and sampling per Talk/Work mode; defaults are a good starting point.
- Import your own **VRM**, not a purchase ZIP, prefab, FBX or unitypackage. Export from your configured Unity project first. Appearance changes keep the character's personality and memory. See [avatar import](AVATAR_IMPORT.en.md).
- Character memory persists across restart/local/API switching. History and long-term memory are separate. Secret mode reads existing memory without recording new chat; it does not prevent API transmission. See [memory](CONVERSATION_IDENTITY.md) and [privacy](PRIVACY.md).
- Default speech is Japanese VOICEVOX, with five standard voices. English UI does not include an English TTS model. Check engine, mute, volume and installed voice data if speech fails.
- Retry downloads after checking connectivity/free space. Do not mix manifests/data from older releases. If still failing, report app version, OS, model, full error and steps via [Issues](https://github.com/Tsubame-chan/YuiVRMAIStudio/issues). Remove API keys, private conversations and private avatars.
- Replay the illustrated tutorial from Help. Support status and experimental integrations are listed in [runtime support](RUNTIME_SUPPORT.md).
