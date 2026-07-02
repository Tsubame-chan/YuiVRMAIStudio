# Yui VRM AI Studio

[English README](README.en.md)

**お気に入りのVRMキャラクターを、話し、見て、調べて、覚えるAIエージェントへ。**

Yui VRM AI Studioは、自分のVRMキャラクターをデスクトップ上のAIアバターとして動かし、テキスト・音声・画像・画面コンテキストを使って会話できるアプリです。VRChatで使っているキャラクター、創作キャラ、推しアバターを、ただ眺めるだけではなく、話し、覚え、調べ、日常作業を手伝ってくれる存在にすることを目指しています。

現在はWindows / macOS向けのDesktop Public Betaです。ReleaseのアプリZIPには最小構成のLocal Gemma SLMとLocal VOICEVOXを含めているため、バックエンドを立てなくてもまず試せます。より高品質な会話、リアルタイム会話/翻訳、会話DB、追加TTSを使いたい場合は、あとからOpenAI APIキーやローカルバックエンドを追加します。

## どんな体験か

- 自分の `.vrm` キャラクターを画面に表示し、そのキャラと会話できます。
- メッセージ入力、音声入力、画像入力、選択中カメラ/画面コンテキストを会話に使えます。
- ローカルメモリと会話履歴を使い、継続的なAIアバターとして育てていく方向のアプリです。
- バックエンドなしでも最低限試せます。フル機能はローカルバックエンドとBYOK設定で拡張します。
- 日本語音声はVOICEVOXを標準fallbackにし、AivisSpeech HDやIrodori TTSなどを任意で追加できます。

## どこから始めるか

| 対象 | 状態 | 入口 |
| --- | --- | --- |
| Windows Desktop Public Beta | 公開Beta | [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md) |
| macOS Desktop Public Beta | 公開Beta | [`docs/MAC_PUBLIC_BETA.md`](docs/MAC_PUBLIC_BETA.md) |
| iOS / Android | 公開候補として検証中 | 現時点ではDesktop Betaを優先 |

Windows / macOSのREADMEとセットアップ手順は、同じ設計思想で読めるように整理しています。最新版の実行ファイルは [GitHub Releases](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases) のBeta配布物を確認してください。

## ダウンロードの選び方

- すぐ使う: GitHub ReleasesからWindowsまたはmacOSのBeta配布物をダウンロードします。大きいZIPは `.part-*` に分割しているため、Release本文またはOS別ガイドの手順で結合してから展開します。最小構成のLocal Gemma SLMとLocal VOICEVOXを含むため、追加データなしでも起動して試せます。
- ソースを見る/改造する: GitHubの `Code > Download ZIP` や `git clone` はソースコード用です。実行ファイルや大型モデルは含まれないため、そのまま展開しただけでは完成アプリとしては不完全です。
- ソースからビルドする: Releaseの `LocalAIAssets_Minimum` をリポジトリ直下へ展開してからUnityでビルドします。
- 任意の高品質音声を使う: AivisSpeech HDやIrodori TTSなどは追加runtimeとして導入します。なくてもアプリは動きますが、バックエンド接続時の声の選択肢を増やせます。
- フル機能を使う: リアルタイム会話/翻訳、会話DB、Backend TTSを使う場合は、アプリZIPに加えてこのリポジトリのソースを取得し、セットアップガイドに従ってローカルバックエンドを起動します。

## できること

- VRM 1.0 / VRM 0.x の `.vrm` アバターをAIキャラクターとして表示・会話
- テキストチャット、音声入力、日本語音声応答
- 画像入力 / Vision、画面コンテキスト
- 会話履歴とローカルメモリ
- 天気、イベント、ニュース、場所などの現在情報に対するweb search支援
- OpenAI Realtime APIを使う低遅延会話の実験
- OpenAI Realtime STTとVOICEVOX TTSを組み合わせるRealtime VOICEVOXモード
- Auto SelectによるBackend優先 / Local fallbackのAIモード選択
- Local Gemma SLMによるオフライン・低通信環境向けの軽量会話
- ローカルVOICEVOX、Backend VOICEVOX、AivisSpeech HD、Irodori TTSの状態表示と選択
- リアルタイム翻訳モード

## 少し詳しい仕組み

アプリ本体はUnityで動きます。AI providerとの通信、会話DB、音声生成、画像処理などは、同じマシン上で起動するローカル補助サービス、またはアプリ内蔵のローカル実行基盤が担当します。

バックエンドを起動すると高品質な会話、リアルタイム会話/翻訳、会話DB、Backend TTSを使えます。バックエンドがない場合でも、Direct APIやLocal Gemma、ローカルVOICEVOXで最低限すぐ試せる方向へ整備しています。接続先やポートは環境に合わせて変更できるため、このREADMEでは特定の開発環境のURLやIPアドレスを前提にしていません。

## 技術メモ: Provider Status

### 主なprovider

- OpenAI: chat / STT / vision / realtime / translation / hosted web search
- VOICEVOX Engine: Japanese TTS runtime
- Local Gemma SLM: offline-first local chat fallback
- Local VOICEVOX: built-in/native Japanese TTS fallback where supported

### 実装済み・検証中

- 汎用HTTP TTS adapter: Irodori TTSなど、JSON-in/audio-out型の外部TTSを検証するための実験的な接続口
- Open-Meteo current weather API: web searchとは別に、構造化された現在天気を取得する実験的な接続口
- LM Studio local chat provider: OpenAI互換のローカル `/chat/completions` を使う実験的な接続口
- Grok / xAI chat provider: xAIのOpenAI互換 `/chat/completions` を使う実験的な接続口
- 共通Capability判定: Help画面と設定画面でBackend / Local / Direct APIの利用可否を同じ基準で表示

### Beta時点の安心材料

- 初回のおすすめは `Auto Select` です。Backendが健全ならBackendを優先し、なければLocal/Directへ戻ります。
- ReleaseアプリZIPは、最小構成だけで起動して試せる状態を前提にしています。
- Provider/modelは外部サービス側の変更で動作が変わることがあります。設定画面とHelp画面の接続状態を確認してください。

### 今後対応候補

- LM Studio / Ollamaなど、OpenAI互換ローカルLLM providerの拡充
- provider選択UI
- OS標準STT/TTSの横展開と品質検証
- 専用の地図・カレンダー等のAPI連携

## 必要なもの

最小構成:

- GitHub ReleasesのWindows / macOS Beta配布物
- `.vrm` アバターを使う場合はVRMファイル

追加機能:

- OpenAI APIや高品質な画像理解/STTを使う場合はOpenAI APIキー
- リアルタイム会話/翻訳、会話DB、Backend TTSを使う場合はPython 3.12+とローカルバックエンド
- 日本語音声を拡張する場合はVOICEVOX Engine、AivisSpeech HD、Irodori TTSなどの外部TTS runtime

OS別の詳細:

- Windows: [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md)
- macOS: [`docs/MAC_PUBLIC_BETA.md`](docs/MAC_PUBLIC_BETA.md)

### Git cloneとRelease配布物の違い

GitHubの通常リポジトリには、巨大なGemmaモデル本体、音声モデル、音声辞書、生成済みビルドを入れません。これらは容量とライセンス境界の都合で、GitHub ReleasesのBeta配布物側に分けます。Releaseのアプリ配布物は最小構成だけで動きます。追加音声やソースビルド用assetは必要な人だけ取得してください。詳しくは [`docs/LOCAL_AI_ASSETS.md`](docs/LOCAL_AI_ASSETS.md) を参照してください。

### TTS / Irodori検証

日本語音声の標準fallbackはVOICEVOXです。PC版では、バックエンド未接続でも使えるローカルVOICEVOXを優先し、バックエンド接続時はより細かく調整できるVOICEVOX EngineやAivisSpeech HD、Irodori TTSを選べる方針です。Irodori TTSは追加検証中で、OS別に候補を分けています。

- macOS Apple Silicon: [`docs/IRODORI_TTS_PACKAGING.md`](docs/IRODORI_TTS_PACKAGING.md) の MLX VoiceDesign 経路
- Windows NVIDIA: [`docs/IRODORI_TTS_WINDOWS_NVIDIA.md`](docs/IRODORI_TTS_WINDOWS_NVIDIA.md) の Irodori-TTS-Server 経路
- Windows CPU / GPUなし: VOICEVOX推奨

大型モデル本体やTTSサーバー本体は、ライセンスと容量の都合でGit管理には入れません。必要なruntimeはユーザー環境で導入するか、GitHub Releasesの配布物として分けて扱います。Irodoriが失敗した場合は `TTS_FALLBACK_PROVIDER=voicevox` でVOICEVOXへ戻せる構成にしています。

Unityアプリ側のBackend URLは、VOICEVOXやIrodoriのURLではなく、常にYui backendを指定します。通常は `http://127.0.0.1:8000`、iPhoneなど別端末から同じPC/Macのバックエンドへ接続する場合は `http://<PCまたはMacのLAN/VPN IP>:8000` です。VOICEVOXやIrodoriのURLは `.env` と起動スクリプト側で管理します。

## 自分のVRMキャラクターを使う

このbeta版が直接読み込めるのは `.vrm` ファイルです。VRChat SDKのアバター、Unityシーン、Unity prefab、`.unitypackage`、VRChatにアップロード済みのアバターそのものは直接読み込めません。

VRChat用のUnityプロジェクトで管理しているアバターを使いたい場合は、元のBOOTH/配布パッケージに `.vrm` が含まれていないか確認してください。ない場合は、Unity/UniVRMやBlender/VRMのワークフローで別途VRMとして書き出してから読み込んでください。

## Privacy / Data Flow

Yui VRM AI StudioはBYOK方式です。APIキーはユーザー自身のPC/Mac上の `.env` に保存されます。

有効にした機能によって、以下の情報が設定済みの外部AI providerへ送信される可能性があります。

- チャット本文
- 音声入力
- アップロード画像
- スクリーンショット / 画面コンテキスト
- 翻訳対象の音声・テキスト
- web searchが必要な質問内容

以下はローカルに保存されます。

- `.env`
- SQLiteの会話DB
- VOICEVOX生成音声キャッシュ
- ログ

画面コンテキストやリアルタイム翻訳を使う場合は、画面上・音声経路上の機密情報に注意してください。

## Roadmap

### Desktop Public Beta

- Windows / macOS Desktop Public Betaの共通化
- OpenAI chat / STT / vision / web search
- VOICEVOX TTS
- Local Gemma SLMとDirect API fallback
- Auto SelectによるBackend優先 / Local fallback
- Backend VOICEVOX / AivisSpeech HD / Irodori TTSの選択
- 会話履歴・メモリ
- 画像/画面コンテキスト
- Realtime系の実験機能

### Next

- Windows / macOSのRelease配布手順の改善
- 鑑賞モードとデスクトップ操作性の継続改善
- 汎用HTTP TTS adapterを使ったIrodori TTS等の検証
- provider選択UI
- Grok / xAI API providerの実キー疎通
- LM Studioを中心としたOpenAI互換ローカルLLM providerの実機疎通
- 専用の天気APIのチャット統合、地図・カレンダー等のAPI連携

### Future

- iOS / Androidの公開版検討
- 外部アプリ音声ブリッジ
- YouTube / ゲーム / 配信 / 通話音声のリアルタイム翻訳
- フィジカルAI / 外部デバイス連携

## 詳細ドキュメント

- Windowsセットアップ: [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md)
- macOS Public Beta: [`docs/MAC_PUBLIC_BETA.md`](docs/MAC_PUBLIC_BETA.md)
- Irodori Windows NVIDIA検証: [`docs/IRODORI_TTS_WINDOWS_NVIDIA.md`](docs/IRODORI_TTS_WINDOWS_NVIDIA.md)
- Irodori optional backend packaging: [`docs/IRODORI_TTS_PACKAGING.md`](docs/IRODORI_TTS_PACKAGING.md)
- API仕様: [`docs/api.md`](docs/api.md)
- 外部情報 / web search方針: [`docs/LLM_EXTERNAL_INFO.md`](docs/LLM_EXTERNAL_INFO.md)
- ローカルAI/TTS asset配布: [`docs/LOCAL_AI_ASSETS.md`](docs/LOCAL_AI_ASSETS.md)
- 品質と検証方針: [`docs/QUALITY_AND_VALIDATION.md`](docs/QUALITY_AND_VALIDATION.md)

## トラブルシューティング

初回起動で問題がある場合は、まず使っているOSのセットアップガイドを確認してください。

- Windows: [`docs/SETUP_GUIDE.md#troubleshooting`](docs/SETUP_GUIDE.md#troubleshooting)
- macOS: [`docs/MAC_PUBLIC_BETA.md`](docs/MAC_PUBLIC_BETA.md)

よくある原因:

- ローカル補助サービスが起動していない
- `.env` に `OPENAI_API_KEY` が設定されていない
- VOICEVOX Engineが見つからない、または起動していない
- Windows版で `YuiFilePickerHelper.exe` がアプリ本体と同じフォルダにない

開発者向けのAPI詳細や診断手順は [`docs/api.md`](docs/api.md) を参照してください。

## License And Credits

Project code is released under the MIT License. See [`LICENSE`](LICENSE).

Third-party assets and libraries keep their own licenses.

- UnityChan assets are distributed under the Unity-Chan License Terms.
- VOICEVOX/VOICEVOX Engine follows the upstream VOICEVOX terms.
- If you publish generated speech, include the required VOICEVOX credit for the selected voice. The default beta voice is `VOICEVOX:冥鳴ひまり`.
- ChatdollKit, lilToon, UniVRM, and other Unity packages remain under their respective licenses.
