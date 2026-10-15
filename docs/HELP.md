# はじめに・よくある質問 / Help & FAQ

[README 日本語](../README.md) · [README English](../README.en.md) · [ダウンロード / Releases](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases) · [不具合報告 / Issues](https://github.com/Tsubame-chan/YuiVRMAIStudio/issues)

## 日本語

**どれをダウンロードすればいい？**

MacはMacOSPublicBeta、WindowsはWindowsPublicBetaのアプリZIPです。Code ZIPはソースで、そのまま起動できません。[Mac導入](MAC_PUBLIC_BETA.md) / [Windows導入](SETUP_GUIDE.md)。

**iPhone版は？**

iOS 26以降。日本向け無料版を[App Store](https://apps.apple.com/jp/app/id6815341780)から入手できます。

**初回に通信する？**

デスクトップ版は確認後、約2.5GBのAI・音声データとOS別実行環境を取得します。展開にはさらに空き容量が必要です。iOS版は標準E2B・音声を同梱しており、初回の追加取得はありません。E4Bは設定から任意に取得します。容量は開始前の確認画面に表示します。対応する新版では取得中の進捗をパーセントで表示します。

**どのAIを使う？**

標準E2Bは軽量でオフライン会話向け。Mac・Windows・iOSの任意E4Bはより高品質ですが、応答時間と負荷が増えます。高品質な会話にはOpenAI APIを推奨します。APIキーと利用料金が必要で、ChatGPTの契約とは別です。詳細設定ではTalk/Work別に生成上限、コンテキスト、温度などを調整できます。迷ったら既定値を使ってください。

**自分でダウンロードしたAIモデルを使える？**

追加モデル対応版では、設定 → AI → 端末内AI → 追加から、単一GGUF（.gguf）または互換LiteRT-LM（.litertlm）の文章モデルを取り込み、モデル一覧から選択します。PCやBackendは不要です。生のsafetensors、分割GGUF、画像・音声用追加ファイルは直接使えません。形式だけで互換性や品質を保証できず、大きいモデルは停止・クラッシュする可能性があります。標準Gemmaとは別の実験用機能です。対応OS・実行エンジンの条件と配布状況は[追加モデルの案内](LOCAL_MODEL_IMPORT.md)を確認してください。アプリ内ヘルプにも日英の常設案内があります。

**Backendはどこから入手する？**

[GitHub Releases](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases)からPC版のアプリZIPを入手し、初回準備後にYuiBackend内のStart_Yui_Backend.command（Mac）／Start_Yui_Backend.bat（Windows）で起動します。Code ZIPはソースです。スマートフォンから使う場合はPCと同じTailscaleへ接続し、設定にPCの接続先を入力します。端末内AI・端末内音声・OpenAIへの直接接続だけなら不要です。[Mac手順](MAC_PUBLIC_BETA.md) / [Windows手順](SETUP_GUIDE.md)。対応版ではヘルプの接続状況と設定の「？」からも入手・接続方法を開けます。

**性格はどこで変える？**

設定のキャラクター項目で性格や口調を文章で指定します。共通のAI指示やモデル別指示も併用できます。APIに切り替えてもキャラクター設定を使います。

**自分のアバターを使いたい。**

設定からVRM 0.x / 1.0を読み込みます。Unity/VRChatアバターは先にVRMへ書き出します。購入ZIP・unitypackage・FBXを直接入れることはできません。[アバター導入](AVATAR_IMPORT.md)。読み込み表示が消えるまで待ってください。「着替え」は同じキャラクターの外見だけを変えます。

**記憶を消したい／内緒話をしたい。**

設定の記憶管理で確認・削除できます。会話履歴は別です。秘密モードは既存記憶を参照し、新規の会話を記録しません。外部APIへの送信は通常どおり発生します。[記憶の仕様](CONVERSATION_IDENTITY.md) / [プライバシー](PRIVACY.md)。

**音声が出ない／英語で話さない。**

音声エンジン、ミュート、端末音量、選択した声のデータを確認してください。標準は日本語VOICEVOXの5声です。Mac・iOS版0.2.5以降は設定から英語Kokoroの6声を任意取得できます。日本語Irodoriの3声も任意取得です。Backend音声はBackendと対象エンジンの設定も必要です。

**モデル・データの取得に失敗した。**

通信と空き容量を確認し、表示されたエラーを控えて再試行してください。異なる版のmanifestや古いデータを手動で混ぜないでください。[データとチェックサム](LOCAL_AI_ASSETS.md)。改善しなければOS、アプリ版、モデル名、エラー全文、再現手順をIssueに記載してください。APIキーや私的な会話・アバターは添付しないでください。

**操作案内をもう一度見たい。**

アプリのヘルプから「チュートリアルをもう一度見る」を開けます。高度なモデル設定はヘルプから確認できます。

**VPNにつながっているのにBackendが未接続。**

アプリにはPCのVPNアドレスとBackendのポートを指定します。PCで開く `127.0.0.1` は他端末から使えません。BackendがVPN側のアドレスで待ち受けていること、VPNの通信ルールとPCのファイアウォールも確認してください。Safari等で `http://PCのVPNアドレス:8000/health` に `status: ok` が表示されるか確認します。この `/health` は確認用で、アプリには付けません。ブラウザから到達できてもアプリが未接続なら、保存したURLとアプリの版を確認してください。管理画面の提供元が「接続済み」でも、iPhoneからPCへ到達できることを示すわけではありません。[Backend案内](BACKEND_CONSOLE.md)。

## English

- Builds supporting additional models offer Settings → AI → On-device AI → Add for single-file `.gguf` and compatible `.litertlm` text/chat models. Select the imported model in Models. No PC or Backend is required. Raw safetensors, split GGUF and separate vision/audio files are unsupported. Compatibility, speed and quality depend on the model, runtime and OS; large models may stop or crash. See [supported formats and release status](LOCAL_MODEL_IMPORT.md). The app also includes permanent instructions in Help.
- Get Backend through the desktop app ZIP in [GitHub Releases](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases), complete first-run setup, then start `Start_Yui_Backend.command` (Mac) or `Start_Yui_Backend.bat` (Windows) inside YuiBackend. The Code ZIP is source. For a phone connection, join both devices to Tailscale and enter the PC address in Settings. Backend is optional for on-device AI/voices and direct OpenAI access. Help → Connections and the Settings question-mark button provide acquisition and connection instructions in supporting builds.

- Download the **MacOSPublicBeta** or **WindowsPublicBeta** app ZIP from [Releases](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases). The Code ZIP is source only. See the [Mac guide](MAC_PUBLIC_BETA.en.md).
- The free Japan iOS release requires iOS 26 and is available on the [App Store](https://apps.apple.com/jp/app/id6815341780).
- Desktop first-run setup downloads approximately 2.5GB of AI/voice data plus its OS runtime after confirmation. Leave extra room for extraction. iOS includes standard E2B and speech data; E4B is optional. Mac, Windows and iOS offer optional E4B.
- E2B favors lighter offline use; E4B improves quality at the cost of latency, memory and storage. OpenAI API is recommended for higher-quality chat and requires an API key and API charges, separately from a ChatGPT subscription.
- Character settings accept written personality/tone instructions. Common and model-specific instructions also apply. Local advanced settings control context/output/thinking budgets and sampling per Talk/Work mode; defaults are a good starting point.
- Import your own **VRM**, not a purchase ZIP, prefab, FBX or unitypackage. Export from your configured Unity project first. Appearance changes keep the character's personality and memory. See [avatar import](AVATAR_IMPORT.en.md).
- Character memory persists across restart/local/API switching. History and long-term memory are separate. Secret mode reads existing memory without recording new chat; it does not prevent API transmission. See [memory](CONVERSATION_IDENTITY.md) and [privacy](PRIVACY.md).
- Default speech is Japanese VOICEVOX, with five standard voices. Mac/iOS 0.2.5 and later offer optional English Kokoro and Japanese Irodori downloads in Settings. Windows NVIDIA users can add Irodori to their Backend through the [Console](BACKEND_ASSET_DOWNLOADS.md#english). Check engine, mute, volume and installed voice data if speech fails.
- Retry downloads after checking connectivity/free space. Do not mix manifests/data from older releases. If still failing, report app version, OS, model, full error and steps via [Issues](https://github.com/Tsubame-chan/YuiVRMAIStudio/issues). Remove API keys, private conversations and private avatars.
- Replay the illustrated tutorial from Help. Support status and experimental integrations are listed in [runtime support](RUNTIME_SUPPORT.md).
